## Context

Hay 5 grillas de datos en la app, todas sobre `Table` de `@ars-docendi/ui`: Mis pedidos, Revisión, Períodos, Usuarios y Docentes. Las 5 reciben la lista completa de la API y filtran y ordenan en el cliente.

Estado actual:

- `.adoc-table-wrap` (ui-lib) tiene `overflow: hidden`, así que las columnas que no entran quedan cortadas. Mis pedidos (`.adoc-mp-table { overflow-x: auto }`) y Revisión (`.adoc-tabla-scroll`) lo parchearon por su cuenta.
- Solo Mis pedidos pagina: un paginador propio de 9 filas con "Anterior / números / Siguiente" y "Registros x–y de N". No usa `Pagination` de la ui-lib.
- Acciones es siempre la última columna.
- Solo Mis pedidos tiene la fila clickeable (abre el detalle), con `stopPropagation` en sus botones.
- Mis pedidos y Períodos eliminan con una X roja sin texto; el resto de las acciones de fila son botones de texto.

Usuarios: personal administrativo, con pantallas chicas y poca experiencia tecnológica (definido con July).

## Goals / Non-Goals

**Goals:**

- Que toda la información de una grilla sea visible (sin columnas cortadas ni páginas).
- Que operar una fila sea fácil: toda la fila es el objetivo del click.
- Que las 5 grillas se comporten igual, con el comportamiento en la ui-lib y no en parches por pantalla.

**Non-Goals:**

- Paginación en el servidor o virtualización de filas. Los volúmenes son de un departamento (decenas a pocos cientos de filas); se revisa si crece.
- Columnas fijas (ni Acciones ni la primera): ver D3.
- Cambiar filtros, orden o columnas de cada grilla.
- Roles (`ListaRoles`), que es una lista de tarjetas, no una tabla.

## Decisions

### D1. Sin paginación

Las 5 grillas muestran todas las filas. Filtrar por encabezado y las pestañas de Revisión son la forma de acotar. El meta ya muestra el total ("N pedidos", "N docentes").

- **Por qué**: los usuarios buscan una fila puntual o trabajan una cola; paginar agrega clics entre el usuario y la fila. Además, 4 de las 5 grillas ya no paginan.
- **Alternativas descartadas**: `Pagination` de la ui-lib con solo números y flechas en las 5 grillas (más estado y clics, sin necesidad por el volumen); scroll de la página sin contenedor (con scroll horizontal, el encabezado fijo solo funciona dentro del contenedor que scrollea).

### D2. Scroll dentro de la tabla con encabezado fijo (ui-lib)

`Table.Root` envuelve el `<table>` en un contenedor de scroll propio (`.adoc-table-scroll { overflow: auto }`) y suma la prop `maxHeight` (por ejemplo, `"calc(100vh - 260px)"`), que acota su alto. El `thead` queda fijo (`position: sticky; top: 0`) mientras el cuerpo scrollea. El contenedor va en `Table.Root` y no en `Table` (el wrapper), para que la `Table.Toolbar` quede fuera del scroll y no tape el encabezado; el wrapper conserva `overflow: hidden` para su borde. Decidido al implementar.

- Cada pantalla pasa el alto disponible según su encabezado, para que la página en sí no scrollee y no haya doble barra.
- Los filtros de encabezado ya se abren en un portal (`createPortal`), así que funcionan dentro del contenedor con scroll (Revisión ya lo demuestra).
- Se borran los parches `.adoc-mp-table { overflow-x }` y `.adoc-tabla-scroll`.

### D3. Sin columnas fijas (descartado)

Se implementó y probó una columna Acciones fija a la derecha (`sticky="end"` en `Table`). July la descartó al verla: la columna quedaba ancha y vacía en muchas filas, tapaba columnas de datos y partía la tabla con una línea divisoria. Se quitó de la ui-lib y de las grillas.

Lo que resolvía (acciones fuera de la vista en pantallas chicas) queda cubierto de otra forma:

- El click en la fila ejecuta la acción principal (D4) desde cualquier columna.
- Mis pedidos deja de forzar anchos (`min-width: 1120px` y anchos por columna), así la tabla usa el ancho natural y entra en pantallas comunes; el scroll horizontal queda para pantallas realmente angostas.

### D4. Click en la fila = acción principal

Regla única:

- Si la grilla tiene detalle, el click abre el detalle: Mis pedidos (ya lo hace) y Revisión.
- Si no, abre Editar (modal): Períodos, Usuarios y Docentes.
- Solo si el usuario puede ejecutar esa acción en esa fila, con la misma condición que muestra el botón. Si no puede, la fila no es clickeable (sin cursor de mano ni resaltado).

