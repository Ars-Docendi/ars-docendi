# Domain: Portal

## Propósito

**Portal de autogestión del docente**: el docente accede a sus datos personales, declara horas disponibles, mantiene áreas de experticia. Es la fuente canónica de información del docente que otros módulos consumen.

## Roles que interactúan

- **Docente** — Autogestiona sus datos.
- **Secretaría Académica** — Vista de todos los docentes, parametrización (áreas de experticia disponibles, etc.).

## Bounded context

- **Pertenece**: Docentes (entidad y sus atributos), áreas de experticia, horas disponibles declaradas.
- **No pertenece**: Designaciones (`Designaciones`), reservas de aulas (`Aulas`), asignaciones académicas reales (Guaraní).

## Entidades principales

| Entidad                     | Descripción                  | Schema/Tabla             | PII                                   |
| --------------------------- | ---------------------------- | ------------------------ | ------------------------------------- |
| `Docentes`                  | Datos personales del docente | `portal.Docentes`        | **Sí** — nombre, DNI, email, teléfono |
| `AreasExperticia`           | Catálogo de áreas            | `portal.AreasExperticia` | No                                    |
| `DocenteAreas`              | Relación docente ↔ áreas     | `portal.DocenteAreas`    | No                                    |
| _(otras a definir en spec)_ | ...                          | ...                      | ...                                   |

## API pública (contract)

| Interfaz              | Métodos                                                | Consumido por            |
| --------------------- | ------------------------------------------------------ | ------------------------ |
| `IPortalDocenteQuery` | `ObtenerDocentePorId`, `ExisteDocente`, `ObtenerAreas` | `Designaciones`, `Aulas` |

## Endpoints HTTP

| Método                    | Path               | Rol       | Descripción  |
| ------------------------- | ------------------ | --------- | ------------ |
| GET                       | `/api/portal/ping` | (anónimo) | Health check |
| _(a documentar en specs)_ | ...                | ...       | ...          |

## Reglas de negocio

Ver [`docs/business-rules/portal.md`](../../business-rules/portal.md) (a crear).

## Dependencias

- **Hacia adentro**: ninguna (es módulo fundacional de datos).
- **Hacia afuera**: `Designaciones` y `Aulas` consumen este módulo vía `Modules.Portal.Contracts`.
- **Externas**: **Azure AD** (login institucional — el docente se autentica con sus credenciales UNLaM).

## Specs activas

_(autogenerable a futuro)_

## Decisiones registradas

- **Identidad desde Azure AD**: el ID del docente se vincula al `oid` (object ID) del token de Azure AD. NO se manejan credenciales propias.
- **PII sensible**: ver `data-model.md` para tratamiento de PII (encriptación, logs, backup).
