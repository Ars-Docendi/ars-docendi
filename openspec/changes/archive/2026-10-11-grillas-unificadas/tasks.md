## 1. ui-lib v1.0.4 (repo externo `Ars-Docendi/ui-lib`)

- [x] 1.1 Crear la rama `feat/table-scroll-sticky` desde `main` actualizado
- [x] 1.2 Contenedor de scroll (`overflow: auto`) dentro de `Table.Root`, con prop `maxHeight` que acota su alto (design D2)
- [x] 1.3 Encabezado fijo: `thead th` con `position: sticky; top: 0` y fondo propio dentro del contenedor
- [x] 1.4 ~~Columna fija `sticky="end"`~~: implementada, probada y descartada por July; quitada de la ui-lib (design D3)
- [x] 1.5 Story de `Table` con muchas filas y columnas y encabezado fijo
- [x] 1.6 Versión `1.0.4`, typecheck y build en verde
- [x] 1.7 PR a `main`, merge, tag `v1.0.4` y verificar que se publicó `release/v1.0.4`

## 2. Click en la fila (dev)

- [x] 2.1 Red: tests del helper de fila clickeable (dispara la acción; ignora clicks en `button`/`a`/`input`/`select`/`label`; ignora con texto seleccionado; sin acción no marca la fila como clickeable)
- [x] 2.2 Green: helper en `shared/ui` que arma los props de la fila (`onClick` + clase clickeable con cursor y hover) (design D4)

## 3. Grillas (dev, con la ui-lib local hasta el release)

- [x] 3.1 Mis pedidos: borrar el paginador (estado, CSS e íconos que queden sin uso) y el parche `.adoc-mp-table { overflow-x }`; la fila usa el helper; X → botón "Eliminar"
- [x] 3.2 Revisión: borrar `.adoc-tabla-scroll`; la fila abre el detalle con el helper (pasando el origen)
- [x] 3.3 Períodos: la fila abre Editar; X → botón "Eliminar"
- [x] 3.4 Usuarios: la fila abre Editar, solo si el usuario puede editar
- [x] 3.5 Docentes: la fila abre Editar, solo si el usuario puede editar
- [x] 3.6 Las 5 grillas: `maxHeight` según el alto disponible de su pantalla; Mis pedidos sin anchos forzados; sin botón "Ver" (Revisión sin columna Acciones); filas enfocables con Enter
- [x] 3.7 Tests por grilla: sin paginador (Mis pedidos), click en la fila abre la acción principal, "Eliminar" con texto, y un botón de la fila no dispara el click de la fila
- [x] 3.8 Verificar en el navegador con los datos de volumen: encabezado fijo, acciones visibles en pantalla angosta, ninguna columna cortada

## 5. Anchos de columna y textos recortados

- [x] 5.1 Red: tests de `shared/ui/TextoRecortado` (muestra el texto; con texto que no entra muestra el tooltip con el texto completo en ~100 ms y lo cierra al salir; con texto que entra no lo muestra)
- [x] 5.2 Green: `TextoRecortado` con ancho máximo y "…" (design D7)
- [x] 5.3 Mis pedidos: Docente, Cátedra y Tipo con `TextoRecortado`; verificar en 1280 px
- [x] 5.4 Revisión, Períodos, Usuarios y Docentes: revisar columnas grilla por grilla con July y aplicar anchos (Revisión sin avatar y prioridad junto al nombre; Períodos nombre recortable; Usuarios Nombre/Email; Docentes Nombre)

## 4. Dependencia y cierre

- [x] 4.1 Bumpear `@ars-docendi/ui` a `github:Ars-Docendi/ui-lib#release/v1.0.4` y `pnpm install`; quitar la copia local
- [x] 4.2 Actualizar `docs/product/designs/proyecto-docente-design-spec.md` (patrón de acciones por fila, tablas sin paginación, click en la fila)
- [x] 4.3 Lint, typecheck, build y tests del frontend en verde
- [x] 4.4 `openspec validate grillas-unificadas --strict`