Se implementa en dev con un helper compartido (`shared/ui`) que arma los props de la fila (`onClick`, clase clickeable). Ignora el click cuando:

- nace en un elemento interactivo (`button`, `a`, `input`, `select`, `label`), así cada botón y link de la fila hace lo suyo sin `stopPropagation` en cada uno;
- hay texto seleccionado (el usuario está copiando un DNI o un legajo).

En las grillas con detalle se **quita el botón "Ver"**: repetía lo mismo que la fila en todas las filas. Revisión se queda sin columna Acciones (ganando ancho); Mis pedidos conserva Editar y Eliminar. Para que siga siendo accesible y evidente, las filas clickeables son enfocables con Tab, se abren con Enter o Espacio (solo cuando el foco está en la fila, no en un botón de adentro) y se resaltan al pasar el mouse. En las grillas sin detalle, el botón "Editar" se mantiene. Decidido con July después de probar la UI.

- **Por qué no es riesgoso abrir Editar**: el modal no cambia nada hasta Guardar, y se cierra con Cancelar o Escape.

### D7. Textos largos recortados con tooltip

Para que las grillas entren en pantallas comunes sin sacar columnas, las columnas de texto variable reservan un **ancho mínimo** (`anchoMinimo`) y el texto **usa todo el ancho que tenga la columna**; solo si no le alcanza se recorta con "…" y se ve completo al pasar el mouse. Componente `shared/ui/TextoRecortado`, igual en todas las grillas. Ajustado grilla por grilla con July.

- Primero se probó un ancho **máximo** sobre el texto: recortaba aunque la columna tuviera espacio (July lo vio con la página alejada). Se cambió a mínimo + ocupar el ancho disponible.
- Para que el espacio sobrante vaya a estas columnas y no a las demás, la caja lleva un medidor invisible (`::after` con `attr(data-texto)`, con salto de línea): la tabla toma el texto completo como ancho **preferido** sin subir el **mínimo**. No agrega texto al DOM.
- El tooltip se posiciona compensando el `zoom` de la raíz (`index.css`), igual que `FiltroEncabezado`.

- Tooltip **propio** (portal a `body`, `role="tooltip"`): aparece a los ~100 ms, con el tamaño de letra de la tabla (14 px) y los colores de la guía. Se probó primero el nativo (`title`) y July lo descartó: tardaba ~1 s en aparecer y la letra era chica.
- Solo se muestra cuando el texto realmente no entra (sin tooltips redundantes) y se cierra al salir con el mouse o al scrollear.
- **Alternativas descartadas**: sacar columnas (Legajo debajo del nombre) y salto de línea (filas más altas). July prefirió conservar columnas y el alto de fila.

### D5. Acciones de fila siempre con texto

Las X de eliminar de Mis pedidos y Períodos pasan a ser `Button variant="ghost" size="sm"` con el texto "Eliminar" en rojo (`color-text-danger`), con el mismo formato que Ver/Editar/Desactivar. Esto cumple el anti-pattern "iconos sin label" de `design-principles.md`, importante para este público.

### D6. Release de la ui-lib

Mismo flujo que v1.0.3: PR a `main` → tag `v1.0.4` → `release.yml` publica `release/v1.0.4` → bump en dev. Mientras tanto, dev se prueba con la copia local del build (sin usar `pnpm run`, que restaura la dependencia).

## Risks / Trade-offs

- [Con cientos de filas, el scroll es largo] → Los filtros por encabezado y el meta con el total. Si el volumen crece (años de historial), se filtra por defecto (como el período en Revisión) antes que paginar.
- [`position: sticky` en el encabezado con `border-collapse: collapse` pierde el borde inferior] → Se dibuja con `box-shadow` en vez de `border`. Verificar en Chrome, Firefox y Safari.
- [El alto disponible depende de cada pantalla] → Cada página pasa su `maxHeight`; si el encabezado cambia, hay que ajustarlo. Se centraliza en una constante por pantalla.
- [Un click en la fila puede no ser evidente para quien no lo sabe] → Cursor de mano, resaltado al pasar el mouse y foco visible con el teclado.

## Migration Plan

1. ui-lib: `Table.Root` con contenedor de scroll, `maxHeight` y encabezado fijo → PR → tag `v1.0.4`.
2. dev: aplicar en las 5 grillas, borrar paginador y parches, cambiar las X por "Eliminar", sumar el click en la fila.
3. Bump a `#release/v1.0.4`.
4. Rollback: revertir el PR y volver a `#release/v1.0.3`.

## Open Questions

Ninguna.
