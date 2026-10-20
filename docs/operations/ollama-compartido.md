# Ollama compartido — instalación y operación

## Alcance y topología

Sólo infraestructura: `infra/ollama/{compose.yml,.env.example}`, el entrypoint privado de `infra/traefik/traefik.yml` y la regla `ia` de `infra/cloudflared/`. No modifica backend, frontend, CI/CD ni aplicaciones; no crea tests. El endpoint queda preparado para una integración posterior, no implementa el chatbot.

```text
vm-dev (staging/pr-N) ──HTTPS + Service Token──▶ Cloudflare Access (borde)
                                                        │ túnel principal
pc-prod: cloudflared (valida JWT) → Traefik :11435 (entrypoint ollama) → Ollama → GPU
pc-prod: backend prod ──red Docker `traefik`──────────▶ Traefik :11435
```

Proyecto `docendi-ai` sólo con Ollama, volumen `docendi-ai-modelos` y red externa privada `docendi-ai-inferencia` compartida únicamente con Traefik. Ollama no publica puertos; el Traefik existente publica su entrypoint `ollama` sólo en `127.0.0.1:11435`. No se agrega otro proxy ni socket Docker a Ollama. El túnel principal enruta `ia.<DOMINIO>` al entrypoint `ollama` (nunca a `web`) sólo tras Cloudflare Access; prod no usa el túnel y su credencial no tiene Service Token, por lo que no sirve desde Internet. Capas y configuración de Cloudflare: [infra/cloudflared/README.md](../../infra/cloudflared/README.md#ia-privada-iadominio).

## 1. Inventario y ventana — operador

En **pc-prod**, registrar Debian/kernel, `nvidia-smi`, `docker context show`, `docker version`, `docker compose version`, `docker inspect --format '{{json .NetworkSettings.Networks}}' cloudflared` (debe compartir la red `traefik`) y espacio libre. En **vm-dev**, registrar la IP pública de egreso (`curl -fsS https://cloudflare.com/cdn-cgi/trace | grep '^ip='`) desde el host y desde un contenedor; debe ser estable para la política Access.

Usar Bash y detenerse ante cualquier comando fallido; no continuar una recreación parcial. En la shell operativa: `set -euo pipefail`.

Completar dominio, team name de Zero Trust, IP de egreso de vm-dev, RAM/CPU/disco disponibles y modelo local. La habilitación inicial cambia configuración estática/mounts/ports de Traefik: requiere recrear el contenedor compartido en ventana aprobada, sin tocar las aplicaciones. Configuración validada con Traefik v3.7.5; comprobar compatibilidad de la versión instalada antes de aplicar, sin actualizarla silenciosamente.

> Los bloques de administración siguientes son para el operador autorizado en una ventana aprobada. No los ejecuta la aplicación de OpenSpec ni CI. Instalar/reconfigurar driver o reiniciar Docker puede interrumpir producción. Si driver/Docker/toolkit ya funcionan, omitir su instalación; no sustituirlos ciegamente.

## 2. Prerrequisitos Debian 13 amd64 — administración de pc-prod

Comprobar distribución/arquitectura antes de usar estos comandos; para otra versión seguir sus referencias oficiales. Secure Boot requiere módulos firmados/enrolamiento MOK; si falta el paquete de headers del kernel actual, resolverlo antes de instalar DKMS. No mezclar instalaciones `.run` y paquetes APT.

```bash
. /etc/os-release
test "$ID" = debian && test "$VERSION_ID" = 13
test "$(dpkg --print-architecture)" = amd64
uname -r
```

### Utilidades comunes (también si Docker ya existe)

```bash
apt-get update
apt-get install -y ca-certificates curl gnupg apache2-utils
```

### Docker (sólo si falta)

No eliminar ni reinstalar paquetes en un host productivo existente. Para un host limpio, comandos del administrador:

```bash
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
printf '%s\n' 'Types: deb' 'URIs: https://download.docker.com/linux/debian' 'Suites: trixie' 'Components: stable' 'Architectures: amd64' 'Signed-By: /etc/apt/keyrings/docker.asc' > /etc/apt/sources.list.d/docker.sources
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl status docker --no-pager
```

Preparar acceso Docker del usuario operador según política local (el grupo Docker otorga privilegios equivalentes sobre el host). No instalar Docker Desktop. Si Docker ya existe, instalar únicamente utilidades ausentes, incluyendo `apache2-utils` para `htpasswd`.

### Driver NVIDIA (sólo si falta o es incompatible)

Usar el driver NVIDIA con módulos abiertos y soporte RTX 5070/Blackwell; no asumir que cualquier `nvidia-driver` de Debian reconoce esta GPU. Verificar `contrib` en las fuentes APT de Debian según la guía NVIDIA. Rama aprobada/pinning y compatibilidad del kernel son decisiones del administrador. Para **compute/headless**, con el repositorio NVIDIA oficial:

```bash
apt-get install -y linux-headers-"$(uname -r)"
curl -fSL https://developer.download.nvidia.com/compute/cuda/repos/debian13/x86_64/cuda-keyring_1.1-1_all.deb -o /tmp/cuda-keyring_1.1-1_all.deb
dpkg -i /tmp/cuda-keyring_1.1-1_all.deb
apt-get update
apt-get -V install nvidia-driver-cuda nvidia-kernel-open-dkms
# Reiniciar el host sólo en la ventana aprobada si lo requiere la instalación.
```

Si la PC también necesita escritorio, la alternativa oficial es `apt-get -V install nvidia-open`, no ambos comandos por costumbre. Después del reinicio verificar `nvidia-smi`: GPU y driver reconocidos, sin fallos de módulos; verificar servicios previos. No se necesita instalar CUDA Toolkit completo en el host para ejecutar esta imagen.

### NVIDIA Container Toolkit

```bash
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey -o /tmp/nvidia-container-toolkit.gpg
gpg --dearmor --yes -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg /tmp/nvidia-container-toolkit.gpg
printf '%s\n' 'deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://nvidia.github.io/libnvidia-container/stable/deb/$(ARCH) /' > /etc/apt/sources.list.d/nvidia-container-toolkit.list
apt-get update
apt-get install -y nvidia-container-toolkit
nvidia-ctk runtime configure --runtime=docker
systemctl restart docker
```

El último paso modifica configuración del daemon y lo reinicia: aprobar ventana y comprobar aplicaciones después. Esta guía usa Docker Engine convencional; rootless necesita el procedimiento NVIDIA específico. No aplicar el bloque en vm-dev.

## 3. Configuración y credenciales — usuario operador en pc-prod

Trabajar en checkout revisado, local al filesystem del daemon. No montar rutas que sólo existan en SSH/devcontainer. No hay cambios de GitHub. Si ya usaste la versión preliminar con otro proxy, conservar credenciales/modelos y editar el archivo existente, no recrearlo ni usar `htpasswd -c` otra vez.

```bash
cd /opt/ars-docendi/repo  # ajustar al checkout aprobado real
export OLLAMA_CONFIG_DIR="$HOME/.config/ars-docendi/ollama"
export OLLAMA_ENV_FILE="$OLLAMA_CONFIG_DIR/servicio.env"
export OLLAMA_AUTH_DIR="$OLLAMA_CONFIG_DIR/auth"
install -d -m 700 "$OLLAMA_CONFIG_DIR" "$OLLAMA_AUTH_DIR"
if [ ! -e "$OLLAMA_ENV_FILE" ]; then
    install -m 600 infra/ollama/.env.example "$OLLAMA_ENV_FILE"
fi
# Migración opcional de credenciales de la guía preliminar; no sobrescribir.
if [ ! -e "$OLLAMA_AUTH_DIR/htpasswd" ] && [ -f "$OLLAMA_CONFIG_DIR/htpasswd" ]; then
    install -m 600 "$OLLAMA_CONFIG_DIR/htpasswd" "$OLLAMA_AUTH_DIR/htpasswd"
fi
if [ ! -e "$OLLAMA_AUTH_DIR/htpasswd" ]; then
    htpasswd -B -C 12 -c "$OLLAMA_AUTH_DIR/htpasswd" prod
fi
chmod 600 "$OLLAMA_AUTH_DIR/htpasswd"
stat -c '%a %u:%g %n' "$OLLAMA_AUTH_DIR/htpasswd"
oc() { docker compose --env-file "$OLLAMA_ENV_FILE" -p docendi-ai -f infra/ollama/compose.yml "$@"; }
oc config --quiet
```

Password interactivo: nunca `htpasswd -b`, passwords en argumentos, imágenes o labels. El usuario del Traefik existente debe poder leer carpeta/archivo protegidos; si no es el propietario, el administrador concede permisos mínimos a su UID/grupo, no lectura mundial. Se monta la **carpeta** en `/etc/traefik/ollama-auth:ro` y el archivo se llama `htpasswd`. Montar sólo `$OLLAMA_AUTH_DIR`, no la carpeta de configuración/backups completa. `usersFile` guarda hashes leídos al construir el middleware, no hay reload automático sólo por editarlo.

Editar `servicio.env` fuera de Git: red externa, revisión de autenticación, `OLLAMA_ARCHIVO_AUTH` igual a la ruta absoluta de `$OLLAMA_AUTH_DIR/htpasswd`, recursos y modelo. La ruta host es información del operador, no un mount del Compose de Ollama. No exportar todo el archivo al shell: podría pisar futuros cambios de `--env-file`. Modelo y límites son defaults, no allowlist/cuotas.

| Configuración preliminar                                      | Configuración corregida                                                                    |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| `OLLAMA_PROXY_IMAGEN`, `OLLAMA_PROXY_UID`, `OLLAMA_PROXY_GID` | Eliminadas: se reutiliza Traefik con su imagen/usuario actuales.                           |
| `OLLAMA_PUERTO_PROXY`                                         | Eliminada: entrypoint estático 11435; si se cambia, actualizar publicación e ingress `ia`. |
| `OLLAMA_ARCHIVO_AUTH`                                         | Conservada como ruta host; montar su carpeta en Traefik, no en Ollama.                     |
| —                                                             | `OLLAMA_RED_TRAEFIK=docendi-ai-inferencia`, `OLLAMA_AUTH_REVISION=1`.                      |

No agregar opciones a backend/frontend, modificar `.env.example` de aplicaciones ni scopes GitHub.

## 4. Preparar red y habilitar Traefik — operador, pc-prod

Crear sólo la red privada si no existe; no usar `traefik` o `arsdocendi-datos` como red de Ollama:

```bash
export OLLAMA_RED_TRAEFIK=docendi-ai-inferencia  # coincidir con servicio.env
if ! docker network inspect "$OLLAMA_RED_TRAEFIK" >/dev/null 2>&1; then
    docker network create "$OLLAMA_RED_TRAEFIK"
fi
TRAEFIK_IMAGEN="$(docker inspect --format '{{.Image}}' traefik)"
export TRAEFIK_IMAGEN
docker run --rm --user "$(id -u):$(id -g)" --entrypoint traefik "$TRAEFIK_IMAGEN" version
docker inspect --format '{{json .Mounts}} {{json .HostConfig.PortBindings}} {{json .NetworkSettings.Networks}}' traefik
```

Inventariar sólo esos campos, no `inspect` completo con entornos. Respaldar configuración estática realmente usada y forma actual de crear el contenedor. Preservar todas sus redes/mounts/puertos/opciones, imagen y router público; usar el mecanismo de operación existente si Traefik está gestionado por otro Compose/systemd. No crear una segunda instancia productiva.

Cambios necesarios en Traefik existente: cargar el `entryPoints.ollama` entregado (`:11435`), publicar **127.0.0.1:11435:11435**, montar carpeta auth sólo lectura y conectar red privada **además** de su red pública. No basta `docker restart` para añadir mounts/ports/redes. El entrypoint HTTP privado queda detrás de Cloudflare (TLS en el borde) y del cloudflared local; no requiere ACME. Sin `forwardedHeaders`: rateLimit/inFlightReq cuentan por IP remota real (cloudflared o backend prod), no por `X-Forwarded-For`.

Si el contenedor corresponde exactamente al `docker run` documentado en `infra/README.md`, ésta es la recreación reversible **sólo en ventana aprobada**. Si tiene opciones adicionales, incorporarlas antes; no usar el ejemplo como clon automático. Guardar config anterior protegida antes de instalar el nuevo archivo; `docker cp traefik:/etc/traefik/traefik.yml "$OLLAMA_CONFIG_DIR/traefik-antes.yml"` captura su archivo visible, no necesariamente la config que cargó si fue editada en caliente.

```bash
# No sobrescribir un backup de contenedor existente:
test -z "$(docker ps -a --filter 'name=^/traefik-antes-ollama$' --format '{{.Names}}')"
docker rename traefik traefik-antes-ollama
docker stop traefik-antes-ollama
# Sustituir rutas por las reales del daemon; AUTH debe ser la carpeta del htpasswd.
docker create --name traefik --network traefik --restart unless-stopped \
  -p 127.0.0.1:8080:8080 -p 127.0.0.1:11435:11435 \
  -v /var/run/docker.sock:/var/run/docker.sock:ro \
  -v /opt/ars-docendi/repo/infra/traefik/traefik.yml:/etc/traefik/traefik.yml:ro \
  -v /opt/ars-docendi/repo/infra/traefik/dynamic:/etc/traefik/dynamic:ro \
  -v "$OLLAMA_AUTH_DIR:/etc/traefik/ollama-auth:ro" "$TRAEFIK_IMAGEN"
docker network connect "$OLLAMA_RED_TRAEFIK" traefik
docker start traefik
docker port traefik 11435/tcp  # esperado: 127.0.0.1:11435
docker logs --tail 30 traefik
```

Revisar ausencia de errores de parser/provider, comprobar routing público existente y mantener backup detenido hasta aceptar corte. No publicar este puerto ni montar auth en vm-dev/Proxmox: allí sólo se consume el endpoint. Ollama declara `traefik.docker.network` y router **exclusivamente** en `ollama`; no hay router IA en `web`. El provider Docker y red privada funcionan sin archivos dinámicos IA que afecten otros hosts.

## 5. Arrancar Ollama y comprobar GPU — pc-prod

```bash
oc pull
oc up -d --wait --wait-timeout 120
oc ps
export OLLAMA_MODELO=qwen3:4b  # mismo modelo local elegido en servicio.env
oc exec -T ollama ollama pull "$OLLAMA_MODELO"
oc exec -T ollama ollama list
oc exec -T ollama ollama run "$OLLAMA_MODELO" 'Respondé únicamente: servicio disponible.'
oc exec -T ollama ollama ps
nvidia-smi
docker port "$(oc ps -q ollama)"  # vacío: sin publicación de 11434
```

Falta runtime/GPU: resolver, no quitar la reserva para simular éxito. Healthcheck comprueba API local, no inferencia GPU. Aceptar sólo respuesta real y PROCESSOR de `ollama ps`; medir VRAM/RAM y ajustar modelo/contexto si offload/OOM. CPU 2/RAM 8 GiB son iniciales; RAM no limita VRAM. `OLLAMA_NO_CLOUD=1` deshabilita cloud pero descargar modelos requiere salida a Internet. Volumen persistente `docendi-ai-modelos`; la red externa no se borra con `oc down`.

Las labels admiten sólo GET `/api/version`, GET `/api/tags`, POST `/api/chat` y POST `/api/generate`; otros métodos/rutas devuelven 404. Cadena: `ollama-tasa` (30/min, ráfaga 10, también frena fuerza bruta) → `ollama-auth` → `ollama-concurrencia` (4 en curso); excesos devuelven 429. BasicAuth elimina Authorization antes de upstream. Streaming con flush inmediato, **sin Buffering ni Retry**. No se conserva el antiguo límite 64 KiB: Traefik no limita body/tokens por ambiente en esta configuración. El read timeout privado es 30 s para recibir la petición; write timeout 0 permite streaming sin deadline total. No se registran access logs de ese entrypoint.

## 6. Cloudflare Tunnel y Access — operador, Cloudflare + pc-prod

1. Crear en `htpasswd` el usuario `staging` (sección 8, sin `-c`); `prod` queda sólo para la ruta local.
2. Zero Trust → Access → aplicación **self-hosted** `ia.<DOMINIO>`; anotar el **AUD**. Única política, acción **Service Auth**: _Include_ Service Token `vm-dev-ia` (vencimiento ≤ 1 año), _Require_ IP de egreso de vm-dev. Sin Allow/Bypass ni identidades. Guardar Client ID/Secret fuera de Git.
3. DNS: CNAME con proxy `ia` → `<UUID_PRINCIPAL>.cfargotunnel.com`.
4. WAF: rate limiting para el host `ia` y bloqueo de métodos distintos de GET/POST.
5. Materializar `/etc/cloudflared/config.yml` de pc-prod desde `config-principal.yml` (dominio, `teamName`, `audTag`) y en Proxmox la regla `ia` → 404. Validar con `ingress validate` / `ingress rule https://ia.<DOMINIO>` y reiniciar cada cloudflared en ventana aprobada.

No crear una regla `ia` en el túnel de Proxmox hacia Traefik, ni enrutar `ia` por `web`.

## 7. Comprobación desde Internet, vm-dev y prod

```bash
read -r -p 'Dominio: ' DOMINIO
export OLLAMA_URL="https://ia.${DOMINIO}"
# Desde cualquier origen: Access debe cortar antes del túnel (403 o redirección).
curl -sS -o /dev/null -w 'HTTP %{http_code}\n' "$OLLAMA_URL/api/version"
# Apex productivo no expone IA:
curl -sS -o /dev/null -w 'HTTP %{http_code}\n' "https://${DOMINIO}/api/version"  # 404
```

Desde vm-dev, con el Service Token en un archivo protegido de cabeceras (no en argumentos):

```bash
install -d -m 700 "$HOME/.config"
install -m 600 /dev/null "$HOME/.config/ia-access.headers"
# Formato de `curl -H @archivo`: una cabecera `Nombre: valor` por línea, sin comillas,
# comentarios ni líneas vacías; finales LF. El Client ID termina en `.access`.
read -r -p 'Client ID: ' CF_ID
read -r -s -p 'Client Secret: ' CF_SECRET; echo
printf 'CF-Access-Client-Id: %s\nCF-Access-Client-Secret: %s\n' "$CF_ID" "$CF_SECRET" > "$HOME/.config/ia-access.headers"
unset CF_ID CF_SECRET
cut -d: -f1 "$HOME/.config/ia-access.headers"  # sólo los dos nombres, sin mostrar valores
export OLLAMA_USUARIO=staging
export OLLAMA_MODELO=qwen3:4b  # mismo modelo instalado en pc-prod; Ollama responde 404 si falta o está vacío
curl -sS -H @"$HOME/.config/ia-access.headers" -o /dev/null -w 'HTTP %{http_code}\n' "$OLLAMA_URL/api/version"  # 401: falta BasicAuth
curl -fsS -H @"$HOME/.config/ia-access.headers" --user "$OLLAMA_USUARIO" "$OLLAMA_URL/api/version"  # 200
printf '{"model":"%s","prompt":"Respondé brevemente en español.","stream":true,"options":{"num_predict":32}}' "$OLLAMA_MODELO" | curl -fsS --no-buffer -H @"$HOME/.config/ia-access.headers" --user "$OLLAMA_USUARIO" --max-time 180 -H 'Content-Type: application/json' --data-binary @- "$OLLAMA_URL/api/generate"
curl -sS -H @"$HOME/.config/ia-access.headers" --user "$OLLAMA_USUARIO" -X POST -o /dev/null -w 'HTTP %{http_code}\n' "$OLLAMA_URL/api/pull"  # 404
```

El mismo token desde otra IP debe recibir 403. Nunca password o secreto en argumentos, `-v` o logs.

Sondas temporales en el namespace del consumidor real, sin modificar backend/frontend:

```bash
export CURL_IMAGEN=curlimages/curl@sha256:58adaa4e8dca9c988bae2aba4ab3434a0bb2da16bbe3f92dec39ec7785166777
# vm-dev (staging/pr-N): por el túnel, cabeceras montadas sólo lectura.
docker run --rm -it --user "$(id -u):$(id -g)" --network container:staging-backend-1 -v "$HOME/.config/ia-access.headers:/h:ro" "$CURL_IMAGEN" --fail -H @/h --user staging "$OLLAMA_URL/api/version"
# pc-prod (prod): ruta local por la red `traefik`, sin Service Token.
docker run --rm -it --network container:prod-backend-1 "$CURL_IMAGEN" --fail --user prod http://traefik:11435/api/version
```

Ajustar nombres de contenedor reales. Accesibilidad de infraestructura no acredita chatbot integrado; la entrega del Service Token y credenciales a staging/pr-N corresponde al change de integración.

## 8. Rotar/revocar usuarios y operar modelos

```bash
htpasswd -B -C 12 "$OLLAMA_AUTH_DIR/htpasswd" staging  # alta/rotación SIN -c
htpasswd -D "$OLLAMA_AUTH_DIR/htpasswd" pr-123  # sólo si existe ese usuario
chmod 600 "$OLLAMA_AUTH_DIR/htpasswd"
# EDITAR servicio.env: aumentar OLLAMA_AUTH_REVISION (1→2→3...), sin exportarla.
oc config --quiet
oc up -d --force-recreate --wait --wait-timeout 120 ollama
```

Traefik carga `usersFile` al crear el middleware; cambiar sólo el archivo **no garantiza revocación**. Montar carpeta evita inode viejo; aumentar revisión cambia realm/labels y reconstruye auth por evento Docker. Recrear sólo Ollama interrumpe brevemente IA, no Traefik ni aplicaciones; esperar reload y comprobar usuario válido/revocado. Mantener al menos un usuario: archivo vacío/ilegible invalida middleware y puede devolver 404 en lugar de 401, siempre fallo cerrado. No añadir usuarios a aplicaciones/GitHub ni automatizar pr-N.

CLI local `oc exec -T ollama ollama pull/list/rm` administra modelos. Defaults globales (una inferencia, un modelo, cola 4) no reservan capacidad ni priorizan prod. Traefik no inspecciona JSON ni impone allowlist de modelos/contexto. Uso no productivo se controla manualmente.

## 9. Actualización, parada y rollback

```bash
oc ps
oc logs --tail 50 ollama  # revisar/redactar antes de compartir
docker logs --tail 30 traefik
oc exec -T ollama ollama ps
# Guardar config previa; tras cambiar digest Ollama en servicio.env:
oc config --quiet
oc pull
oc up -d --wait --wait-timeout 120
oc down  # sin -v: modelos persisten, red externa/Traefik quedan intactos
```

Para rollback **sólo IA**, retirar la regla `ia` del ingress de pc-prod (o dejarla en `http_status:404`), deshabilitar la aplicación Access/Service Token y detener Ollama; router Docker desaparece sin parar Traefik. No borrar red externa ni volumen, no prune/reset/teardown de aplicaciones/DNS público/DB/storage. Reponer digest/config de Ollama para recuperar IA.

Si falla la **habilitación inicial de Traefik**, el operador detiene/elimina sólo el contenedor nuevo de Traefik, repone configuración estática anterior y renombra/inicia `traefik-antes-ollama`; preservar imagen/options/mounts anteriores y comprobar routing público. Nunca dejar ambos activos con mismos puertos. Rollback del proxy compartido requiere ventana; conservar backup hasta aceptación, no ejecutarlo en esta sesión. Cambios futuros de entrypoint/mounts/puertos también requieren recreación de Traefik; cambios de labels no.

### Diagnóstico

| Síntoma                       | Revisar                                                                                        |
| ----------------------------- | ---------------------------------------------------------------------------------------------- |
| Error NVIDIA                  | Driver/toolkit; no quitar reserva como solución.                                               |
| Error red externa inexistente | Crear red y conectar Traefik en pc-prod; no modificar redes de aplicaciones.                   |
| 404 en ruta permitida         | entrypoint, labels/provider, red correcta, archivo auth legible/no vacío y errores de Traefik. |
| 401 tras rotación             | Aumentar revisión, recrear Ollama y comprobar reload; no asumir watch del htpasswd.            |
| 502 / 504                     | IP/puerto upstream, red privada, recursos; no añadir retries de generación.                    |
| 403 con Service Token         | IP de egreso distinta a la política Access, token vencido/revocado o cabeceras ausentes.       |

## 10. Entrega al backend por GitHub

El backend consume la inferencia con las opciones `Inferencia__*`, que los workflows completan desde secrets de cada GitHub Environment ([matriz](github-pr-deploy.md#fuentes-de-variables-y-secretos)). Los archivos de cabeceras y las contraseñas por teclado de las secciones 7 y 8 quedan sólo para diagnóstico manual.

| Environment  | Usuario `htpasswd` | Secrets                                                                                           |
| ------------ | ------------------ | ------------------------------------------------------------------------------------------------- |
| `prod`       | `prod`             | `INFERENCIA_CONTRASENA_PROD`                                                                      |
| `staging`    | `staging`          | `INFERENCIA_CONTRASENA_STAGING`, `INFERENCIA_ACCESS_CLIENT_ID`, `INFERENCIA_ACCESS_CLIENT_SECRET` |
| `pr-preview` | `preview`          | `INFERENCIA_CONTRASENA_PREVIEW`, `INFERENCIA_ACCESS_CLIENT_ID`, `INFERENCIA_ACCESS_CLIENT_SECRET` |

1. En pc-prod, dar de alta `preview` con `htpasswd -B -C 12 "$OLLAMA_AUTH_DIR/htpasswd" preview` (sin `-c`), aumentar `OLLAMA_AUTH_REVISION` y recrear Ollama (sección 8).
2. Cargar cada secret en su Environment con `gh secret set <NOMBRE> --env <environment>` (lo pide por stdin; no pasar el valor como argumento). El Client ID del Service Token incluye el sufijo `.access`.
3. Desplegar y abrir el dashboard de sistema: la fila "Inferencia (IA)" debe mostrar **Disponible**. "No disponible" con prod correcto suele indicar Access (IP/token) o BasicAuth del usuario del ambiente; el log de Access y la sección 7 permiten aislarlo.

Para revocar previews sin afectar staging, eliminar `preview` del `htpasswd` (sección 8) o vaciar su secret. Revocar el Service Token corta staging y previews a la vez.

## Referencias y aceptación

Fuentes: [Ollama Docker](https://docs.ollama.com/docker), [FAQ](https://docs.ollama.com/faq), [Docker Debian](https://docs.docker.com/engine/install/debian/), [driver Debian](https://docs.nvidia.com/datacenter/tesla/driver-installation-guide/debian.html), [NVIDIA Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html), [Traefik BasicAuth](https://github.com/traefik/traefik/blob/v3.7.5/docs/content/reference/routing-configuration/http/middlewares/basicauth.md), [entrypoints](https://github.com/traefik/traefik/blob/v3.7.5/docs/content/reference/install-configuration/entrypoints.md), [RateLimit](https://github.com/traefik/traefik/blob/v3.7.5/docs/content/reference/routing-configuration/http/middlewares/ratelimit.md), [InFlightReq](https://github.com/traefik/traefik/blob/v3.7.5/docs/content/reference/routing-configuration/http/middlewares/inflightreq.md), [Access service tokens](https://developers.cloudflare.com/cloudflare-one/identity/service-tokens/), [cloudflared origin parameters](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/configure-tunnels/cloudflared-parameters/origin-parameters/).

Separar validación aislada de archivos/routing de acta real GPU/Access/túnel y descubrimiento Docker en pc-prod. Tarea 3.2 manual pendiente hasta esas comprobaciones; ningún resultado esperado documentado acredita ejecución.
