# Stack

## Apps y packages

| Capa          | Tecnología                                                  | Path                            |
| ------------- | ----------------------------------------------------------- | ------------------------------- |
| Backend       | C# — .NET 10 (ASP.NET Core Web API)                         | `backend/`                      |
| Frontend      | React 19 + TypeScript + Vite 8                              | `frontend/`                     |
| Base de datos | PostgreSQL 18                                               | (via docker-compose o VM)       |
| Autenticación | SSO Microsoft Azure AD (credenciales institucionales UNLaM) | integrado en backend + frontend |

## Backend (.NET 10)

Monolito modular en un único `ArsDocendi.slnx`. Cada módulo es un proyecto separado más un proyecto público `*.Contracts`.

| Proyecto                               | Rol                                                                                                                                                                  |
| -------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ArsDocendi.Host`                      | Composition root. Configura DI, autenticación, módulos, pipeline HTTP. Único proyecto que referencia todos los `Modules.X.Contracts` y los `Modules.X` para hosting. |
| `ArsDocendi.Shared`                    | Utilidades transversales (sin lógica de dominio) **más** la persistencia de los schemas `identity` y `audit` — única I/O admitida, ver invariante #4.                |
| `Modules.Designaciones` + `.Contracts` | Workflow de designaciones docentes + lectura de asignaciones via API Guaraní.                                                                                        |
| `Modules.Aulas` + `.Contracts`         | Pedidos y asignación de aulas/laboratorios para exámenes.                                                                                                            |
| `Modules.Portal` + `.Contracts`        | Datos personales del docente, horas, áreas de experticia.                                                                                                            |
| `Modules.Tareas` + `.Contracts`        | Tablero tipo Trello con semáforo de vencimiento para tareas internas.                                                                                                |

Restricciones (ver [`module-anatomy.md`](./module-anatomy.md)):

- `Modules.X` solo referencia `Shared` y `Modules.*.Contracts`. **Prohibido referenciar `Modules.X` internos entre sí**.
- Cada módulo expone `GET /api/{x}/ping` como smoke test.
- `*.Contracts` contiene solo DTOs, interfaces y tokens públicos. **Sin lógica**.

## Frontend (React 19 + Vite)

| Carpeta                           | Rol                                                                        |
| --------------------------------- | -------------------------------------------------------------------------- |
| `frontend/src/app/`               | Router (react-router-dom) + composición de páginas.                        |
| `frontend/src/shared/`            | API client (axios), auth (Azure AD), UI primitivos compartidos.            |
| `frontend/src/features/<modulo>/` | Una feature por módulo del backend (designaciones, aulas, portal, tareas). |

Restricciones:

- Las features **no se importan entre sí**. Lo común sube a `src/shared/`.
- Data fetching: **React Query** (`@tanstack/react-query`).
- HTTP: **axios** instance compartida en `src/shared/api/`.

## Base de datos

PostgreSQL 18. **Un schema por módulo** para mantener aislamiento de bounded contexts (`designaciones`, `aulas`, `portal`, `tareas`).

Migraciones: Entity Framework Core por módulo (a definir convención específica de migrations folder).

## Autenticación

SSO con Microsoft Azure AD. El backend valida tokens JWT emitidos por Azure AD. El frontend hace login redirigido al tenant institucional UNLaM. Los roles (Jefe de Cátedra, Coordinador, Secretaría Académica, Decanato, Administrativos, Docente) se mapean desde claims del token o desde tablas internas del módulo `Portal`.

## Decisiones registradas

- **Monolito modular vs microservicios**: monolito por escala del departamento (~6 roles, ~100s de docentes), por simplicidad operacional en VMs universitarias, y por evitar latencia de network entre módulos que coexisten en una misma defensa académica. Cuando crezca, puede romperse por módulo (los `.Contracts` ya son las fronteras).
- **.NET 10 vs versiones LTS previas**: por rolling release y features modernas. `global.json` con `latestFeature` roll-forward.
- **Vite vs Next.js**: SPA con backend separado simplifica deploy (estáticos + API), no requiere SSR para uso institucional interno.
- **PostgreSQL vs SQL Server**: open-source, mejor soporte en VMs Linux universitarias, no requiere licencias.
