# Design

## Context

- `identity.permisos` tiene `code`, `nombre` y `descripcion`; no tiene módulo. Los códigos usan prefijo de módulo (`roles.*`, `designaciones.*`, `tareas.*`, …), definidos en `Shared/Auth/Permisos.cs`.
- `/roles` es una lista lateral con buscador más un panel de permisos plano con checkbox nativo; alta y edición son modales, y el alta permite copiar permisos de un rol base.
- La cookie de sesión guarda sólo usuario e inicio. `EventosSesion.ValidarAsync` reconstruye el principal en cada solicitud con `ServicioSesion`, que hoy toma el primer rol por nombre y todos sus permisos. El ámbito no viaja en el principal: los consumidores (`AlcancePersonas`/`ResolutorAlcanceDocentes`, `Modules.Designaciones`, `Modules.Tareas`) lo consultan por `usuarioId` en `IConsultasIdentity`, que suma todas las asignaciones.
- `@ars-docendi/ui` ofrece `Checkbox` (con `indeterminate`) y `RoleMenu` (opciones con `name`, `scope`, `current`). No ofrece acordeón.

## Goals / Non-Goals

**Goals:**

- Ver y editar los permisos de un rol agrupados por módulo.
- Que cada sesión opere con una única asignación (rol + ámbito), elegida por la persona y cambiable sin volver a ingresar.
- Dejar la base lista para retirar permisos sin uso.

**Non-Goals:**

- Cambiar el schema de permisos o de asignaciones.
- Permisos asignados directamente a usuarios (fuera del rol).
- Recordar la última asignación elegida.
- Definir en este documento qué permisos se retiran (fase 3, se decide con la vista terminada).

## Decisions

### D1. Módulo por prefijo del código

El módulo es el segmento anterior al primer `.` del código. Un mapa en el frontend (`features/roles/modulosPermisos.ts`) traduce el prefijo a etiqueta y orden (`personas` → Personas, `designaciones` → Designaciones, …); un prefijo sin entrada se muestra con el prefijo capitalizado al final, para que un permiso nuevo nunca quede oculto. Alternativa descartada: columna `modulo` en `identity.permisos`. Requiere migración y no aporta nada mientras los códigos sigan la convención.

### D2. Grilla y pantalla completa

`/roles` replica la grilla de Personas: `Table` de la guía, `FiltroEncabezado`, orden por columna, `filaClickeable` y `MenuAcciones` (Editar; Eliminar sólo en personalizados, con `ModalConfirmarEliminar`). La columna Permisos sale de `permisos.length` sobre el catálogo. `/roles/nuevo` y `/roles/:rolId` son una sola pantalla `EditorRol`: datos del rol arriba y módulos debajo, sin encabezado propio, con "Cancelar" y "Guardar" al pie. Los campos de datos se habilitan con `roles.administrar` (y nunca en roles de sistema) y los permisos con `roles.gestionar_membresia`.

Guardar en alta hace un solo `POST` con los permisos iniciales (reemplaza `rolBaseId`), atómico en el servidor. Guardar en edición hace `PUT` de datos sólo si cambiaron y luego `PUT` de permisos con la versión devuelta; si el segundo falla por concurrencia, la pantalla queda con los permisos pendientes y ofrece recargar, como hoy.

### D3. Bloque desplegable con tokens

Cada módulo se renderiza como `<details>`/`<summary>`, que da teclado y estado abierto/cerrado accesibles sin JS propio. El `Checkbox` de módulo de la guía va dentro del `summary` con `indeterminate`, y su `onClick` frena la propagación para no plegar el bloque. Bordes, espaciados, colores del contador y chevron salen de los tokens de diseño existentes en `roles.css`; no se agrega un componente compartido mientras haya un solo uso.

### D4. La cookie guarda la asignación, no los permisos

