# Design

## Context

- **Hoy hay dos superficies** para la misma persona:
  - `UsuariosController` + `ServicioUsuarios` (Shared) + `ServicioUsuariosAdministracion` (Host): persona, cuenta y membresías de cualquier rol, con `usuarios.ver` y `usuarios.administrar`.
  - `DocentesController` + `ServicioDocentes` (Host): persona, cuenta, membresías limitadas a Docente/Jefe de Cátedra (`RolesPermitidos`) y designaciones vigentes vía `IAdministracionDesignaciones`, en una sola transacción (`UnidadDeTrabajoAdministracion`), con `docentes.ver` y ámbito de Jefe de Cátedra (`ResolutorAlcanceDocentes` → `materiasVisibles`).
- **Permisos actuales** (base local):
  - `usuarios.ver`: Administrativo, Secretaría, Administrador de Sistemas, Observador.
  - `usuarios.administrar`: Administrativo, Secretaría, Administrador de Sistemas.
  - `docentes.ver`: Administrativo, Secretaría, Administrador de Sistemas, Jefe de Cátedra.
  - Decanato no administra. Ningún permiso habilita editar el portal de otro.
- **`ServicioPortal`** (Modules.Portal) opera siempre sobre la persona autenticada (`PersonaActualAsync`/`PerfilActualAsync`). Valida archivos con `RequerirArchivoPropioAsync` (dueño = usuario actual) y expone `IPortalQueries.ObtenerPerfilAsync(personaId)` de sólo lectura, que ya consume Designaciones.
- **Grafo**: el Host ya depende de `Portal.Contracts` y `Designaciones.Contracts`, y Designaciones depende de `Portal.Contracts`. Que Portal dependa de Designaciones crearía un ciclo entre módulos.
- **Datos locales**: 1188 personas, 9 sin cuenta y 784 cuentas sin rol (SGA). No se migran: la regla se exige al guardar.
- **Prototipo de referencia**: listado, editor con pestañas, perfil de Mi Portal y permisos por rol.

## Goals / Non-Goals

**Goals**

- Una pantalla y una API de personas que cubran todo lo que hoy hacen Usuarios y Docentes, con guardado atómico.
- Permisos editables desde Roles, validados en backend en cada operación.
- Perfil de Mi Portal visible y corregible desde la administración, reutilizando los componentes del portal.

**Non-Goals**

- Cambiar Mi Portal del propio docente, las reglas de designaciones o el circuito de pedidos.
- Completar roles o cuentas de personas existentes por migración.
- Selector de rol activo para usuarios con varios roles (lo encara otra persona).

## Decisions

### Fase 0 — Buscador de materias (implementada)

