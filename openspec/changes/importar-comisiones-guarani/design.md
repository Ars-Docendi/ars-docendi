## Context

Motivación y alcance: ver `proposal.md`. Comportamiento: ver `specs/`.

Estado actual relevante:

- `Modules.Designaciones` ya declara la API Guaraní como dependencia externa "TBD" y registra la decisión "NO replicar esa data en nuestra DB" (`docs/architecture/domains/designaciones.md`). Este cambio la revisa.
- No hay ningún cliente HTTP saliente en el backend (`AddHttpClient` no se usa); las integraciones externas existentes (Microsoft, SeaweedFS) se configuran con secciones de `appsettings` y secretos inyectados por ambiente (`AutenticacionMicrosoft:ClientSecret`, `Almacenamiento:*`).
- El DDL vive en `database/<schema>/`; sobre los baselines de `20261005…`, cada cambio es una migración incremental `<timestamp>_<Nombre><Contexto>` creada con `node scripts/migrations/scaffold.mjs <Contexto> <Nombre>`, que genera el `.sql` y el wrapper EF con `[RecursosMigracionSql(...)]` + `AplicarRecursosSql`. Nunca se edita un baseline ni un recurso publicado. Todas las entidades usan `ExcludeFromMigrations()`.
- Los permisos son filas de `identity.permisos` + `identity.rol_permisos` insertadas por SQL versionado (último id: `b2000000-…-025`). Los controllers usan `[Authorize(Policy = Permisos.X)]`; el frontend usa `RequirePermission` y el registro `frontend/src/app/shell/nav.ts`.
- `identity.materias`/`carreras` son el catálogo propio. `identity.personas.documento` es `UNIQUE NOT NULL` y es la clave natural de una persona; `carreras.code` es único y `materias.code` es único en todo el catálogo (materia canónica de 5 dígitos, sin carrera; la relación con carreras vive en `planes`/`materias_plan`, como catálogo informativo). `IConsultasIdentity` no tiene hoy ninguna búsqueda por documento.
- En la rama `feature/asistente-modelo-local` (no integrada) existe `Modules.Asistente`, un asistente **text-to-SQL** que ejecuta SQL generado por un modelo directamente contra `identity` y `designaciones`, con un rol de sólo lectura, `GRANT` por columna declarados en `database/asistente/manifiesto-privilegios.json` (un test falla ante cualquier tabla sin clasificar), policies RLS que conjuntan el permiso de dominio y `COMMENT ON` que describen el schema al modelo. Esa rama ya usa los ids de permiso `b2000000-…-026` a `-032` y los archivos `database/identity/014` a `020`.

## Goals / Non-Goals

**Goals:**

- Un snapshot local de comisiones por período lectivo, reemplazable de forma atómica y legible sin Guaraní.
- Aislar todo el conocimiento del contrato de Guaraní en un único cliente, testeable sin red.
- Que una importación concurrente o interrumpida nunca deje un período vacío ni a medio cargar.

**Non-Goals:**

- Sincronización automática o programada (la importación es manual).
- Vínculo manual de docentes o carreras desde la UI: todo vínculo se deriva en la importación (ver decisión 3).
- Editar comisiones desde Ars Docendi o escribir algo en Guaraní.
- Acotar la visibilidad de comisiones por ámbito: es información institucional del departamento, visible completa para quien tenga `comisiones.ver`.
- Filtrar por carrera o departamento al importar: la cuenta técnica de Guaraní ya devuelve sólo lo de Ingeniería.
- Historial de versiones de comisiones: sólo se conserva el último snapshot por período (el registro de importaciones sí es histórico).

## Decisions

### 1. Vive en `Modules.Designaciones`

El dominio ya es dueño de la integración con Guaraní y quienes consumen la pantalla son los mismos actores de designaciones. Alternativas descartadas: `Modules.Aulas` (su alcance registrado es sólo exámenes; el aula de la comisión es un texto informativo) y un módulo nuevo (proyectos, ping, contracts y migrador para dos tablas y una pantalla). No se agrega nada a `Modules.Designaciones.Contracts`: ningún otro módulo consume comisiones hoy.

### 2. Snapshot normalizado: comisión + tres tablas hijas

