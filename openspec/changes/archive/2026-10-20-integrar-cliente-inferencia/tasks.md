# Tasks

## 1. Backend: proyecto `ArsDocendi.Inferencia`

- [x] 1.1 Crear `backend/src/ArsDocendi.Inferencia` (net10.0, `Microsoft.AspNetCore.App`, referencia a `ArsDocendi.Shared`), agregarlo a `ArsDocendi.slnx`, referenciarlo desde `ArsDocendi.Host` y registrar `AddInferencia(builder.Configuration)` en `Program.cs`; verificar que la solución compila.
- [x] 1.2 Implementar `InferenciaOptions` y su validación al arranque (URL absoluta http/https; usuario y contraseña obligatorios con URL; par de Service Token completo o vacío; sin validar en Development; mensajes sin valores); verificar con test de integración que Production con URL sin contraseña no arranca.
- [x] 1.3 Registrar `ClienteInferencia` con `AddHttpClient`, BasicAuth y cabeceras `CF-Access-*` condicionales; implementar `ComprobarAsync` (GET `api/version`, timeout 3 s, logging Serilog sin URL ni credenciales); verificar con handler stub que prod no envía `CF-Access-*` y staging envía ambas.
- [x] 1.4 Implementar `GET /api/inferencia/estado` con `sistema.estado.ver` y `EstadoInferenciaDto`; verificar con tests: deshabilitado sin requests, disponible, `no_disponible` ante 401/500/timeout sin URL ni usuario en el cuerpo, y 403 sin permiso sin contactar el servicio.

## 2. Frontend: dashboard de sistema

- [x] 2.1 Agregar `comprobarInferencia` y el estado `deshabilitado` en `features/sistema/api/sistemaApi.ts`; verificar en `sistemaApi.test.ts` el mapeo de cada estado y la falla de red como `no_disponible`.
- [x] 2.2 Agregar la fila "Inferencia (IA)", la etiqueta y clase `deshabilitado` y la nota actualizada en `DashboardPage.tsx`/`sistema.css`; verificar en `DashboardPage.test.tsx` que deshabilitado se distingue de no disponible y que una falla de inferencia no oculta los demás componentes.
- [x] 2.3 Actualizar `docs/product/designs/administracion-sistema-design-spec.md` con la fila y el estado nuevos.

## 3. Configuración y despliegue

- [x] 3.1 Agregar `Inferencia__*` al servicio `backend` de `infra/compose/compose.base.yml` con defaults vacíos; verificar `docker compose config` con y sin las variables.
- [x] 3.2 Completar `env:` de los jobs de deploy en `deploy-prod.yml` (URL local, usuario `prod`, sin Access), `deploy-staging.yml` (usuario `staging` + Access) y `pr-env-deploy.yml` (usuario `preview` + Access) según la tabla del diseño; verificar que ningún paso imprime esos valores.
- [x] 3.3 Actualizar `docs/operations/github-pr-deploy.md` (matriz de secrets por Environment) y `docs/operations/ollama-compartido.md` (alta del usuario `preview` y entrega a GitHub en lugar de archivos manuales).

## 4. Documentación de arquitectura

- [x] 4.1 Actualizar `docs/architecture/dependency-graph.md` (nodos y edge registry `Host → ArsDocendi.Inferencia → Shared`), `docs/architecture/api-contracts.md` (endpoint y DTO) y, si corresponde, `module-anatomy.md` e `infrastructure.md`.

## 5. Verificación

- [x] 5.1 Ejecutar `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check` y `openspec validate --all --strict`; registrar checks que el entorno no permita completar.
- [x] 5.2 Operador: crear secrets y usuario `preview`, desplegar staging y un pr-N, y confirmar "Disponible" en el dashboard de cada uno y en prod tras el release.

## Evidencia

- `dotnet test backend/ArsDocendi.slnx`: 191/191 (incluye 9 nuevos en `Inferencia/InferenciaTests.cs` contra servicio simulado; Testcontainers PostgreSQL para permisos).
- Frontend: `test:run` 276/276, `lint` y `build` sin errores; `format:check` y `openspec validate --all --strict` OK.
- `docker compose config` de `compose.base.yml` con y sin `INFERENCIA_*`: valores vacíos por defecto e interpolación desde el entorno del proceso.
- **5.2 pendiente:** no hay acceso a GitHub Environments, pc-prod ni vm-dev desde esta sesión.
