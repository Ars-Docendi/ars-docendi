## Why

Hoy `identity.materias` pertenece directamente a una carrera (`carrera_id NOT NULL`, `UNIQUE(carrera_id, code)`) y no existe la noción de plan de estudios. Con los datos reales del SGA eso produce tres problemas:

- Una misma materia dictada en varias carreras (por ejemplo "Álgebra y Geometría Analítica II", código `01032`) se carga como filas distintas, lo que duplica opciones en los selectores de Docentes y Membresías sin forma de diferenciarlas.
- Las materias de planes activos pero no vigentes no tienen dónde encajar: aparecen como "faltantes" en la carga, aunque son válidas.
- Cualquier cambio de plan (nuevas materias, materias que salen) obliga a reescribir filas de materia en lugar de cambiar una pertenencia.

El modelo de SIU-Guaraní separa carrera (propuesta en Guaraní), plan, materia y su pertenencia a un plan. Adoptar ese corte mínimo para la materia resuelve los tres problemas; el plan y la pertenencia materia–plan se incorporan como catálogo **informativo** (qué materias dicta cada carrera y con qué vigencia), sin que ninguna regla de negocio dependa de ellos — ninguna regla implementada necesita esa granularidad: el ruteo al Coordinador solo precisa la carrera, y la autoridad de la designación solo precisa la materia canónica.

## What Changes

- La tabla **materias** pasa a ser canónica: una fila por materia, con código de 5 dígitos (con ceros a la izquierda), nombre y estado activa. Un código de materia es único en todo el sistema, sin importar en cuántas carreras se dicte.
- Nueva entidad **plan** asociada a una carrera (`carrera_id`), con código, nombre y banderas de vigencia/actividad. Nueva tabla de pertenencia **materia_plan** (materia ↔ plan), única por par `(plan, materia)`. Ambas son catálogo informativo: ninguna FK de negocio las referencia.
- **BREAKING**: `identity.materias` deja de tener `carrera_id` y su unicidad pasa a ser global por código. Como una materia ya no determina una única carrera, `identity.user_roles` (Docente), `designaciones.pedidos` y `designaciones.designaciones` agregan `carrera_id` explícito junto a `materia_id`: una materia compartida entre carreras exige elegir una al cargar la membresía o el trámite.
- Migración versionada que transforma los datos existentes de `identity.carreras`, `identity.materias`, `identity.user_roles`, `designaciones.pedidos` y `designaciones.designaciones` sin perder filas ni asignaciones.
- Actualización de `data-model.md` y `api-contracts.md` en el mismo diff.
- Fuera de alcance: versiones de plan, revisiones de materias, catálogos de estado y responsables académicas (se tratarán en un change posterior si una pantalla los necesita).

## Capabilities

### New Capabilities

- `modelo-academico`: planes, materias y su pertenencia a planes (`materia_plan`), incluida la vigencia y la unicidad del código de materia. Catálogo informativo, sin FK de negocio.

### Modified Capabilities

- `persistencia-identity`: `identity.materias` deja de estar atada a una carrera; `identity.user_roles` agrega `carrera_id` explícito para el Docente (junto a `materia_id`). Cambian los requisitos de unicidad y de integridad de ámbito de `user_roles`.
- `administracion-membresias-ambito`: una membresía de Docente manda `materia_id` y `carrera_id` juntos, validados contra el catálogo informativo materia–plan (que la materia se dicte, vigente, en esa carrera).
- `pedidos-designacion`: el pedido manda `materia_id` y `carrera_id` juntos, elegidos por quien lo carga. El alcance del Coordinador (BR-designaciones-009) sigue siendo la carrera del pedido.

## Impact

- **Base de datos (identity):** nuevas tablas `planes` y `materias_plan` (informativas); modificación de `materias` (sin `carrera_id`) y de `user_roles` (agrega `carrera_id` para Docente); migración versionada en `database/identity/`.
- **Designaciones:** `pedidos` y `designaciones` agregan `carrera_id` junto a la materia canónica.
- **Módulos consumidores:** Designaciones, Tareas (solo autorización por `user_roles`), Administración de Docentes y Usuarios (catálogos de materias y membresías).
- **Contratos:** `api-contracts.md` (catálogos de materias y carreras, membresías) y `data-model.md`.
- **Grafo de dependencias:** sin nuevas referencias entre módulos; los cambios quedan dentro de `Shared/identity` y `Modules.Designaciones`, que ya dependían de identity.
- **Rollback:** el Down vuelve a agregar `carrera_id` a `materias` y restaura `user_roles`/`pedidos`/`designaciones` a su forma anterior. No se conserva una tabla de respaldo porque la data original se regenera desde el SGA.
- **Datos reales:** la carga a partir del SGA resuelve materia y carrera directamente desde `materias_docente.csv`, sin heurística de desambiguación por plan. Los datos con PII no se versionan.
