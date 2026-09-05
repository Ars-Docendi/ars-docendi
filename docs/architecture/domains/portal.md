# Domain: Portal

## Propósito

**Portal de autogestión del docente**: el docente consulta identidad institucional de solo lectura y mantiene contacto, CV, trayectoria, formación, certificaciones, proyectos, habilidades e intereses.

## Roles que interactúan

- **Docente** — Autogestiona sus datos.
- **Secretaría Académica** — Vista de todos los docentes, parametrización (áreas de experticia disponibles, etc.).

## Bounded context

- **Pertenece**: Docentes (entidad y sus atributos), áreas de experticia, horas disponibles declaradas.
- **No pertenece**: Designaciones (`Designaciones`), reservas de aulas (`Aulas`), asignaciones académicas reales (Guaraní).

## Entidades principales

| Entidad                                                       | Descripción                                                 | Schema/Tabla                     | PII      |
| ------------------------------------------------------------- | ----------------------------------------------------------- | -------------------------------- | -------- |
| `Perfiles`                                                    | Raíz vinculada a `identity.personas` por `persona_id`       | `portal.perfiles`                | No       |
| `Contactos`, `Cvs`                                            | Datos editables y metadata del CV, sin bytes                | `portal.contactos`, `portal.cvs` | Contacto |
| `Experiencias`, `Educaciones`, `Certificaciones`, `Proyectos` | Colecciones informativas con períodos opcionales            | `portal.*`                       | No       |
| `Habilidades`                                                 | Vocabulario normalizado, con términos sugeridos             | `portal.habilidades`             | No       |
| `DocenteHabilidades`                                          | Relación discriminada por `tipo` (`habilidad` \| `interes`) | `portal.docente_habilidades`     | No       |

## API pública (contract)

| Interfaz         | Métodos              | Consumido por                   |
| ---------------- | -------------------- | ------------------------------- |
| `IPortalQueries` | `ObtenerPerfilAsync` | Futuros consumidores de lectura |

## Endpoints HTTP

| Método          | Path                                                               | Rol         | Descripción                 |
| --------------- | ------------------------------------------------------------------ | ----------- | --------------------------- | ----------- | ----------- | ------------------- |
| GET             | `/api/portal/ping`                                                 | (anónimo)   | Health check                |
| PUT             | `/api/portal/perfil/contacto`, `/cv`, `/habilidades`, `/intereses` | autenticado | Actualización independiente |
| POST/PUT/DELETE | `/api/portal/perfil/{experiencia                                   | educacion   | certificaciones             | proyectos}` | autenticado | CRUD de colecciones |

## Reglas de negocio

Ver [`docs/business-rules/portal.md`](../../business-rules/portal.md) (a crear).

## Dependencias

- **Hacia adentro**: ninguna (es módulo fundacional de datos).
- **Hacia afuera**: no agrega edges; los contratos quedan disponibles para futuros consumidores.
- **Externas**: **Azure AD** (login institucional — el docente se autentica con sus credenciales UNLaM).

## Specs activas

_(autogenerable a futuro)_

## Decisiones registradas

- **Identidad desde Azure AD**: el ID del docente se vincula al `oid` (object ID) del token de Azure AD. NO se manejan credenciales propias.
- **PII sensible**: ver `data-model.md` para tratamiento de PII (encriptación, logs, backup).
- **Vocabulario de experticia por folksonomía**: no hay catálogo curado de antemano. Los docentes escriben el término y el autocompletado sobre lo ya cargado evita que se fragmente; `nombre_norm` (único) corta duplicados y `canonica_id` permite fusionar variantes sin perder datos. Decidido el 2026-09-03 — ver D13 en `openspec/changes/portal-docente-perfil/design.md`.
