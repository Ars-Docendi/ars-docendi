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

| Entidad                     | Descripción                                                                                                                                  | Schema/Tabla                | PII                                   |
| --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------- | ------------------------------------- |
| `Docentes`                  | Datos personales del docente                                                                                                                 | `portal.Docentes`           | **Sí** — nombre, DNI, email, teléfono |
| `Habilidades`               | Términos de habilidad e interés, creados por uso (folksonomía): `nombre`, `nombre_norm` único, `usos`, `canonica_id` para fusionar variantes | `portal.Habilidades`        | No                                    |
| `DocenteHabilidades`        | Relación docente ↔ término, discriminada por `tipo` (`habilidad` \| `interes`)                                                               | `portal.DocenteHabilidades` | No                                    |
| _(otras a definir en spec)_ | ...                                                                                                                                          | ...                         | ...                                   |

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
- **Vocabulario de experticia por folksonomía**: no hay catálogo curado de antemano. Los docentes escriben el término y el autocompletado sobre lo ya cargado evita que se fragmente; `nombre_norm` (único) corta duplicados y `canonica_id` permite fusionar variantes sin perder datos. Decidido el 2026-09-03 — ver D13 en `openspec/changes/portal-docente-perfil/design.md`.
