## Why

Las comisiones reales (qué materia se dicta, en qué carrera, aula, horario, modalidad y con qué docentes) viven en SIU-Guaraní, y hoy quienes gestionan designaciones tienen que salir de Ars Docendi para consultarlas. Traerlas a una pantalla propia, por período lectivo, da ese contexto dentro del sistema; hacerlo mediante una importación manual a una tabla local evita consultar Guaraní en cada visita y desacopla la disponibilidad de la pantalla de la de Guaraní.

## What Changes

- Nueva pantalla **Comisiones** (`/designaciones/comisiones`) con una grilla de solo lectura: comisión, materia y código, carrera(s) y código, aula, bandas horarias, docentes, modalidad y ubicación. Se filtra por período lectivo entre los ya importados y muestra cuándo y quién hizo la última importación de ese período.
- Botón **Actualizar desde Guaraní** en el encabezado que abre un modal de importación: se elige año académico y período lectivo (la lista de períodos se pide a Guaraní) y se confirma.
- La importación consulta la API de Guaraní desde el backend y **reemplaza, en una sola transacción, solo las comisiones del período importado**. Si Guaraní falla o responde datos inválidos, se conservan las comisiones previas de ese período.
- Snapshot en un schema propio `comisiones` (gestionado por el módulo Designaciones): `sga_comisiones` (identificada por la PK de Guaraní) más `sga_comision_docentes`, `sga_comision_carreras` y `sga_comision_horarios`. Las FKs hacia `identity` son nullable: el snapshot no exige que nada exista en el sistema. Además, `sga_importaciones` como registro de cada importación (quién, cuándo, período, resultado y cantidades).
- **Vinculación automática en cada importación**: docente → persona por documento, carrera por código y materia por código dentro de la carrera, como FKs nullable (`ON DELETE SET NULL`), igual que las que ya usan `pedidos` y `designaciones` hacia `identity`. El documento no se guarda. Lo que no tiene correspondencia se importa sin vínculo y se cuenta.
- Las tablas nuevas quedan preparadas para el asistente text-to-SQL (rama `feature/asistente-modelo-local`): clasificación en su manifiesto de privilegios, RLS por `comisiones.ver` y comentarios de schema.
- Nuevo cliente HTTP saliente hacia la API de Guaraní con credenciales (usuario/contraseña) en configuración del servidor por ambiente; nunca viajan al navegador ni se guardan en la base.
- Dos permisos nuevos: `comisiones.ver` (Decanato, Secretaría, Coordinador de Carrera, Jefe de Cátedra, Administrativo, sys_admin) y `comisiones.importar` (Secretaría, sys_admin).
- Se revisa la decisión registrada "Solo lectura de Guaraní — NO replicar esa data en nuestra DB" en `docs/architecture/domains/designaciones.md`: pasa a admitir un snapshot de solo lectura, regenerable y no editable desde Ars Docendi.

### Ajustes respecto del planteo original

- **Filtro por período lectivo, no por rango de fechas**: Guaraní organiza las comisiones por año académico + período lectivo; un rango de fechas obliga a decidir qué hacer con períodos que cruzan el borde.
- **Reemplazo acotado al período**: pisar toda la tabla borraría períodos que no se reimportan (importar el 2.º cuatrimestre eliminaría el 1.º).
- **Reemplazo atómico**: borrar e insertar en la misma transacción, después de haber leído y validado todo lo recibido; una caída a mitad de camino no deja el período vacío.
- **Una importación a la vez**: dos importaciones concurrentes del mismo período se rechazan con un conflicto en vez de pisarse.
- **Registro de importaciones** separado del snapshot, para mostrar "Actualizado el … por …" y diagnosticar fallos sin auditar fila por fila miles de inserts.
- **Carreras como lista**: en Guaraní una comisión puede ofrecerse a más de una propuesta; un único par carrera/código perdería información.
- **Docentes, carreras y bandas horarias en tablas hijas, no agrupados en un campo**: el asistente consulta con SQL, y "¿en qué comisiones da clase X?" sólo es confiable con un `JOIN` por `persona_id`, no comparando nombres dentro de un texto.
- **Sin vínculos manuales**: como todo vínculo se recalcula desde Guaraní + `identity`, el reemplazo por período no pierde información.
- **Identificador estable**: la PK es el id de Guaraní, no un uuid regenerado en cada importación.
- **Minimización de PII**: de los docentes solo se guarda nombre, responsabilidad y el vínculo a la persona; el documento se usa para vincular y se descarta.

## Capabilities

### New Capabilities

- `importar-comisiones-guarani`: importación manual, por período lectivo, de las comisiones de Guaraní hacia el snapshot local, con reemplazo atómico por período, registro de importaciones y manejo de errores de Guaraní.
- `listar-comisiones`: pantalla y API de consulta de las comisiones importadas, filtradas por período lectivo, con la información de la última importación.

### Modified Capabilities

- `grillas-datos`: la grilla de Comisiones se suma a las grillas sin paginación.

## Impact

- **Módulos**: `Modules.Designaciones` (backend) y `features/designaciones` (frontend). No cambia ningún `*.Contracts`. `ArsDocendi.Shared` suma una lectura a `IConsultasIdentity` (búsqueda de personas por documento), sin cambiar las existentes ni a sus consumidores.
- **Asistente (rama no integrada)**: al integrarse ambas ramas hay que clasificar las tablas nuevas en `database/asistente/manifiesto-privilegios.json` y agregar RLS y comentarios; ids de permiso y numeración de SQL se eligieron para no chocar (ver design, decisiones 9 y 10).
- **Grafo de dependencias**: sin aristas nuevas entre proyectos. Se agrega una dependencia **externa** de `Modules.Designaciones` hacia la API de Guaraní (ya prevista como "TBD" en el dominio). `Microsoft.Extensions.Http` ya llega con ASP.NET Core; no se agregan paquetes.
- **API**: nuevos endpoints bajo `/api/designaciones/comisiones` (listar, períodos importados, períodos lectivos de Guaraní, importar). Se documentan en `docs/architecture/api-contracts-designaciones.md`.
- **Datos**: schema nuevo `comisiones` con cinco tablas vía SQL versionado (`database/comisiones/`) + migración de Designaciones; dos permisos nuevos en `identity.permisos` con sus `rol_permisos`. Se documenta en `docs/architecture/data-model.md`.
- **Configuración / infraestructura**: sección `Guarani` (`UrlBase`, `Usuario`, `Contrasena`, `TimeoutSegundos`) en `appsettings` con valores vacíos; los secretos se inyectan por ambiente (`Guarani__Usuario`, `Guarani__Contrasena`). Se documenta en `docs/architecture/infrastructure.md`.
- **Seguridad**: credenciales sólo en servidor; errores de Guaraní se registran sin credenciales ni payloads con PII y se devuelven al usuario con mensajes genéricos.
- **Reglas de negocio**: no proviene de normativa institucional; no se registra ninguna BR.
- **Rollback**: el cambio es aditivo. Revertir el despliegue a la versión anterior oculta la pantalla y deja las tablas y permisos nuevos sin uso, sin afectar datos existentes. Siguiendo la convención del repo, la migración no revierte DDL (`Down` lanza `NotSupportedException`); si hiciera falta quitarlos, se hace con una migración incremental nueva.
