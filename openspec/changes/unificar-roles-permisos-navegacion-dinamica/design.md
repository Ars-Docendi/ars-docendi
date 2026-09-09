## Context

La motivación y el alcance funcional están en `proposal.md`; los detalles de comportamiento quedan en los specs delta de este cambio.

Hoy la administración de roles ya persiste `is_active`, `version` y la relación `rol_permisos`. La API separa editar metadatos de reemplazar permisos, mientras que el frontend mantiene dos rutas, dos features y una navegación indexada por nombres de rol. El selector de desarrollo lista el nombre de rol, pero la sesión frontend lo transforma mediante una unión cerrada.

El cambio cruza la persistencia transversal de identidad, el host HTTP, la autenticación de desarrollo y el shell React. Se deben conservar las fronteras existentes: Controller → Service → Repository en backend, identidad escrita sólo por administración, y sin nuevas referencias entre módulos de negocio.

## Goals / Non-Goals

**Goals:**

- Hacer de `/roles` la única superficie, conservando el layout de Membresía Roles.
- Reusar `roles.is_active` para la baja lógica y `version` para proteger mutaciones concurrentes.
- Mantener estable el código de un rol personalizado al renombrarlo.
- Permitir cambiar permisos de roles institucionales y personalizados sin cambiar su identidad.
- Construir sesión, sidebar y guards desde permisos efectivos, aceptando nombres dinámicos.
- Mantener el circuito especial de Designaciones separado del permiso de visibilidad.

**Non-Goals:**

- No agregar una tabla de rutas ni permitir que la base de datos defina URLs o componentes.
- No crear permisos arbitrarios desde la UI: se usa el catálogo persistido y versionado.
- No convertir un rol personalizado en rol de la máquina de estados de Designaciones por asignarle permisos.
- No implementar reactivación de roles dados de baja en este cambio.
- No resolver la integración SSO de producción pendiente; el adaptador conservará la misma forma de sesión para cuando exista esa fuente.

## Decisions

### 1. Una ruta canónica y una compatibilidad mínima

Se conservará `/roles` como ruta canónica y `/membresia-roles` será un redirect del router. Los componentes de `features/membresia-roles` se consolidarán bajo `features/roles`, que ya posee los modelos y hooks de roles. Se quitará la entrada legacy de la sidebar.

Esto evita mantener dos estados de selección, dos consultas y dos superficies de permisos. No se agrega una nueva ruta de administración porque no aporta una frontera real.

### 2. Baja lógica con el estado existente

La operación nueva seguirá Controller → `ServicioRoles` → `RepositorioRoles` y recibirá la versión esperada. El servicio validará actor, existencia, actividad y `EsSistema`; luego marcará `is_active = false` y hará avanzar la versión dentro de la transacción existente. No se borrarán filas de `roles`, `rol_permisos` ni membresías históricas.

La lista y los selectores operativos consultarán roles activos. La creación y las asignaciones rechazarán roles inactivos. Esto respeta la FK `user_roles.role_id ON DELETE RESTRICT` y conserva auditoría sin inventar una estrategia de restauración.

Alternativa descartada: `DELETE` físico. Rompería referencias y perdería la identidad histórica del rol; además no es necesario para ocultarlo de la operación.

### 3. Inmutabilidad de identidad de sistema, permisos mutables

La actualización de metadatos verificará `EsSistema` y rechazará cambios de código, nombre, descripción, ámbito, marca de sistema y estado. El reemplazo de `rol_permisos` seguirá disponible para cualquier rol activo autorizado por `roles.gestionar_membresia`.

El código del rol personalizado no se editará aunque se permita cambiar su nombre. Así, la selección de desarrollo y las claims no quedan invalidadas por una edición visual.

Alternativa descartada: proteger todo el rol de sistema, incluidos permisos. Contradice la necesidad de ajustar los presets institucionales y haría que la navegación no pudiera reflejar la configuración vigente.

### 4. Permisos explícitos para pantallas que tenían excepciones