Los multivalores van en tablas hijas, no en JSONB ni en texto agrupado. El motivo es el asistente: una pregunta como "¿en qué comisiones da clase Pérez?" o "¿qué comisiones hay los lunes a la noche?" tiene que resolverse con un `JOIN` simple que el modelo genera de forma confiable. Con `jsonb_array_elements` o con un campo "Pérez, Juan / Gómez, Ana" el modelo falla o tiene que comparar nombres, y comparar nombres falla con homónimos, tildes y segundos nombres.

| Tabla                                 | Columnas                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `designaciones.sga_comisiones`        | `comision_guarani_id text` **PK**, `importacion_id uuid` FK, `anio_academico int`, `periodo_lectivo_guarani_id text`, `periodo_lectivo_nombre text`, `nombre`, `materia_codigo`, `materia_nombre` (no nulos), `materia_id uuid NULL` FK, `modalidad_codigo`, `modalidad` (descripción), `ubicacion` (nullable), `created_at`                                                                                                                                                                                   |
| `designaciones.sga_comision_carreras` | `comision_guarani_id` FK `ON DELETE CASCADE`, `carrera_codigo`, `carrera_nombre`, `carrera_id uuid NULL` FK; PK `(comision_guarani_id, carrera_codigo)`                                                                                                                                                                                                                                                                                                                                                        |
| `designaciones.sga_comision_docentes` | `comision_guarani_id` FK `ON DELETE CASCADE`, `orden smallint`, `nombre`, `responsabilidad` (nullable, tal como se carga en Guaraní), `persona_id uuid NULL` FK; PK `(comision_guarani_id, orden)`; índice por `persona_id`                                                                                                                                                                                                                                                                                    |
| `designaciones.sga_comision_horarios` | `comision_guarani_id` FK `ON DELETE CASCADE`, `dia smallint` (1–7, `CHECK`), `desde time`, `hasta time` (`CHECK desde < hasta`), `aula` (nullable; en Guaraní el espacio se asigna por banda); `asignacion_guarani_id text`; PK `(comision_guarani_id, asignacion_guarani_id)`. La asignación es la banda + aula de Guaraní: una misma asignación puede compartirse entre comisiones (aparece en cada una con el mismo id) y una comisión puede tener dos asignaciones en el mismo horario con aulas distintas |

- **PK = id de Guaraní.** Es la PK de la comisión en Guaraní y no cambia entre reimportaciones. Con un `uuid` propio, cada importación generaría ids nuevos y rompería cualquier referencia externa (por ejemplo, vínculos del asistente o un historial de conversación que apunta a una comisión). Al pertenecer a un único período en Guaraní, también es única entre períodos.
- **FKs nullable hacia `identity` con `ON DELETE SET NULL`** (`persona_id` → `identity.personas`, `carrera_id` → `identity.carreras`, `materia_id` → `identity.materias`). Es el mismo patrón que `designaciones.pedidos` y `designaciones.designaciones`, que ya referencian `identity` por FK. `NULL` significa "no existe en el sistema". Se usa `SET NULL` y no el `RESTRICT` de esas tablas porque el snapshot se puede regenerar: nunca debe impedir una operación de administración sobre una persona o un catálogo, y la próxima importación recalcula el vínculo.
- **Sin documento persistido**: se recibe de Guaraní, se usa en memoria para vincular y se descarta.
- **El snapshot no exige nada de `identity`**: una comisión se importa completa aunque ningún docente, carrera ni materia exista en el sistema. Las FKs sólo garantizan que un vínculo presente apunte a algo real.

Alternativa descartada: JSONB en la fila de la comisión (la propuesta anterior de este design). Era suficiente para la pantalla, pero deja al asistente sin poder unir docentes con personas.

### 3. Vínculos derivados en cada importación

En la fase de lectura (decisión 5), después de validar:

- **Docente → persona**: `IConsultasIdentity.BuscarPersonasPorDocumentosAsync(documentos)` devuelve `documento → personaId` en una sola consulta. Los documentos se normalizan (sin puntos ni espacios) antes de buscar.
- **Carrera**: `carrera_id` por `identity.carreras.code = carrera_codigo`.
- **Materia**: `materia_id` por `identity.materias.code = materia_codigo`. La materia es canónica (código único de 5 dígitos, sin carrera), por lo que el vínculo es de la comisión y no depende de la carrera. Esto permite cruzar comisiones con designaciones por `materia_id`. En la muestra del período 152, las 368 materias y las 9 carreras coinciden con el catálogo sembrado.

