# Design

## Context

El proxy existente de Ars Docendi es Traefik. El usuario aprobó reutilizarlo, retirando el proxy añadido en la implementación preliminar. Alcance sólo infraestructura/documentación: ninguna aplicación, workflow, variable de backend, gateway o suite de tests.

Topología declarada: pc-prod (Debian) tiene GPU RTX 5070, prod y el túnel Cloudflare principal; vm-dev (Proxmox) aloja staging/pr-N con su propio túnel. Se descarta Tailscale Serve: staging/pr-N llegan por el túnel principal; prod, sólo localmente.

```text
vm-dev ──HTTPS + Service Token──▶ Cloudflare Access ──túnel principal──▶ cloudflared (JWT)
                                                                              │ red traefik
backend prod ─────────────────── red traefik ──────────────────────▶ Traefik :11435 (ollama)
                                                                              │ red privada
                                                                              ▼
                                                                        Ollama → GPU
```

## Goals / Non-Goals

**Goals:** entregar Ollama Docker persistente, privado y autenticado con el Traefik compartido, y documentación de habilitación reversible.

**Non-Goals:** desarrollar aplicaciones/proxy, agregar otra instancia productiva Traefik, modificar backend/frontend/CI/CD, automatizar pr-N, cuotas/prioridad, RAG/historial o tests. No conectar aplicaciones a red Ollama ni reiniciar proxy compartido desde apply.

## Decisions

### 1. Compose exclusivamente Ollama

`infra/ollama/compose.yml` contiene sólo servicio `ollama`, imagen fijada por digest, reserva NVIDIA, volumen `docendi-ai-modelos`, reinicio, límites CPU/RAM/cola/concurrencia y logs acotados. No hay ports, otro proxy o Docker socket en ese servicio. Modo local `OLLAMA_NO_CLOUD=1`; salida a Internet necesaria para modelos. GPU/runtime permanece prerrequisito host y gate manual.

Red `docendi-ai-inferencia` externa administrada por operador, uniendo sólo Ollama y Traefik existente. No reutilizar `traefik`/`arsdocendi-datos` para Ollama. External evita que `oc down` intente eliminar una red todavía usada por el proxy compartido; volumen sigue aislado por proyecto.

### 2. EntryPoint privado y labels Docker

Agregar únicamente `entryPoints.ollama` (:11435) al archivo estático Traefik, conservando web/dashboard/providers. Escucha en la interfaz del contenedor: lo alcanzan cloudflared y el backend prod por la red Docker `traefik` existente. El operador publica además 127.0.0.1:11435:11435 sólo para su CLI. Sin `forwardedHeaders`, para que rateLimit/inFlightReq cuenten por IP remota real.

Labels en Ollama: router `ollama-privada` con entrypoint explícito `ollama`, reglas de GET version/tags y POST chat/generate, BasicAuth y servicio HTTP 11434. `traefik.docker.network` sobreescribe la red Docker pública por defecto del provider. Ningún router IA en `web`, ni archivos dinámicos IA globales que exijan auth en Proxmox. Cadena: `ollama-tasa` (rateLimit, antes de auth para frenar fuerza bruta) → `ollama-auth` → `ollama-concurrencia` (inFlightReq).

Habilitación inicial requiere recrear Traefik para nuevos puertos/mounts/red: reiniciar solamente no agrega esas opciones. Documentar inventario de imagen/opciones existentes, ventana, backup reversible y comprobación pública; nunca ejecutarlo en esta sesión ni clonar un docker run sin preservar extras reales. No actualizar su imagen por este cambio.

Alternativa de proxy adicional descartada: duplica un componente existente. Gateway custom excluido por alcance. Ollama directo sin autenticación también descartado.

### 3. BasicAuth y rotación explícita

Traefik monta carpeta protegida del operador sólo lectura en `/etc/traefik/ollama-auth`, con archivo `htpasswd`. Compose sólo referencia la ruta interna en labels, no hashes/passwords. `removeHeader=true` evita reenviar Authorization. Garantizar permisos del usuario actual de Traefik; no crear variables UID/GID de un proxy nuevo ni permisos mundiales.

BasicAuth carga usersFile al construir middleware. Cambiar sólo el archivo no garantiza relectura/revocación, incluso montando carpeta. Tras alta/rotación/baja, aumentar `OLLAMA_AUTH_REVISION` y recrear exclusivamente Ollama: realm cambia, provider recibe config diferente y reconstruye auth. Interrumpe brevemente IA, no reinicia Traefik/aplicaciones. Confirmar usuario revocado y válido después de reload; mantener al menos un usuario porque archivo vacío/ilegible invalida middleware y falla cerrado (puede devolver 404). Administración manual, sin TTL ni scopes GitHub/entrega automática a pr-N.

### 4. Streaming y límites honestos

Servicio Traefik con flushInterval negativo para flush inmediato, sin Buffering ni Retry. No se traslada el antiguo body cap 64 KiB porque Buffering puede alterar streaming. Configuración no limita body/tokens por ambiente, no inspecciona JSON ni impone allowlist de modelos. Defaults globales Ollama acotados no son políticas inviolables ni prioridades por ambiente.