Se incorporarán `designaciones.revisar` y `docentes.ver` al catálogo versionado y a los defaults institucionales que hoy muestran esas pantallas. El registro de navegación y las rutas usarán esos permisos; el backend de Designaciones seguirá aplicando etapa, ámbito y las restricciones de aceptación de Administración, y Docentes seguirá filtrando por el ámbito efectivo del actor.

Los permisos resuelven visibilidad de pantalla. No reemplazan `ResolutorActor` ni las reglas de dominio que requieren roles institucionales para avanzar el circuito, ni el filtrado de Docentes por las materias del actor.

Alternativa descartada: mantener excepciones por nombre de rol en frontend o en el gate general de Docentes. Reproduciría el bug con roles personalizados y permitiría que una edición de permisos no se refleje.

### 5. Sesión dinámica sin unión de nombres

`CurrentUser` conservará `roleCode`, mostrará el `nombre` recibido y agregará `permissions: string[]`. El catálogo de identidades de desarrollo ampliará `RolDesarrolloDto` con los permisos del rol seleccionado, reutilizando la carga de `ServicioIdentidadesDesarrollo`; la validación de sesión continuará resolviendo los permisos desde identity y agregará las claims existentes.

La sidebar recibirá la sesión completa. En lugar de `NAV_BY_ROLE`, habrá una configuración estática de grupos, rutas, iconos y permiso requerido, filtrada por una función pequeña de permisos. Los guards se cambiarán de `RequireRole` a un guard de permiso que no renderice la ruta si falta el permiso. El registro mínimo será: Portal → `portal.ver`, Aulas → `aulas.ver`, Tareas → `tareas.ver`, Usuarios → `usuarios.ver`, Docentes → `docentes.ver`, Roles → `roles.ver`, Mis pedidos → `designaciones.gestionar`, Revisión → `designaciones.revisar` y Períodos → `periodos.administrar`.

Las URLs siguen siendo código, no datos de identity: eso conserva tipado, evita UI ficticia y no agrega una superficie de configuración especulativa.

### 6. Acciones separadas de visibilidad

La pantalla unificada requerirá `roles.ver`; crear/editar y eliminar requerirán `roles.administrar`; guardar permisos requerirá `roles.gestionar_membresia`. La UI ocultará o deshabilitará acciones según esos permisos y protecciones del rol, pero el backend validará todo nuevamente.

La selección de un rol se identificará por ID y, después de cada mutación, se derivará del listado actualizado o se renovará la consulta. Así se evita guardar permisos con un `version` viejo después de una invalidación de React Query.

## Risks / Trade-offs

- [Sesiones abiertas con claims viejas] → La modificación de permisos será efectiva al renovar/validar la sesión; la UI invalidará sus consultas y no prometerá revocación retroactiva de un token ya emitido.
- [Permisos de pantalla faltantes en instalaciones existentes] → La migración SQL será idempotente y asignará `designaciones.revisar` y `docentes.ver` a los roles institucionales que hoy tienen esas entradas; los roles personalizados deberán recibirlos explícitamente.
- [Código backend aún basado en roles para acciones de Designaciones] → Se conserva intencionalmente y se cubre con tests de que un permiso de pantalla no salta las reglas de dominio.
- [Baja lógica sin reactivación] → Se informa como estado terminal de este cambio, se conserva toda la evidencia y no se agrega un botón muerto de restaurar.
- [Cambio de layout y ruta] → El redirect legacy y pruebas de navegación cubren enlaces existentes; los datos se mantienen en la misma API.

## Migration Plan

1. Agregar el permiso faltante y sus asignaciones default mediante SQL/migración versionada; desplegar backend compatible con clientes actuales.
2. Implementar protección de sistema, baja lógica, DTO de desarrollo y pruebas de API.
3. Publicar el frontend con sesión dinámica, rutas por permisos, pantalla unificada y redirect legacy.
4. Verificar sesión con un rol institucional con permisos modificados y con un rol personalizado renombrado y asignado.
5. Si se revierte el frontend, mantener el backend compatible y no reactivar ni borrar roles dados de baja. Si se revierte la aplicación completa, preservar las filas y revertir sólo el despliegue; cualquier recuperación de un rol inactivo requerirá una decisión posterior explícita.
