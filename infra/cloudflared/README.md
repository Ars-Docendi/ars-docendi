# Cloudflare Tunnel — ingreso público

Único punto de entrada público de la plataforma. Un solo túnel con **un ingress
wildcard** (`*.<dominio> → cloudflared → Traefik`) sirve prod, staging y todos
los `pr-N` sin un public hostname por ambiente (D6).

## Archivos

| Archivo      | Rol                                                         |
| ------------ | ----------------------------------------------------------- |
| `config.yml` | Ingress wildcard + catch-all 404. Versionado, sin secretos. |

> Las credenciales del túnel (`credentials.json` / token) **nunca** van al repo.

## Crear el túnel (una vez, en el app host)

```bash
# 1. Autenticarse contra la cuenta de Cloudflare (abre el navegador).
cloudflared tunnel login

# 2. Crear el túnel. Genera ~/.cloudflared/<TUNNEL_ID>.json (las credenciales).
cloudflared tunnel create arsdocendi

# 3. Tomar el TUNNEL_ID que imprime y ponerlo en config.yml (campo `tunnel`).

# 4. Apuntar el wildcard DNS del dominio al túnel (CNAME *.<dominio> -> <ID>.cfargotunnel.com).
cloudflared tunnel route dns arsdocendi "*.<dominio>"
```

## Inyectar credenciales en runtime (nunca al repo)

Dos opciones, ambas fuera del repo:

- **credentials-file**: montar el `<TUNNEL_ID>.json` generado en `/etc/cloudflared/credentials.json` dentro del contenedor de `cloudflared` (volumen o secret del orquestador). `config.yml` ya apunta ahí.
- **Token**: alternativamente correr `cloudflared tunnel run --token <TOKEN>`, con el token desde un secret de CI / variable de entorno del host.

En CI el token/credenciales vienen de **GitHub Actions secrets** (ver `.env.example` raíz y el runbook). Verificar que no quedan en capas de imagen ni en logs.

## Reemplazar el dominio placeholder

`config.yml` usa `example.net` como placeholder. Al desplegar, reemplazar por el
dominio real (decisión del proyecto: el dominio real no se versiona; vive en el
host/runbook). El wildcard `*.<dominio>` debe coincidir con el `HOST_PUBLICO` que
arman los ambientes (`<AMBIENTE>.<dominio>`).

## Cloudflare Access (opcional, recomendado para staging/pr-N)

Para que los ambientes no-prod no queden abiertos a internet:

- Crear una **Access application** que cubra `staging.<dominio>` y `*.pr-*.<dominio>` (o el patrón que aplique).
- Política: permitir solo identidades del equipo / dominio institucional.
- Dejar `prod.<dominio>` público (o detrás de Access según política del cliente).

Access se configura en el panel de Cloudflare Zero Trust; no requiere cambios en
este repo.

## Verificación: el wildcard cubre todo sin hostname por PR

- `prod.<dominio>`, `staging.<dominio>` y `pr-<N>.<dominio>` resuelven todos por el mismo registro wildcard y el mismo ingress.
- Levantar un `pr-N` nuevo **no** toca Cloudflare: solo aparece un contenedor con su label de Host y Traefik empieza a rutearlo.
- Comprobar con: `dig pr-999.<dominio>` (resuelve al túnel) y `curl -I https://pr-999.<dominio>` una vez que el ambiente exista.
