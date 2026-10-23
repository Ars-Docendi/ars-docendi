# Design

## Context

- `PopupBuscarResponsable` (Tareas) es el patrón de referencia: `Modal` de `@ars-docendi/ui`, formulario de filtros con "Buscar", `Table` con filas clickeables y "Cancelar". Busca en el servidor porque los candidatos dependen de la jerarquía.
- Las materias ya llegan completas al frontend en los catálogos de administración (`materias` canónicas y `materiasPlan` materia–carrera), así que el filtrado puede ser local y no hace falta un endpoint nuevo.
- `MembresiasSelector` vive en `shared/ui` y lo usan Docentes (nuevo/editar) y Usuarios (nuevo/editar rol).
- El `Modal` de la ui-lib acepta `className` sobre `.adoc-modal` (máximo 520px, `.body` con `overflow: auto`). Designaciones ya lo sobrescribe (`.adoc-macc.adoc-modal`).
- Hoy `OpcionCatalogoDto` no lleva código de carrera, y `RepositorioUsuarios` devuelve `mp.Id` (fila materia–plan) como `Id` de `materiasPlan`, mientras `MembresiasSelector` lo compara con `materiaId`.

## Goals / Non-Goals

**Goals**

- Un único componente `BuscadorMaterias` reutilizado por asignaciones y membresías.
- Mismo aspecto que el buscador de Responsable, con los componentes de la ui-lib.
- Modales de docente y del buscador redimensionables sin tocar la ui-lib.

**Non-Goals**

- Cambiar reglas de ámbito, unicidad o permisos (siguen validándose en backend).
- Cambiar el selector de carrera del Coordinador ni los formularios de pedidos de Designaciones.
- Hacer estirables los modales de Usuarios.

## Decisions

1. **Componente compartido `shared/ui/BuscadorMaterias.tsx`** con props: `open`, `modo` (`"par"` | `"materia"`), `pares` (catálogo materia–carrera), `seleccion`, `noElegibles` (claves + motivo), `titulo/contexto`, `onSeleccionar`, `onCerrar`. Lo consume un `SelectorMateria` (campo de solo lectura + botón), equivalente a `SelectorResponsable`. Vive en `shared/ui` porque lo usan Docentes y Usuarios; no depende de ninguna feature.
2. **Filtrado local con aplicación explícita.** Se mantiene "Buscar"/Enter (como Tareas) separando el borrador de los filtros aplicados. Normalización NFD sin diacríticos y `includes` por campo; los cuatro filtros se combinan con AND. Alternativa descartada: endpoint de búsqueda en servidor — innecesario con el catálogo ya cargado.
3. **Modo "materia" para Jefe de Cátedra.** Se agrupan los pares por `materiaId`; una materia coincide si alguno de sus pares coincide con los filtros. La columna "Se dicta en" lista `código · nombre` de las carreras.
4. **No elegibles visibles.** El llamador calcula las claves no elegibles (asignaciones: `materiaId`; membresías: `rolId` + ámbito) y su motivo; el buscador las muestra deshabilitadas con un chip y no dispara `onSeleccionar`. La fila actual se marca con la clase `selected` de la `Table`.
5. **Filas accesibles.** Las filas elegibles usan `propsFilaClickeable` (foco con Tab, Enter/Espacio), igual que el resto de las grillas.
6. **Modales estirables por CSS.** Clase `adoc-modal--estirable` en `shared/ui` aplicada vía `className`: `resize: both; overflow: hidden` y máximos acotados a la ventana. El buscador abre en 880×640 (mínimo 560×420). Los modales de docente abren con `width: fit-content; height: auto` (mínimo 640×320): el campo de solo lectura de `SelectorMateria` declara `size` según el largo de su texto, así el ancho intrínseco alcanza para leer materia y carrera, y el alto termina en "+ Agregar asignación"; `.body { flex: 1; min-height: 0 }` para que desplace el cuerpo y no el pie. Alternativa descartada: agregar `resizable` a la ui-lib (repo externo, release aparte).
7. **Contrato aditivo.** `OpcionCatalogoDto` suma `string? CarreraCodigo = null` al final (no rompe posicionales existentes). `ServicioDocentes` y `RepositorioUsuarios` lo completan en `materiasPlan`; `RepositorioUsuarios` pasa a devolver `mp.MateriaId` como `Id`. `.Distinct()` sigue deduplicando pares de varios planes de la misma carrera.

## Risks / Trade-offs

- **Bug en Usuarios (corregido aquí):** con `Id = mp.Id` las carreras de la membresía Docente nunca coinciden. Se confirma con un test de regresión antes del fix. Si algún consumidor dependiera del id de fila materia–plan, fallaría; la búsqueda no encontró ninguno.
- **`resize` nativo:** el control es la esquina del navegador (sin estilo propio) y no responde al teclado; es una mejora de comodidad, no un requisito de accesibilidad. En pantallas chicas se limita con `max-width/max-height: 100%`.
- **Catálogo grande:** ~1000 pares en una tabla sin virtualizar; aceptable (las grillas del proyecto ya muestran listas sin paginar). Si se nota lento, se limita el render a los primeros N con aviso.

## Migration Plan

Sin migraciones ni datos. Despliegue normal; rollback revirtiendo el diff.
