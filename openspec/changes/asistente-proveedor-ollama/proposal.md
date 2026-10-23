## Why

Develop puso en producción un servicio de inferencia compartido: Ollama en la RTX 5070 de pc-prod, publicado como `ia.<DOMINIO>` detrás de Traefik y Cloudflare Access, con el modelo `qwen3.5:9b` (`servicio-ollama-compartido`), y un cliente mínimo en el proyecto `ArsDocendi.Inferencia` que sólo comprueba si el servicio responde (`integrar-cliente-inferencia`).

El asistente no puede usarlo:

1. **Otra API.** `ProveedorLocal` habla OpenAI (`/v1/chat/completions`, clave Bearer). Traefik sólo deja pasar la API nativa de Ollama (`/api/chat`, `/api/generate`, `/api/version`, `/api/tags`) con BasicAuth por ambiente, más el Service Token de Cloudflare fuera de prod.
2. **Otro cliente.** Las credenciales y el transporte del servicio viven en `ArsDocendi.Inferencia`, un proyecto que hoy no consume nadie más que el endpoint de estado. El asistente es el único consumidor previsto del modelo.
3. **Contexto chico.** El servicio arranca con `OLLAMA_CONTEXT_LENGTH=4096` y el prefijo estable del asistente ronda los 10–12k tokens: Ollama recortaría el prompt.

## What Changes

- **Adaptador `ProveedorOllama`** (`Asistente__Proveedor=ollama`) contra la API nativa (`POST /api/chat`) del servicio compartido:
  - prefijo estable como mensaje `system`, variable como `user`;
  - `think: false` salvo esfuerzo `alto`/`maximo`;
  - `format` con el JSON Schema cuando la llamada lo declara;
  - `options` con temperatura, techo de salida (`num_predict`) y contexto (`num_ctx`) configurado;
  - respuesta entera o por fragmentos NDJSON;
  - fallas traducidas al vocabulario del módulo, sin loguear el prompt ni credenciales.
- **`ArsDocendi.Inferencia` se integra a `Modules.Asistente`.** El cliente (URL, BasicAuth, Service Token, validación al arranque) y `GET /api/inferencia/estado` pasan al módulo sin cambiar configuración (`Inferencia:*`), ruta, permiso ni contrato. Desaparece el proyecto y su arista con el Host.
- **Redacción sin enmascarar con la GPU propia:** `ollama` cuenta como hardware del Departamento para `RedaccionSinEnmascarar`, igual que `local`, y el perfil la prende.
- **Perfil de deploy** `compose.asistente-ollama.yml`, que `spin-up.sh` suma con `ASISTENTE_PROVEEDOR=ollama` cuando el ambiente tiene `INFERENCIA_URL`; sin ella degrada a `simulado` igual que un proveedor real sin clave.
- **Preview a demanda:** la label `asistente-ollama` en un PR despliega su ambiente con el proveedor `ollama`.

## Capabilities

### New Capabilities

- `asistente-proveedor-ollama`: el asistente funciona contra el servicio de inferencia compartido (Ollama) con su API nativa.

### Modified Capabilities

- `cliente-inferencia`: el cliente vive en el módulo del asistente y lo usan tanto el estado como la generación; la configuración y el contrato del estado no cambian.

## Impact

- `backend/src/Modules.Asistente/`: `Infrastructure` (adaptador, cliente de inferencia), `Api` (estado de inferencia), `Configuracion`, `ModuleExtensions`.
- Se elimina `backend/src/ArsDocendi.Inferencia/`; cambian `ArsDocendi.Host` (csproj, `Program.cs`), `backend/ArsDocendi.slnx` y `backend/manifiesto-de-aristas.json`.
- `infra/ollama/{compose.yml,.env.example}` (contexto, keep-alive, flash attention, tasa por credencial), `infra/compose/compose.asistente-ollama.yml`, `infra/scripts/spin-up.sh`, `.github/workflows/pr-env-deploy.yml`.
- Docs: `backend/src/Modules.Asistente/README.md`, `docs/architecture/dependency-graph.md`, `docs/architecture/modelo-local.md`, `docs/operations/ollama-compartido.md`.
- Tests: adaptador contra un transporte falso, composición, estado de inferencia y `spin-up`.
- Sin cambios para `simulado`, `anthropic` ni `local`.
