# Design

## Context

El proxy existente de Ars Docendi es Traefik. El usuario aprobó reutilizarlo, retirando el proxy añadido en la implementación preliminar. Alcance sólo infraestructura/documentación: ninguna aplicación, workflow, variable de backend, gateway o suite de tests.

Topología declarada: pc-prod tiene GPU RTX 5070 y Tailscale directo; vm-dev aloja staging/pr-N sin cliente, accediendo por una VM subnet router separada. Conexión bidireccional confirmada por operador, no aceptación HTTPS/GPU desde esta sesión.

```text
vm-dev → VM subnet router → Tailscale → pc-prod:HTTPS
                                        │ Serve
                                        ▼
                              Traefik existente (ollama)
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

Agregar únicamente `entryPoints.ollama` (:11435) al archivo estático Traefik, conservando web/dashboard/providers. Operador publica 127.0.0.1:11435:11435 y Serve HTTPS hacia ese loopback. El bind es al host loopback, no al loopback dentro del contenedor: la publicación Docker necesita que el entrypoint acepte en su interfaz de contenedor.

Labels en Ollama: router `ollama-privada` con entrypoint explícito `ollama`, reglas de GET version/tags y POST chat/generate, BasicAuth y servicio HTTP 11434. `traefik.docker.network` sobreescribe la red Docker pública por defecto del provider. Ningún router IA en web/Cloudflare, ni archivos dinámicos IA globales que exijan auth en Proxmox. No se configura Funnel.

Habilitación inicial requiere recrear Traefik para nuevos puertos/mounts/red: reiniciar solamente no agrega esas opciones. Documentar inventario de imagen/opciones existentes, ventana, backup reversible y comprobación pública; nunca ejecutarlo en esta sesión ni clonar un docker run sin preservar extras reales. No actualizar su imagen por este cambio.

Alternativa de proxy adicional descartada: duplica un componente existente. Gateway custom excluido por alcance. Ollama directo sin autenticación también descartado.

### 3. BasicAuth y rotación explícita

Traefik monta carpeta protegida del operador sólo lectura en `/etc/traefik/ollama-auth`, con archivo `htpasswd`. Compose sólo referencia la ruta interna en labels, no hashes/passwords. `removeHeader=true` evita reenviar Authorization. Garantizar permisos del usuario actual de Traefik; no crear variables UID/GID de un proxy nuevo ni permisos mundiales.

BasicAuth carga usersFile al construir middleware. Cambiar sólo el archivo no garantiza relectura/revocación, incluso montando carpeta. Tras alta/rotación/baja, aumentar `OLLAMA_AUTH_REVISION` y recrear exclusivamente Ollama: realm cambia, provider recibe config diferente y reconstruye auth. Interrumpe brevemente IA, no reinicia Traefik/aplicaciones. Confirmar usuario revocado y válido después de reload; mantener al menos un usuario porque archivo vacío/ilegible invalida middleware y falla cerrado (puede devolver 404). Administración manual, sin TTL ni scopes GitHub/entrega automática a pr-N.

### 4. Streaming y límites honestos

Servicio Traefik con flushInterval negativo para flush inmediato, sin Buffering ni Retry. No se traslada el antiguo body cap 64 KiB porque Buffering puede alterar streaming. Configuración no limita body/tokens por ambiente, no inspecciona JSON ni impone allowlist de modelos. Defaults globales Ollama acotados no son políticas inviolables ni prioridades por ambiente.

Timeout privado: 30 s para lectura de petición, writeTimeout 0 para no cortar streaming por plazo global, idle 60 s. Cliente debe gestionar su deadline; no agregar lógica de aplicación en este cambio. Deshabilitar access logs del entrypoint privado; conservar observabilidad pública.

### 5. DNS/TLS y acceso desde subred

Serve termina HTTPS de FQDN de pc-prod; resolver ese nombre a su IP Tailscale desde vm-dev/Docker con DNS accesible. No suponer MagicDNS disponible por ruta de subred ni usar `-k`/URL IP con certificado de nombre. `--resolve` sólo diagnóstico.

Reusar VM router y retorno existentes. Inventariar origen efectivo tras NAT y limitar policy/firewall al destino/puerto necesario. No instalar Tailscale en vm-dev o contenedores. Sondear desde namespace/red real sin modificar consumidores.

### 6. Configuración y verificación acotadas

Conservar `OLLAMA_IMAGEN`, `OLLAMA_ARCHIVO_AUTH` (información del operador), `OLLAMA_MODELO` y límites nativos. Retirar `OLLAMA_PROXY_IMAGEN/UID/GID/PUERTO_PROXY`; agregar red externa y revisión auth. Archivo auth/modelo sólo operación, no mounts del Compose de Ollama. Ejemplo sin secretos.

En entorno aislado sin GPU/Tailscale, validar Compose y ejecutar la versión existente de Traefik con file provider materializado a partir de las labels, sin montar socket real ni leer secretos de otras aplicaciones. Esto verifica parser/reglas/auth/streaming, pero NO acredita descubrimiento Docker real ni corte del proxy compartido; éstos quedan en 3.2. Contenedores de validación se marcan `traefik.enable=false` para no afectar el provider compartido.

## Risks / Trade-offs

- [Proxy compartido en habilitación inicial] → ventana y backup reversible, conservar opciones/imagen y routing público; no reiniciar desde apply.
- [Rotación afecta IA brevemente] → recreación de Ollama/revisión explícita, sin reiniciar proxy compartido.
- [Red externa queda al parar IA] → previsto: pertenece al operador/Traefik, no borrarla por teardown de aplicación.
- [Competencia GPU/RAM con producción] → límites conservadores/operación manual, sin prometer reserva o SLO.
- [Fallo DNS/router/certificado] → HTTPS desde namespace real, preservar rutas; no fallback público.
- [Host/daemon no comparten rutas] → operar en pc-prod y validar mounts; usar stdin/volúmenes temporales para validación local aislada.

## Migration Plan

Retirar el archivo del proxy descartado → revisar/validar archivos → operador prepara GPU y auth/red → ventana para recrear Traefik existente con nuevo entrypoint/mount/port/red → Ollama/modelo/GPU → Serve → comprobación local/subred/Docker y público intacto. Sin despliegues/configuración de aplicaciones.

Rollback IA: retirar mapping Serve propio y detener sólo proyecto Ollama sin -v; provider elimina su router, Traefik/red externa siguen. Rollback de corte inicial: retirar contenedor nuevo del proxy y recuperar config/contenedor/imagen anteriores en ventana aprobada. No restaurar DB/storage, modificar DNS público o detener proyectos de aplicaciones.

## Open Questions

Antes de operación real: IP/FQDN/CIDR/origen efectivo/puerto libre, rutas y permisos auth, imagen/options del Traefik de pc-prod y recursos/modelo dimensionados. No son preguntas de integración backend.

## Referencias

Traefik v3.7.5 existente en el daemon consultado, sin asumir que pc-prod usa esa misma versión: documentación/código oficial de BasicAuth, entrypoints y servicios en `github.com/traefik/traefik` (tag v3.7.5). Referencias de Ollama, Docker, NVIDIA y Tailscale en el runbook implementado. Mantener evidencia de validación aislada separada del gate manual real.