Timeout privado: 30 s para lectura de petición, writeTimeout 0 para no cortar streaming por plazo global, idle 60 s. Cliente debe gestionar su deadline; no agregar lógica de aplicación en este cambio. Deshabilitar access logs del entrypoint privado; conservar observabilidad pública.

### 5. Cloudflare Tunnel + Access (staging/pr-N) y ruta local (prod)

Defensa en capas, todas obligatorias:

1. Hostname dedicado `ia.<DOMINIO>` sólo en el túnel principal → `http://traefik:11435`. CNAME explícito `ia` gana al wildcard de Proxmox; su túnel responde 404 a `ia` antes del wildcard.
2. Cloudflare Access self-hosted, única política Service Auth: Service Token `vm-dev-ia` (vencimiento ≤ 1 año) **y** IP de egreso de vm-dev. Corta en el borde: sin token o desde otra IP no llega al origen.
3. `originRequest.access.required` en cloudflared con teamName/AUD: el origen rechaza sin JWT válido aunque la aplicación Access se borre o relaje.
4. WAF: rate limiting del host y bloqueo de métodos ≠ GET/POST.
5. Traefik: allowlist método/ruta, rateLimit, inFlightReq y BasicAuth con usuario `staging` distinto de `prod`.

Prod no usa el túnel: llama `http://traefik:11435` por la red `traefik`, sin salir del host, y su credencial no tiene Service Token, inútil desde Internet. Un fallback por túnel no aporta disponibilidad (mismo host/Traefik) y se descarta. Alternativa WARP Connector/red privada descartada: agrega un componente en Proxmox. La entrega del Service Token a staging/pr-N pertenece al change de integración.

### 6. Configuración y verificación acotadas

Conservar `OLLAMA_IMAGEN`, `OLLAMA_ARCHIVO_AUTH` (información del operador), `OLLAMA_MODELO` y límites nativos. Retirar `OLLAMA_PROXY_IMAGEN/UID/GID/PUERTO_PROXY`; agregar red externa y revisión auth. Archivo auth/modelo sólo operación, no mounts del Compose de Ollama. Ejemplo sin secretos.

En entorno aislado sin GPU/Cloudflare, validar Compose y ejecutar la versión existente de Traefik con file provider materializado a partir de las labels, sin montar socket real ni leer secretos de otras aplicaciones. Esto verifica parser/reglas/auth/streaming, pero NO acredita descubrimiento Docker real ni corte del proxy compartido; éstos quedan en 3.2. Contenedores de validación se marcan `traefik.enable=false` para no afectar el provider compartido.

## Risks / Trade-offs

- [Proxy compartido en habilitación inicial] → ventana y backup reversible, conservar opciones/imagen y routing público; no reiniciar desde apply.
- [Rotación afecta IA brevemente] → recreación de Ollama/revisión explícita, sin reiniciar proxy compartido.
- [Red externa queda al parar IA] → previsto: pertenece al operador/Traefik, no borrarla por teardown de aplicación.
- [Competencia GPU/RAM con producción] → límites conservadores/operación manual, sin prometer reserva o SLO.
- [Service Token filtrado] → IP de egreso requerida, BasicAuth y rate limiting; revocación inmediata en Zero Trust.
- [IP de egreso de vm-dev cambia] → Access devuelve 403 (falla cerrado); actualizar la política.
- [Host/daemon no comparten rutas] → operar en pc-prod y validar mounts; usar stdin/volúmenes temporales para validación local aislada.

## Migration Plan

Retirar el archivo del proxy descartado → revisar/validar archivos → operador prepara GPU y auth/red → ventana para recrear Traefik existente con nuevo entrypoint/mount/port/red → Ollama/modelo/GPU → Access/DNS/WAF e ingress `ia` → comprobación Internet/vm-dev/prod y público intacto. Sin despliegues/configuración de aplicaciones.

Rollback IA: retirar regla `ia` del ingress y la aplicación Access, y detener sólo proyecto Ollama sin -v; provider elimina su router, Traefik/red externa siguen. Rollback de corte inicial: retirar contenedor nuevo del proxy y recuperar config/contenedor/imagen anteriores en ventana aprobada. No restaurar DB/storage, modificar DNS público o detener proyectos de aplicaciones.

## Open Questions

Antes de operación real: dominio, team name/AUD, IP de egreso estable de vm-dev, permisos auth, imagen/options del Traefik de pc-prod y recursos/modelo dimensionados. No son preguntas de integración backend.

## Referencias

Traefik v3.7.5 existente en el daemon consultado, sin asumir que pc-prod usa esa misma versión: documentación/código oficial de BasicAuth, entrypoints y servicios en `github.com/traefik/traefik` (tag v3.7.5). Referencias de Ollama, Docker, NVIDIA y Cloudflare en el runbook implementado. Mantener evidencia de validación aislada separada del gate manual real.