Se agrega el claim `ars:asignacion` a la cookie sólo cuando la persona elige (`POST /api/auth/asignacion`), que valida que sea una asignación vigente propia, reemite la cookie conservando `ars:inicio` y exige el token anti-falsificación. `ValidarAsync` resuelve en cada solicitud: si el claim es una asignación vigente, opera con ella; si no hay claim o ya no es vigente, opera con la primera vigente por nombre de rol y ámbito. El principal lleva siempre `ars:asignacion` (la efectiva, que usa el ámbito) y `ars:asignacion-elegida` sólo si la persona eligió o tiene una sola. Así nunca hay un estado sin permisos: lo descartamos porque obligaba a una pantalla en blanco antes de entrar.

### D5. Contrato de sesión

`GET /api/auth/sesion` suma `asignacion` (`id`, `rolCodigo`, `rolNombre`, `ambitoTipo`, `ambitoNombre`) o `null`, la lista `asignaciones` y `requiereElegirRol`; `rolCodigo` y `rolNombre` pasan a ser opcionales. Las opciones viajan en la propia sesión, sin un `GET /api/auth/asignaciones` aparte. En desarrollo, `GET /api/desarrollo/sesion` devuelve la misma forma. Cuando `requiereElegirRol` es verdadero, el shell abre el selector de rol como modal sobre la aplicación, que ya funciona con la asignación por defecto.

### D6. Ámbito por asignación activa

`ICurrentUser` expone `AsignacionActivaId`. `ConsultasIdentity` lo recibe y, cuando consulta por el propio actor de la sesión, limita a esa asignación las lecturas de ámbito (`TieneRolEn*`, `ObtenerMaterias/CarrerasDeRol`, `ObtenerCodigosDeRolesDeSistema`, `ObtenerCodigosDePermisos`); sobre terceros no cambia nada. Como toda lectura de ámbito de Designaciones, Tareas y Personas pasa por `IConsultasIdentity`, ningún llamador cambia ni se agregan dependencias. Alternativa descartada: que cada llamador pase la asignación; es fácil olvidarlo en uno y volver a sumar ámbitos. Otra descartada: poner materias y carreras en el principal, que obliga a cada consumidor a leer claims.

### D7. Selector y cambio de rol

Un único `ModalCambiarRol` con el `RoleMenu` de la guía, una opción por asignación (`name` = rol, `scope` = materia/carrera/Global) y la actual marcada. "Cambiar rol" del menú de usuario lo abre (sólo con más de una asignación). Al ingresar con varias asignaciones, `AppLayout` lo abre con el título "¿Con qué rol querés trabajar?"; elegir una opción o cerrarlo fija la elección. Tras cambiar, se descartan las consultas cacheadas del rol anterior y se navega al inicio.

### D8. Ingreso de desarrollo

El ingreso de desarrollo sigue autenticando por headers, sin cookie. El header de rol (`X-Dev-Role-Code`) se reemplaza por `X-Dev-Asignacion-Id`: el catálogo `/api/desarrollo/identidades` lista una opción por asignación vigente (rol + ámbito), el modal de desarrollo elige una y el handler valida que sea una asignación vigente de esa identidad y arma el principal con los permisos de su rol y el claim de asignación, igual que la sesión real. "Cambiar rol" en desarrollo cambia la asignación guardada en el navegador; con Microsoft llama a `POST /api/auth/asignacion`. Alternativa descartada: emitir la cookie real desde desarrollo. Unifica el camino, pero obliga a registrar el esquema de cookie sin Microsoft y a rehacer la sesión de desarrollo del frontend y sus tests.

## Risks / Trade-offs

- **Pérdida de ámbito sumado (BREAKING de comportamiento)**: quien tiene el mismo rol en varias materias deja de ver todo junto. Es lo pedido; se documenta en la ayuda.
- **Lecturas de ámbito fuera de `IConsultasIdentity`** no se limitarían. Hoy no hay: las demás lecturas de `user_roles` son de administración (alta de asignaciones, cambio de ámbito de un rol) y no autorizan al actor.
- **Cookies previas al deploy** no traen `ars:asignacion`: con una sola asignación se resuelve sola; con varias, la persona ve el selector una vez.
- **Prefijos sin mapa**: un permiso nuevo con prefijo nuevo aparece al final con su prefijo como nombre hasta agregarlo al mapa.