Lo que no se encuentra queda en `NULL` y se muestra igual. El registro de importaciones guarda `docentes_sin_vincular` y `carreras_sin_vincular`.

**Por qué no hay pérdida de datos**: todo el contenido de las cuatro tablas se puede derivar de Guaraní + `identity` al momento de importar. El `DELETE … CASCADE` del reemplazo sólo borra lo que la misma transacción vuelve a calcular. Se pierde un dato sólo si algo de esas tablas no se puede recalcular, por eso no hay vínculo manual. Para corregir un docente sin vincular se da de alta o se corrige la persona en el sistema y se reimporta. Si algún día hiciera falta un vínculo manual, irá en una tabla propia guardada por identificador de Guaraní, fuera del snapshot.

Límite aceptado: una persona dada de alta en `identity` después de la importación queda sin vincular hasta la siguiente importación del período.

`BuscarPersonasPorDocumentosAsync` es una lectura nueva en `ArsDocendi.Shared` (dentro de la regla 4 de AGENTS.md). Las carreras y materias se resuelven con `ListarMateriasAsync` y una lectura equivalente de carreras, ya existentes o agregadas a la misma interfaz. Ningún consumidor existente cambia.

### 4. Registro de importaciones como autoridad de concurrencia

`designaciones.sga_importaciones`: `id`, `anio_academico`, `periodo_lectivo_guarani_id`, `periodo_lectivo_nombre`, `usuario_id` (FK nullable a `identity.users`), `iniciada_at`, `finalizada_at`, `estado` (`en_curso` | `exitosa` | `fallida`), `cantidad`, `docentes_sin_vincular`, `carreras_sin_vincular`, `causa` (código genérico: `timeout`, `autenticacion`, `respuesta_invalida`, `sin_comisiones`, `no_disponible`, `interrumpida`), `created_at`.

Índice único parcial `(periodo_lectivo_guarani_id) WHERE estado = 'en_curso'`: la base decide quién importa, igual que BR-designaciones-001. Alternativa descartada: `pg_advisory_lock`, que no sobrevive entre la fase de lectura a Guaraní y la de escritura sin retener una conexión durante toda la llamada externa.

### 5. Flujo de importación en tres fases

1. **Reservar** (transacción corta): si existe un `en_curso` del período con `iniciada_at` más antigua que `2 × TimeoutSegundos`, marcarlo `fallida/interrumpida`; luego insertar el nuevo `en_curso`. Violación del índice único → 409.
2. **Leer, agrupar, validar y vincular** (sin transacción abierta): pedir todas las páginas a Guaraní; agrupar las filas planas por `comision` y, dentro de cada grupo, armar los conjuntos distintos de carreras (por código), docentes (por documento) y bandas (por `asignacion`), descartando los nulos que produce el `LEFT JOIN`; validar (los campos propios de la comisión —nombre, materia, ubicación, modalidad— deben ser iguales en todas sus filas, nombre y materia no vacíos, día reconocido, horas válidas, al menos una comisión) y resolver los vínculos de la decisión 3. Cualquier fallo → fase 3b.
3. **Aplicar** (una transacción): `DELETE FROM sga_comisiones WHERE periodo_lectivo_guarani_id = $1` (las hijas caen por cascada), inserción en lote de las cuatro tablas, `UPDATE` del registro a `exitosa` con los contadores. **3b**: `UPDATE` a `fallida` con `causa`, sin tocar el snapshot.

Si una comisión del lote ya existe con otro período (Guaraní la movió), la inserción viola la PK. Se trata como `respuesta_invalida` y no se reemplaza nada: es una inconsistencia que se tiene que ver, no corregir en silencio.

La llamada externa no ocurre dentro de la transacción de escritura, para no retener locks mientras Guaraní responde. El endpoint es síncrono: una importación manual de cientos de comisiones entra en un request con timeout configurable. Alternativa descartada por ahora: job en background con polling, que agrega estado y UI sin necesidad demostrada.