1. **Componente compartido `shared/ui/BuscadorMaterias.tsx`** con props: `open`, `modo` (`"par"` | `"materia"`), `pares` (catálogo materia–carrera), `seleccion`, `noElegibles` (claves + motivo), `titulo/contexto`, `onSeleccionar`, `onCerrar`. Lo consume un `SelectorMateria` (campo de solo lectura + botón), equivalente a `SelectorResponsable`. Vive en `shared/ui` porque lo usan Docentes y Usuarios; no depende de ninguna feature.
2. **Filtrado local con aplicación explícita.** Se mantiene "Buscar"/Enter (como Tareas) separando el borrador de los filtros aplicados. Normalización NFD sin diacríticos y `includes` por campo; los cuatro filtros se combinan con AND. Alternativa descartada: endpoint de búsqueda en servidor — innecesario con el catálogo ya cargado.
3. **Modo "materia" para Jefe de Cátedra.** Se agrupan los pares por `materiaId`; una materia coincide si alguno de sus pares coincide con los filtros. La columna "Se dicta en" lista `código · nombre` de las carreras.
4. **No elegibles visibles.** El llamador calcula las claves no elegibles (asignaciones: `materiaId`; membresías: `rolId` + ámbito) y su motivo; el buscador las muestra deshabilitadas con un chip y no dispara `onSeleccionar`. La fila actual se marca con la clase `selected` de la `Table`.
5. **Filas accesibles.** Las filas elegibles usan `propsFilaClickeable` (foco con Tab, Enter/Espacio), igual que el resto de las grillas.
6. **Modales estirables por CSS.** Clase `adoc-modal--estirable` en `shared/ui` aplicada vía `className`: `resize: both; overflow: hidden` y máximos acotados a la ventana. El buscador abre en 880×640 (mínimo 560×420). Los modales de docente, "Nuevo usuario" y "Editar usuario" usan la clase compartida `adoc-modal--ajustado` (`width: fit-content; height: auto`, mínimo 640×320): el campo de solo lectura de `SelectorMateria` declara `size` según el largo de su texto, así el ancho intrínseco alcanza para leer materia y carrera, y el alto termina en "+ Agregar asignación"; `.body { flex: 1; min-height: 0 }` para que desplace el cuerpo y no el pie. Alternativa descartada: agregar `resizable` a la ui-lib (repo externo, release aparte).
7. **Contrato aditivo.** `OpcionCatalogoDto` suma `string? CarreraCodigo = null` al final (no rompe posicionales existentes). `ServicioDocentes` y `RepositorioUsuarios` lo completan en `materiasPlan`; `RepositorioUsuarios` pasa a devolver `mp.MateriaId` como `Id`. `.Distinct()` sigue deduplicando pares de varios planes de la misma carrera.

### Administrar personas

1. **`ServicioPersonas` en el Host, a partir de `ServicioDocentes`.**
   - Ya resuelve persona, cuenta, membresías y designaciones en una transacción. Se amplía `RolesPermitidos` a todos los roles de sistema, con las validaciones de ámbito de `ServicioUsuarios`.
   - `ServicioUsuarios` (Shared) conserva sólo lo que siga usando `ServicioPersonas`.
   - `PersonasController` reemplaza a `UsuariosController` y `DocentesController`.
   - Alternativa descartada: orquestar las dos APIs viejas desde el frontend. Requiere dos llamadas no atómicas.
2. **Cuatro permisos nuevos.**
   - `personas.ver` (todas) y `personas.ver_ambito` (materias de jefatura) reproducen la distinción actual `usuarios.ver`/`docentes.ver`. Un permiso no lleva el rol de origen, así que el ámbito no puede inferirse de uno solo.
   - `personas.administrar` habilita las mutaciones. `portal.administrar` habilita corregir el portal de otro, acotado al ámbito cuando el actor no tiene `personas.ver`.
   - Una migración incremental de `identity` (scaffold `node scripts/migrations/scaffold.mjs Identity PermisosPersonas`) los crea, los asigna, retira `usuarios.ver`, `usuarios.administrar` y `docentes.ver`, y su `Down` los restaura con sus asignaciones.
3. **Ámbito del Jefe de Cátedra.**
   - Se reutiliza `ResolutorAlcanceDocentes`, que devuelve las materias de jefatura, y el filtro por designación vigente de `ServicioDocentes`.
   - Fuera del ámbito, el detalle y el portal responden 404, como hoy en Docentes.
4. **Portal administrado por contrato.**
   - `Modules.Portal.Contracts` agrega `IAdministracionPortal`, con leer perfil, descargas y las mismas mutaciones de Mi Portal, todas con `personaId` objetivo.
   - `ServicioPortal` se refactoriza para que cada operación reciba el perfil objetivo: el uso propio lo resuelve con la persona actual y el administrativo con el `personaId`. Las validaciones y versiones quedan en un solo lugar.
   - Los archivos que sube el operador son suyos en Storage, así que `RequerirArchivoPropioAsync` sigue siendo válido: valida dueño = usuario actual y propósito, y el archivo se vincula al perfil objetivo. La auditoría registra al operador porque la toma del usuario actual.
   - El Host expone `/api/administracion/personas/{id}/portal/...`, autoriza con `portal.administrar` y el ámbito, y delega en el contrato.
   - Así no se agrega la dependencia Portal → Designaciones.
