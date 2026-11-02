# Domain: Portal

## Propósito

**Portal de autogestión del docente**: el docente consulta identidad institucional de solo lectura y mantiene contacto, CV, trayectoria, formación, certificaciones, proyectos, habilidades e intereses.

## Roles que interactúan

- **Docente** — Autogestiona sus datos.
- **Secretaría Académica** — Ve el perfil de cualquier docente desde Personas (pestaña "Datos docentes") y cura el vocabulario de habilidades e intereses (`portal.vocabulario.administrar`).

## Bounded context

- **Pertenece**: perfil del docente (contacto, CV, experiencia, educación, certificaciones, proyectos) y habilidades/intereses.
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

| Interfaz         | Métodos              | Consumido por                                                 |
| ---------------- | -------------------- | ------------------------------------------------------------- |
| `IPortalQueries` | `ObtenerPerfilAsync` | `Modules.Designaciones` (correo del perfil al exportar altas) |

## Endpoints HTTP

| Método          | Path                                                                   | Rol                              | Descripción                                                                            |
| --------------- | ---------------------------------------------------------------------- | -------------------------------- | -------------------------------------------------------------------------------------- |
| GET             | `/api/portal/ping`                                                     | (anónimo)                        | Health check                                                                           |
| PUT             | `/api/portal/perfil/contacto`, `/cv`, `/habilidades`, `/intereses`     | autenticado                      | Actualización independiente                                                            |
| POST/PUT/DELETE | `/api/portal/perfil/{experiencia,educacion,certificaciones,proyectos}` | autenticado                      | CRUD de colecciones                                                                    |
| GET             | `/api/portal/vocabulario`                                              | autenticado                      | Términos incorporados y no fusionados, para elegir                                     |
| GET             | `/api/portal/vocabulario/terminos`                                     | `portal.vocabulario.administrar` | Todos los términos con estado, canónico y cantidad de docentes                         |
| POST            | `/api/portal/vocabulario/terminos/{id}/incorporar`                     | `portal.vocabulario.administrar` | Incorporar un término sugerido                                                         |
| PUT             | `/api/portal/vocabulario/terminos/{id}`                                | `portal.vocabulario.administrar` | Renombrar; 409 `termino-repetido` si ya existe                                         |
| POST            | `/api/portal/vocabulario/terminos/{id}/fusionar`                       | `portal.vocabulario.administrar` | Fusionar en `destinoId`; mueve los usos y sube las versiones de los perfiles afectados |

Mi Portal también muestra las designaciones vigentes del docente; las lee desde
`GET /api/designaciones/vigentes/mias` (ver [designaciones.md](./designaciones.md)), sin que
`Modules.Portal` dependa de Designaciones.

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

Ver [docs/business-rules/portal.md](../../business-rules/portal.md) (`BR-portal-001` a `005`).

## Dependencias

- **Hacia adentro**: `ArsDocendi.Storage.Contracts` (CV y documentos de proyectos).
- **Hacia afuera**: `Modules.Designaciones` consume `IPortalQueries` (correo del perfil para exportar altas).
- **Externas**: ninguna. El ingreso con Microsoft lo resuelve el Host; ver [api-contracts.md](../api-contracts.md#autenticación-y-autorización).

## Decisiones registradas

- **PII sensible**: ver `data-model.md` para tratamiento de PII (encriptación, logs, backup).
- **Vocabulario de experticia curado desde el uso**: la migración `VocabularioExperticiaPortal` siembra un vocabulario base y los docentes pueden sugerir términos nuevos, que quedan `sugerido` hasta que la Secretaría los incorpora. `termino_norm` (único) corta duplicados y `canonica_id` registra una fusión: guardar la variante resuelve al canónico. Ver `openspec/specs/experticia-docente/spec.md` y `openspec/changes/archive/2026-11-02-catalogo-aulas-y-vocabulario-portal/`.