### 6. Cliente concreto `ClienteGuarani` con `HttpClient` tipado

Registrado con `services.AddHttpClient<ClienteGuarani>()`, configurado desde `IOptions<OpcionesGuarani>` (`UrlBase`, `Usuario`, `Contrasena`, `TimeoutSegundos`, default 60). Autenticación HTTP Basic con usuario/contraseña. Expone dos operaciones: listar períodos lectivos de un año y listar comisiones de un período, cada una en una sola respuesta sin paginación. Los DTOs de Guaraní son internos al cliente; hacia el servicio devuelve registros propios ya normalizados.

Sin interfaz: hay una sola implementación y los tests sustituyen el `HttpMessageHandler`, que ejercita también el mapeo real. Errores traducidos a una excepción de dominio con la causa genérica; `ServicioComisiones` la convierte en 502/504/503. El log Serilog registra causa, status HTTP y período, nunca credenciales ni cuerpo de respuesta (puede contener PII de docentes).

`OpcionesGuarani` se valida al resolver la integración, no al arrancar: un ambiente sin Guaraní configurado (dev, efímeros) levanta normal y sólo los endpoints de importación responden 503.

### 7. Endpoints

| Método | Path                                                      | Permiso               |
| ------ | --------------------------------------------------------- | --------------------- |
| GET    | `/api/designaciones/comisiones/periodos`                  | `comisiones.ver`      |
| GET    | `/api/designaciones/comisiones?periodoLectivoId=…`        | `comisiones.ver`      |
| GET    | `/api/designaciones/comisiones/guarani/periodos-lectivos` | `comisiones.importar` |
| POST   | `/api/designaciones/comisiones/importaciones`             | `comisiones.importar` |

`/periodos` devuelve los períodos importados con la última importación exitosa (fecha, nombre del actor resuelto vía `IConsultasIdentity`) y, si el último intento falló, su fecha. El listado devuelve todas las comisiones del período (sin paginación, coherente con `grillas-datos`); los filtros son de cliente. `POST /importaciones` recibe `{ anioAcademico, periodoLectivoId }` y devuelve el resumen de la importación.

Capas: `ComisionesController` → `ServicioComisiones` → `RepositorioComisiones` + `ClienteGuarani`.

### 8. Auditoría sólo del registro de importaciones

`audit.attach('designaciones.sga_importaciones')`, pero **no** sobre `sga_comisiones` ni sus tablas hijas: cada importación borraría e insertaría cientos de filas en `audit.change_log` por datos que son una réplica externa regenerable. La trazabilidad de quién cambió el snapshot y cuándo la da el registro de importaciones. Es una excepción explícita a la convención de `data-model.md`, que se documenta allí.

### 9. Permisos

SQL versionado en `database/identity/`, mismo patrón que `012_identity_permisos_tareas.sql`: `comisiones.ver` para `decanato`, `secretaria`, `coordinador_carrera`, `jefe_catedra`, `administrativo`, `sys_admin`; `comisiones.importar` para `secretaria` y `sys_admin`. Constantes en `Permisos` y políticas registradas como las existentes.

**Ids y numeración**: se usan `b2000000-…-033` y `-034`, para no chocar con los `-026` a `-032` de la rama del asistente. El número del archivo SQL se asigna contra la rama que se integre primero, para no repetir los `014`–`020` que esa rama ya ocupa.

### 10. Compatibilidad con el asistente (text-to-SQL)

Las tablas nuevas caen en el schema `designaciones`, que el asistente expone. Cuando las dos ramas se integren, el test del manifiesto de privilegios va a fallar por tablas sin clasificar. Eso es correcto: obliga a decidir qué ve el modelo. La decisión queda tomada acá, y la aplica quien integre segundo:

| Tabla                   | Clasificación en el manifiesto                                                              |
| ----------------------- | ------------------------------------------------------------------------------------------- |
| `sga_comisiones`        | concedida: todas las columnas salvo `importacion_id`                                        |
| `sga_comision_carreras` | concedida: todas                                                                            |
| `sga_comision_docentes` | concedida: todas (sólo nombre, responsabilidad y `persona_id`; no hay documento)            |
| `sga_comision_horarios` | concedida: todas                                                                            |
| `sga_importaciones`     | denegada-explícita: dato operativo (quién importó y causas de fallo), no consulta académica |

