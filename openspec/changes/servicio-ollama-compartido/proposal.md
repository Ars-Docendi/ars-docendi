# Proposal

## Why

Se necesita preparar una instancia local de Ollama que aproveche la RTX 5070 de pc-prod y sea alcanzable desde la red de prod y desde vm-dev para staging/pr-N. Este cambio prepara únicamente la infraestructura y su instalación; la integración del chatbot se tratará en otro cambio.

## What Changes

- Incorporar archivos declarativos de un proyecto Compose independiente `docendi-ai`: Ollama, GPU NVIDIA, volumen de modelos, red dedicada y política de reinicio.
- Reutilizar el Traefik existente con entrypoint privado, BasicAuth por archivo y routing Docker de Ollama restringido por método/ruta, sin agregar otro proxy ni desarrollar gateway.
- Documentar HTTPS privado con Tailscale Serve en pc-prod y el acceso existente vm-dev→VM subnet router→IP Tailscale pc-prod, sin instalar Tailscale en vm-dev ni contenedores.
- Incorporar un `.env.example` propio de la instancia, sin secretos, y un runbook completo de prerrequisitos, instalación, arranque, modelos, comprobación operativa, mantenimiento y rollback.
- No crear tests ni ejecutar suites de backend/frontend; realizar sólo validación de archivos/configuración y comprobaciones operativas documentadas.

## Capabilities

### New Capabilities

- `inferencia-local-compartida`: servicio de inferencia local persistente, privado y autenticado, con instalación y operación documentadas, independiente de las aplicaciones.

### Modified Capabilities

Ninguna. No cambian los requisitos ni archivos de despliegue de las aplicaciones o del pipeline existente.

## Impact

Lista permitida de archivos futuros:

- `infra/ollama/compose.yml`.
- `infra/traefik/traefik.yml`: agregar sólo entrypoint privado, conservando routing público/provider existentes.
- `infra/ollama/.env.example`.
- `docs/operations/ollama-compartido.md`, desarrollado a partir de `runbook.md`.
- Sólo un enlace documental desde `infra/README.md`, si corresponde.

No modificar `backend/**`, `frontend/**`, `infra/compose/compose.base.yml`, scripts de spin-up/teardown de ambientes, workflows, GitHub Environments/secrets, bases o storage. No desarrollar un gateway .NET, cliente IA, API del chatbot, UI, scheduler, CLI de credenciales ni helpers programados. No crear archivos de tests.

No hay cambios de schema, normativa institucional o grafo de dependencias. Driver NVIDIA/toolkit se instalan en pc-prod por el operador en una ventana de mantenimiento; la VM router y rutas existentes se reutilizan. Habilitar entrypoint/mount/puerto/red requiere recrear Traefik existente por el operador en ventana aprobada; no se reinicia desde apply. Los únicos secretos nuevos son credenciales operativas del proxy, administradas manualmente fuera del repositorio. No se automatiza su entrega a Docendi.

Rollback: retirar exclusivamente la publicación privada de IA y detener `docendi-ai` sin borrar modelos ni tocar las aplicaciones. Compartir GPU no garantiza prioridad ni capacidad reservada para producción; esas políticas no pertenecen a este alcance.