5. **Verificación de DNI.**
   - `GET /api/administracion/personas/documento/{documento}`, con `personas.administrar`, normaliza a dígitos y devuelve `{ existe, personaId, nombre, legajo, activo }`. Expone sólo lo mínimo para el aviso.
   - El frontend la consulta con debounce mientras se escribe. La unicidad sigue validándose al guardar, con el conflicto `identity-document-conflict` existente.
6. **Reglas de guardado.** Rol obligatorio, cuenta obligatoria y asignaciones obligatorias para Docente y Jefe de Cátedra se validan en `ServicioPersonas` con errores por campo (`membresias`, `upn`, `designaciones`). Las personas heredadas sin rol o sin cuenta se listan marcadas.
7. **Frontend.**
   - Feature `features/personas` con rutas `/personas`, `/personas/nueva` y `/personas/:personaId`; `RequirePermission` acepta `personas.ver` o `personas.ver_ambito`. Redirecciones desde `/usuarios` y `/docentes`.
   - Listado sobre `Table` + `FiltroEncabezado` + `propsFilaClickeable`, con los criterios de filtro y orden actuales.
   - Editor con `Tabs` de la ui-lib, `MembresiasSelector` ampliado a Coordinador y roles globales, y `AsignacionesSelector` con la materia a todo el ancho y cargo, dedicación y horas debajo.
   - Perfil: los componentes `Seccion*` de `features/portal` aceptan modo sólo lectura y handlers inyectados. Como el portal y personas son features distintas, se mueven a `shared/ui/perfil` o personas usa una variante; se decide al implementar, sin cross-import entre features.
   - Se retiran `features/usuarios` y `features/docentes`.
8. **Orden de retiro.** Primero se agrega lo nuevo, después se migra el frontend y al final se retiran las APIs y permisos viejos. Así cada paso compila y se prueba.

## Risks / Trade-offs

- **Bug en Usuarios (corregido aquí):** con `Id = mp.Id` las carreras de la membresía Docente nunca coinciden. Se confirma con un test de regresión antes del fix. Si algún consumidor dependiera del id de fila materia–plan, fallaría; la búsqueda no encontró ninguno.
- **`resize` nativo:** el control es la esquina del navegador (sin estilo propio) y no responde al teclado; es una mejora de comodidad, no un requisito de accesibilidad. En pantallas chicas se limita con `max-width/max-height: 100%`.
- **Catálogo grande:** ~1000 pares en una tabla sin virtualizar; aceptable (las grillas del proyecto ya muestran listas sin paginar). Si se nota lento, se limita el render a los primeros N con aviso.

- **Cambio grande y BREAKING en la API.** Se mitiga con el orden de retiro (decisión 8). El único consumidor es el frontend propio.
- **Permisos editados a mano.** Si alguien quitó o agregó `usuarios.*` o `docentes.ver` a roles desde la pantalla Roles, la migración traduce las asignaciones existentes en lugar de usar una lista fija: quien tenía `usuarios.ver` recibe `personas.ver`, y así con cada permiso. Decanato recibe `personas.ver` y `personas.administrar`, y Jefe de Cátedra `portal.administrar`.
- **Refactor de `ServicioPortal`.** Toca la edición propia de Mi Portal. Los tests existentes del portal tienen que seguir verdes sin cambios de comportamiento.
- **784 cuentas sin rol.** El listado las muestra marcadas, y editarlas exige completar el rol. Es esperable que el operador las corrija de a poco.

## Migration Plan

1. Implementar la API nueva y la migración de permisos manteniendo las APIs viejas.
2. Migrar el frontend a `/personas` con redirecciones.
3. Retirar las APIs viejas, `features/usuarios` y `features/docentes`.
4. Para revertir, se vuelve atrás el diff. El `Down` de la migración restaura los permisos retirados y sus asignaciones.
