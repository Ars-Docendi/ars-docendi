# Proposal

## Why

La pantalla de Roles lista los permisos en una sola columna plana, sin agrupar, y cuesta ver qué habilita cada rol en cada módulo. Además, una persona con varias asignaciones de rol opera siempre con la primera por nombre, sin poder elegir, y el ámbito suma todas sus asignaciones. Ordenar los permisos por módulo y hacer que cada sesión opere con una única asignación elegida es la base para depurar los permisos que hoy no habilitan nada.

## What Changes

Se implementa en tres fases, en este orden:

### Fase 1 — Grilla de roles y permisos por módulo

- `/roles` pasa a ser la grilla administrativa de la guía (como Personas): columnas Nombre, Descripción, Ámbito, Tipo (Sistema o Personalizado), Permisos (`asignados/total`) y Acciones; filtros por encabezado, orden por columna y fila clickeable. Se retiran el panel lateral y el panel de permisos.
- "Nuevo rol" abre `/roles/nuevo` y la fila o "Editar" abren `/roles/:rolId`: pantalla completa con Nombre, Descripción y ámbito arriba y, debajo, directamente los módulos, sin encabezado de rol. Un único "Guardar" persiste datos y permisos. Se retiran `ModalNuevoRol` y `ModalEditarRol`.
- Cada módulo es un bloque desplegable con checkbox de módulo (marcado, desmarcado o intermedio), nombre y contador `asignados/total`; al desplegarlo se ven sus permisos. El checkbox de módulo marca o desmarca todos.
- Un rol nuevo arranca con todos los permisos destildados. **BREAKING**: se retira la opción de crear un rol a partir de un rol base (`rolBaseId`); `POST /api/administracion/roles` recibe los permisos iniciales.
- Los roles personalizados se desactivan y reactivan desde la grilla (columna Estado, acciones Editar y Desactivar/Activar). **BREAKING**: `DELETE /api/administracion/roles/{id}` se reemplaza por `POST .../desactivar` y `POST .../activar`; el listado incluye roles inactivos.
- Un buscador en la sección de permisos filtra por permiso o módulo y despliega los módulos con coincidencias.
- El módulo se deduce del prefijo del código del permiso (`designaciones.*` → Designaciones). No cambia el schema.
- Se usan `Table` y `Checkbox` (con `indeterminate`) de `@ars-docendi/ui` y `FiltroEncabezado`/`MenuAcciones` de `shared/ui`; el bloque desplegable, que la guía no tiene, se arma con los tokens de diseño.

### Fase 2 — Rol activo elegido por la persona

- Cada **asignación de rol** vigente es una opción: rol más ámbito. El mismo rol en dos materias da dos opciones.
- Si la persona tiene una sola asignación, entra directo con ella. Si tiene más de una, entra con la primera por nombre y sobre la aplicación se abre el selector para confirmarla o elegir otra. Se pregunta en cada ingreso; no se recuerda la última elección.
- El menú de usuario (donde está "Cerrar sesión") muestra el rol y el ámbito activos debajo del nombre y suma "Cambiar rol", que abre el mismo selector sin volver a ingresar.
- La sesión opera con los permisos del rol elegido y **sólo con el ámbito de esa asignación**. **BREAKING** (comportamiento): quien hoy suma el ámbito de varias asignaciones del mismo rol pasa a ver sólo el de la elegida.
- La cookie guarda la asignación elegida (no los permisos). En cada solicitud se verifica que siga vigente; si dejó de estarlo, se opera con la de por defecto y se vuelve a ofrecer elegir.
- **API**: `POST /api/auth/asignacion` (elige o cambia la asignación). `GET /api/auth/sesion` informa la asignación activa, las opciones y si falta elegirla. En desarrollo se ingresa eligiendo sólo la identidad y después se elige el rol en la misma pantalla; `GET /api/desarrollo/sesion` devuelve la misma sesión y el header `X-Dev-Asignacion-Id` reemplaza a `X-Dev-Role-Code` (que queda como atajo para tests).

### Fase 3 — Depuración de permisos sin uso

- Con la vista por módulo terminada, se revisa con producto qué permisos no habilitan nada y se retiran con una migración incremental de `identity`. El detalle se agrega a este change como delta de spec antes de implementarlo.

## Capabilities

### New Capabilities

- `seleccion-rol-activo`: elección y cambio de la asignación de rol con la que opera una sesión.

### Modified Capabilities

- `listar-roles`: grilla administrativa con filtros por encabezado en lugar de lista lateral con buscador.
- `crear-rol`: alta a pantalla completa con permisos, todos destildados; se retira la herencia desde rol base.
- `editar-rol`: edición a pantalla completa de datos y permisos con un único guardado.
- `eliminar-rol`: la baja lógica pasa a desactivar/activar, con reactivación.
- `gestionar-permisos-rol`: los permisos se visualizan y marcan agrupados por módulo dentro de la pantalla del rol.
- `inicio-sesion-microsoft`: la sesión opera con la asignación elegida y su cookie la identifica; se retira la elección automática por nombre.

## Impact

- **Shared (identity)**: `ServicioSesion` resuelve la asignación elegida en lugar del primer rol por nombre; `CurrentUser` expone la asignación activa; `ConsultasIdentity` limita sus consultas de ámbito a la asignación activa cuando consulta por el actor de la sesión.
- **Host**: `EventosSesion`, `EndpointsAutenticacion`, `AutenticacionDesarrolloHandler`, `ResolutorAlcanceDocentes`/`AlcancePersonas`.
- **Consumidores cross-module** del ámbito del actor: `Modules.Designaciones` (`ResolutorActor`, `ServicioPedidos`, `ServicioPedidosApi`, `ServicioLoteDesignaciones`, `ServicioCatalogosDesignaciones`) y `Modules.Tareas` (`DirectorioPersonas`). Siguen consumiendo `IConsultasIdentity`; no se agregan dependencias ni cambia el grafo.
- **Frontend**: `features/roles` (grilla, pantalla de alta/edición, permisos por módulo; se retiran `ListaRoles`, `PanelPermisos` y los modales), pantalla de selección de rol, `MenuUsuario`, `shared/auth` (sesión y guardas).
- **Base de datos**: sin cambios en fases 1 y 2; fase 3 agrega una migración de datos en `identity`.
- **Shared (identity) roles**: `ServicioRoles`/`ModelosRoles` reemplazan `RolBaseId` por los permisos iniciales.
- **Docs**: `api-contracts.md` y `api-contracts-administracion.md` (auth y alta de roles), design spec `administracion-roles-permisos-design-spec.md`, artículos de ayuda `administrar-roles-y-permisos`, `ingresar-al-sistema` y `recorrer-la-pantalla`.
- **Rollback**: fases 1 y 2 se revierten con el diff (las cookies emitidas sin asignación vuelven a resolverse por nombre). Fase 3 se revierte con el `Down` de su migración.