- **RLS**: `ENABLE ROW LEVEL SECURITY` con una policy `FOR SELECT` sobre las cuatro tablas concedidas, con el predicado `identity.asistente_tiene_permiso('comisiones.ver')`. No hay predicado de ámbito porque la visibilidad es global (ver Non-Goals). Va en el SQL del módulo Designaciones, como `009_designaciones_rls_asistente.sql` en esa rama.
- **`COMMENT ON`** en tablas y columnas, explicando que es un snapshot de Guaraní por período, que `persona_id`/`carrera_id`/`materia_id` pueden ser `NULL` y cómo unir con `identity.personas` y `designaciones.designaciones`.

Si esta rama se integra primero, el SQL de RLS y comentarios no puede escribirse todavía, porque `identity.asistente_tiene_permiso` no existe. Lo agrega la rama del asistente al integrarse, siguiendo esta tabla. Si el asistente se integra primero, este change lo incluye (grupo 7 de tasks).

### 11. Frontend

En `features/designaciones`: `api/comisionesApi.ts`, `hooks/useComisiones.ts` (React Query; invalidar listado y períodos al importar), `pages/ComisionesPage.tsx`, `components/TablaComisiones.tsx`, `components/ModalImportarComisiones.tsx` y `filtrosComisiones.ts`. Ruta bajo `RequirePermission permission="comisiones.ver"`; entrada "Comisiones" en el grupo Designaciones de `nav.ts`. La grilla reutiliza los componentes de grilla compartidos (scroll interno, encabezado fijo, textos recortados con tooltip). El modal sigue el patrón de `ModalPeriodo` y muestra los errores 409/502/503/504 con mensajes diferenciados.

## Risks / Trade-offs

- [El contrato real de la API de Guaraní (paths, paginación, nombres de campos de modalidad/ubicación) todavía no está confirmado] → Queda encapsulado en `ClienteGuarani` y sus DTOs; los tests usan respuestas grabadas de la API real (anonimizadas) apenas se obtengan. Specs y endpoints propios no dependen de ese contrato.
- [La importación real sólo se prueba desde local (en la red) y producción; CI y staging no llegan a Guaraní] → Los tests usan fixtures grabados con un `HttpMessageHandler` falso; la primera importación en producción se hace con verificación manual del resultado.
- [Snapshot desactualizado: la pantalla puede mostrar datos viejos] → Se muestra siempre la fecha y el autor de la última importación; la actualización es responsabilidad explícita de Secretaría.
- [Request largo si Guaraní es lento] → Timeout configurable; la reserva caduca a `2 × timeout` para no bloquear el período si el proceso muere.
- [Nombres de docentes son PII leída de un sistema externo] → Sólo nombre y responsabilidad; nunca en logs; visibles sólo con `comisiones.ver`.
- [Docentes sin vincular por documento faltante o mal cargado en `identity`] → Quedan visibles con su nombre; el contador en el registro de importaciones lo hace visible. La corrección se hace en `identity` y se reimporta.
- [Choque con la rama del asistente: ids de permiso, numeración de SQL y manifiesto] → Ids `-033`/`-034` reservados acá; numeración y clasificación se resuelven al integrar, con la tabla de la decisión 10 como fuente.
- [Revisa una decisión arquitectónica registrada] → Se actualiza la sección "Decisiones registradas" del dominio con el motivo y los límites (sólo lectura, regenerable, no editable, sin depender de que los datos existan en `identity`).
- [Sin auditoría fila por fila del snapshot] → Aceptado: el dato es una copia regenerable de Guaraní; el registro de importaciones da la trazabilidad necesaria.

## Migration Plan

1. Desplegar SQL + migración (tablas nuevas y permisos). Aditivo, sin impacto en datos existentes.
2. Configurar `Guarani__UrlBase`, `Guarani__Usuario`, `Guarani__Contrasena` sólo en producción (secretos del pipeline de deploy), el único ambiente desplegado con acceso a la red de Guaraní.
3. Primera importación manual del período vigente por Secretaría.

