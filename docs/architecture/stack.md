# Stack

## Apps y packages

| Capa          | Tecnología                                                            | Path                                      |
| ------------- | --------------------------------------------------------------------- | ----------------------------------------- |
| Backend       | C# — .NET 10 (ASP.NET Core Web API)                                   | `backend/`                                |
| Frontend      | React 19 + TypeScript + Vite 8                                        | `frontend/`                               |
| Base de datos | PostgreSQL 18                                                         | (via docker-compose o VM)                 |
| Autenticación | Microsoft Entra ID (OpenID Connect) con sesión por cookie del backend | backend (cliente confidencial) + frontend |

## Backend (.NET 10)

Monolito modular en un único `ArsDocendi.slnx`. Cada módulo es un proyecto separado más un proyecto público `*.Contracts`.

| Proyecto                               | Rol                                                                                                   |
| -------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| `ArsDocendi.Host`                      | Composition root: DI, autenticación, pipeline HTTP, administración de identidad y CLI de migraciones. |
| `ArsDocendi.Shared`                    | Utilidades transversales y persistencia de los schemas `identity` y `audit` (invariante #4).          |
| `ArsDocendi.Migraciones`               | Runner de SQL versionado embebido, diagnóstico de integridad y preview.                               |
| `ArsDocendi.Storage` + `.Contracts`    | Adjuntos sobre almacenamiento S3 (SeaweedFS) con antivirus (ClamAV).                                  |
| `ArsDocendi.Inferencia`                | Cliente del servicio Ollama compartido y su estado.                                                   |
| `Modules.Designaciones` + `.Contracts` | Pedidos de designación, circuito de aprobación, períodos y exportación de lotes.                      |
| `Modules.Aulas` + `.Contracts`         | Reserva de aulas para exámenes. Hoy sólo expone ping e infraestructura.                               |
| `Modules.Portal` + `.Contracts`        | Perfil del docente: contacto, CV, trayectoria y experticia.                                           |
| `Modules.Tareas` + `.Contracts`        | Tablero de tareas y proyectos internos con semáforo de vencimiento.                                   |

Restricciones (ver [`module-anatomy.md`](./module-anatomy.md)):

- `Modules.X` solo referencia `Shared` y `Modules.*.Contracts`. **Prohibido referenciar `Modules.X` internos entre sí**.
- Cada módulo expone `GET /api/{x}/ping` como smoke test.
- `*.Contracts` contiene solo DTOs, interfaces y tokens públicos. **Sin lógica**.

## Frontend (React 19 + Vite)

| Carpeta                            | Rol                                                                                 |
| ---------------------------------- | ----------------------------------------------------------------------------------- |
| `frontend/src/app/`                | Router (react-router-dom) y layout.                                                 |
| `frontend/src/shared/`             | Cliente HTTP (axios), sesión y guards de permisos, UI compartida.                   |
| `frontend/src/features/<feature>/` | Una carpeta por área funcional (designaciones, portal, tareas, usuarios, roles, …). |

Componentes base: `@ars-docendi/ui` (repositorio `Ars-Docendi/ui-lib`).

Restricciones:

- Las features **no se importan entre sí**. Lo común sube a `src/shared/`.
- Data fetching: **React Query** (`@tanstack/react-query`).
- HTTP: **axios** instance compartida en `src/shared/api/`.

## Base de datos

PostgreSQL 18. **Un schema por contexto** (`identity`, `audit`, `storage`, `designaciones`, `aulas`, `portal`, `tareas`).

Migraciones: SQL versionado embebido y aplicado por `ArsDocendi.Migraciones`; EF Core sólo mapea. Ver [data-model.md](./data-model.md) y [database/README.md](../../database/README.md).

## Autenticación

Ingreso con cuentas Microsoft (Microsoft Entra ID) resuelto por el backend: el Host hace el intercambio OpenID Connect como cliente confidencial (código + PKCE) y emite una cookie de sesión propia; el frontend nunca maneja tokens de Microsoft. El ingreso sólo verifica usuarios dados de alta por la administración. El rol y los permisos no vienen del token: se leen de `identity` en cada solicitud. La registración de la app es configuración (`AutenticacionMicrosoft`), así que puede reemplazarse por una institucional de UNLaM sin cambiar código. Ver [`api-contracts.md`](./api-contracts.md#autenticación-y-autorización).

## Decisiones registradas

- **Monolito modular vs microservicios**: monolito por escala del departamento (~6 roles, ~100s de docentes), por simplicidad operacional en VMs universitarias, y por evitar latencia de network entre módulos que coexisten en una misma defensa académica. Cuando crezca, puede romperse por módulo (los `.Contracts` ya son las fronteras).
- **.NET 10**: versión LTS vigente. `global.json` fija el SDK con roll-forward `latestFeature`.
- **Vite vs Next.js**: SPA con backend separado simplifica deploy (estáticos + API), no requiere SSR para uso institucional interno.
- **PostgreSQL vs SQL Server**: open-source, mejor soporte en VMs Linux universitarias, no requiere licencias.
