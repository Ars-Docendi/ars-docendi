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

| Método          | Path                                                                   | Rol         | Descripción                 |
| --------------- | ---------------------------------------------------------------------- | ----------- | --------------------------- |
| GET             | `/api/portal/ping`                                                     | (anónimo)   | Health check                |
| PUT             | `/api/portal/perfil/contacto`, `/cv`, `/habilidades`, `/intereses`     | autenticado | Actualización independiente |
| POST/PUT/DELETE | `/api/portal/perfil/{experiencia,educacion,certificaciones,proyectos}` | autenticado | CRUD de colecciones         |

## Lecturas, versiones y referencias documentales

El perfil completo se obtiene mediante seis SELECT split de EF bajo una
transacción `REPEATABLE READ`, limitada a PostgreSQL. La metadata de archivos se
resuelve fuera de ese snapshot en un lote de IDs mediante Storage.Contracts. No
se promete una transacción distribuida sobre bytes. El actor se resuelve por
cuenta concreta; las mutaciones obtienen la sección o el ítem pertinente, no el
perfil con todas sus colecciones.

Contacto, CV, experiencias, educaciones, certificaciones y proyectos exponen
`version` basada en `xmin`; las mutaciones de filas existentes exigen ese token.
Habilidades e intereses tienen contadores independientes en el perfil. Una
intención obsoleta responde `409 concurrency-conflict` y el frontend invalida la
lectura sin retry ciego. Las altas incidentales de perfil y términos usan UPSERT;
la unicidad incidental no permite pisar una sección concurrente.

La asociación de un archivo confirmado comparte la exclusión por archivo y la
transacción con Storage mediante el participante local. Reemplazar o eliminar
CV/documento confirma primero la desvinculación: si un pedido conserva una
referencia, conserva también los bytes y su descarga autorizada. Los fallos de
consulta de referencias impiden borrar. No se agrega una FK intermodular ni se
inventan bytes para una URI legacy.

Índices SQL incrementales por `perfil_id` cubren experiencias, educaciones,
certificaciones y proyectos. Las pruebas de rendimiento mantienen contenido y
cardinalidad, comparan planes/buffers y costos de escritura; sus tiempos no son
un SLA productivo.

## Reglas de negocio

Este módulo todavía no tiene reglas provenientes de normativa institucional registradas.

## Dependencias

- **Hacia adentro**: ninguna (es módulo fundacional de datos).
- **Hacia afuera**: no agrega edges; los contratos quedan disponibles para futuros consumidores.
- **Externas**: **Azure AD** (login institucional — el docente se autentica con sus credenciales UNLaM).

## Specs activas

_(autogenerable a futuro)_

## Decisiones registradas

- **Identidad desde Azure AD**: el ID del docente se vincula al `oid` (object ID) del token de Azure AD. NO se manejan credenciales propias.
- **PII sensible**: ver `data-model.md` para tratamiento de PII (encriptación, logs, backup).
- **Vocabulario de experticia por folksonomía**: no hay catálogo curado de antemano. Los docentes escriben el término y el autocompletado sobre lo ya cargado evita que se fragmente; `nombre_norm` (único) corta duplicados y `canonica_id` permite fusionar variantes sin perder datos. Ver `openspec/specs/experticia-docente/spec.md`.

## Qué de esto ve el asistente conversacional

Seis de las diez tablas —`perfiles`, `educaciones`, `certificaciones`,
`experiencias`, `habilidades` y `docente_habilidades`— se le conceden al asistente
con `GRANT` por columna y RLS propia. **`contactos` no se le concede a ningún rol**:
el teléfono y el mail personales que el docente carga acá no son consultables por el
asistente, y el contacto institucional ya sale de `identity`.

Quién puede consultar el perfil de otra persona lo decide `portal.ver_trayectoria_ajena`,
un permiso que **nace concedido a nadie**. Mientras siga así, cada actor ve
únicamente su propio perfil.

Estas policies **no protegen la API REST de este módulo**: la defensa de los
dieciocho endpoints sigue siendo `ServicioPortal.PersonaActualAsync` más el filtro
por dueño del repositorio. Cubren exclusivamente el camino del asistente, que entra
con roles de solo lectura que no son el dueño.

Las reglas de negocio con su fundamento normativo están en
[`business-rules/portal.md`](../../business-rules/portal.md).