Rollback: revertir la versión de la app. Las tablas y permisos quedan sin uso y no afectan nada existente; la migración no revierte DDL (convención del repo: `Down` lanza `NotSupportedException` y la recuperación es desde respaldo). Si hubiera que eliminarlos, se agrega una migración incremental.

## Confirmado con el equipo

- **Autenticación**: usuario y contraseña de una cuenta técnica, cargados como secretos (user-secrets en local, variables `Guarani__*` en el deploy).
- **Alcance**: la API devuelve sólo las comisiones del Departamento de Ingeniería; la importación no filtra por carrera.
- **Endpoints**: el equipo provee dos, uno de comisiones por período lectivo y otro que lista los períodos lectivos. Este último alimenta el modal de importación.
- **Identificador**: `comision_guarani_id` es la PK de la comisión en Guaraní, estable entre consultas.
- **Transporte**: JSON, un arreglo de objetos cuyas claves son los alias de la consulta SQL (lo arma el equipo del endpoint).
- **Forma del payload: filas planas**, una por combinación carrera × docente × banda horaria, con `LEFT JOIN` hacia docentes, bandas, aula y modalidad, para que una comisión sin docentes o sin bandas llegue igual (con esos campos nulos). Columnas: `comision`, `asignacion`, `comision_nombre`, `comision_ubicacion`, `elemento_codigo`, `elemento_nombre`, `propuesta_codigo`, `propuesta_nombre`, `dia_semana`, `hora_inicio`, `hora_finalizacion`, `aula`, `modalidad_codigo`, `modalidad_descripcion`, `docente_nombre`, `docente_tipo_documento`, `docente_documento`, `docente_responsabilidad`. El agrupamiento y la eliminación de duplicados se hacen de nuestro lado (decisión 5).
- **Responsabilidad del docente**: se guarda y se muestra tal como se carga en Guaraní (`sga_docentes_responsabilidades.nombre`). En la muestra casi todas dicen "Ingenieria"; es el dato de origen y no se reinterpreta.
- **Muestra real (período 152, 634 comisiones, 515 docentes, 132 aulas)**: `dia_semana` llega como texto sin tildes (`Lunes` … `Sabado`) y se mapea a 1–7; las horas llegan como `HH:mm:ss`; los documentos son numéricos de 7–8 dígitos, sin puntos, y los tipos son DNI o DNI temporario, por lo que se vincula sólo por número; el nombre llega en un único campo `apellido_nombres`; la modalidad llega como `modalidad_codigo` + `modalidad_descripcion` (7 valores, p. ej. `A`/Presencial); 4 comisiones llegan sin docentes y 1 sin bandas; cada `asignacion` tiene un único día, horario y aula; 62 de 567 asignaciones se comparten entre 2 y 5 comisiones, y una comisión tiene dos asignaciones en el mismo horario con aulas distintas. Contra las personas de `sga.sql`, 388 de 515 docentes (75 %) se vinculan por documento. Comisiones compartidas: 15 se ofrecen a 5 carreras a la vez.
- **Períodos lectivos**: el endpoint devuelve `periodo_lectivo` (id, p. ej. `152`), `periodo_lectivo_nombre` (p. ej. "2do Cuatrimestre") y `periodo_lectivo_anio` (p. ej. `2026`). Como el nombre puede incluir o no el año según cómo se cargó en Guaraní, el año se toma siempre de `periodo_lectivo_anio` y el nombre se guarda y se muestra tal cual. El modal arma el selector de año con los años distintos de la respuesta y filtra los períodos en el cliente, así que nuestro endpoint no recibe parámetros. Se guardan como `periodo_lectivo_guarani_id`, `periodo_lectivo_nombre` y `anio_academico`; el orden "más reciente primero" usa año y luego id descendentes.
- **Sin paginación**: ambos endpoints devuelven todo en una sola respuesta (un período son unas 2000 filas, alrededor de 0,5 MB).
- **Red**: la API sólo responde dentro de la red de UNLaM. Llegan a ella el entorno local de desarrollo (conectado a esa red) y el servidor productivo. Los ambientes que no están en la red (staging, efímeros, CI) quedan sin la sección `Guarani` configurada: la pantalla funciona con datos sembrados y la importación responde 503.

## Open Questions

- Paths de ambos endpoints. Afecta sólo la configuración y los fixtures de `ClienteGuarani`.
