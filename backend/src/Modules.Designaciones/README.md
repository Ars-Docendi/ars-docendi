# Modules.Designaciones

Implementa **RF-01 — Gestión de Proyecto Docente**: workflow de designaciones y visualización de asignaciones (integración API Guaraní).

Ver [dominio Designaciones](../../../docs/architecture/domains/designaciones.md).

## Endpoints

- `GET /api/designaciones/ping` — smoke test.
- `GET /api/designaciones/comisiones/periodos` y `GET /api/designaciones/comisiones?periodoLectivoId=…` — comisiones importadas de Guaraní (`comisiones.ver`).
- `GET /api/designaciones/comisiones/guarani/periodos-lectivos` y `POST /api/designaciones/comisiones/importaciones` — importación desde Guaraní (`comisiones.importar`).

## Configuración

Sección `Guarani` (`UrlBase`, `Usuario`, `Contrasena`, `RutaPeriodosLectivos`,
`RutaComisiones` con `{periodoLectivo}`, `TimeoutSegundos`). Sin configuración
completa la importación responde 503 y el resto del módulo funciona igual. En
local: `dotnet user-secrets set "Guarani:Contrasena" "…" --project src/ArsDocendi.Host`.

## Schema PostgreSQL

`designaciones`
