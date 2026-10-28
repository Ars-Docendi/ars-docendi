---
status: draft # draft | review | approved
owner: ""
feature: "openspec/changes/centro-de-ayuda/specs/"
last_updated: 2026-10-21
---

# Design spec: Centro de ayuda

## Resumen

Se diseña el **Centro de ayuda** (`/ayuda`): documentación dentro de la app, organizada por módulo, con artículos de pasos y capturas, búsqueda local y visibilidad según los permisos de la sesión. Es contenido estático del frontend; no hay backend. Se accede desde el ítem "Centro de ayuda" al pie del menú lateral (no hay barra superior en escritorio).

## Roles que ven esta surface

- [x] Jefe de Cátedra
- [x] Coordinador de Carrera
- [x] Secretaría Académica
- [x] Decanato
- [x] Administrativos
- [x] Docente

> Todos los roles autenticados entran a `/ayuda`; qué módulos y artículos ven depende de los permisos de su sesión, no del nombre del rol.

## Flujo principal

1. La persona abre "Centro de ayuda" desde el pie del menú lateral. El menú de la app se contrae a solo íconos mientras esté en `/ayuda`.
2. En el inicio ve los módulos que puede usar; también puede buscar desde la sidebar de documentación.
3. Elige un módulo y luego un artículo. La sidebar de documentación marca el paso visible mientras scrollea y permite saltar a cada paso.
4. Amplía una captura con clic o teclado y la cierra con Esc, el fondo o el botón de cerrar.

## Layout / IA

- **Dos sidebars**: el menú de la app (arranca contraído a íconos en cada entrada a `/ayuda`; el botón ☰ de su cabecera lo puede desplegar durante esa visita, y al salir vuelve la preferencia guardada) y la sidebar de documentación de 264 px, sticky y con scroll propio, a la izquierda del contenido dentro de `main`.
- La sidebar de documentación contiene el buscador, los módulos visibles (colapsables) con sus artículos y, en un artículo, el índice de pasos.
- El contenido muestra breadcrumb desde "Inicio" (`MigasAyuda`, con `Link` del router; el `Breadcrumbs` de la librería recarga la página) y `PageHeader`, sin pretitle.
- Rutas: `/ayuda`, `/ayuda/buscar?q=`, `/ayuda/:modulo`, `/ayuda/:modulo/:articulo`.

## Estados a diseñar

| Estado                 | Descripción                                                                              | Cuándo se muestra                                           |
| ---------------------- | ---------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| Loading                | No aplica: el contenido es estático.                                                     | —                                                           |
| Sin artículos visibles | Aviso "Todavía no hay artículos de ayuda disponibles para tu perfil"; sin listas vacías. | Registro vacío o la sesión no cumple ningún permiso         |
| Sin resultados         | "No encontramos resultados para «…». Probá con otras palabras."                          | Búsqueda sin coincidencias entre artículos visibles         |
| Slug desconocido       | Redirección con `replace` a `/ayuda`.                                                    | Módulo o artículo inexistente, o que la sesión no puede ver |
| Error                  | No aplica: no hay red.                                                                   | —                                                           |
| Success                | Módulos, artículos y pasos con capturas.                                                 | Estado normal                                               |

## Accesibilidad

- **Combobox de búsqueda** (ARIA 1.2): input `role="combobox"` con `aria-expanded`, `aria-controls` y `aria-activedescendant`; listbox de resultados con `aria-selected`. Empieza **sin opción activa**; flechas mueven la selección, Enter abre, Esc cierra. Máximo 5 resultados y "Ver todos los resultados" si hay más.
- **Breadcrumb**: `nav` con la última miga como `aria-current="page"` y separadores `aria-hidden`.
- **Visor de capturas**: usa el `Modal` de `@ars-docendi/ui`. Al abrir se enfoca el diálogo; al cerrar el foco vuelve a la miniatura. **Limitación conocida**: el `Modal` no atrapa el foco, por lo que Tab puede salir del diálogo hacia la página.
- Scroll-spy y scroll a anclas respetan `prefers-reduced-motion`.

## Responsive

Por debajo de 900 px la sidebar de documentación se apila sobre el contenido; no se agrega menú hamburguesa.

## Decisiones de diseño

- Sin "Temas frecuentes", videos ni medios placeholder: cada pieza de UI existe porque un artículo la usa.
- El filtrado por permisos es presentacional; el backend sigue siendo la autoridad.
- El módulo Tareas declara `tareas.ver` y `tareas.gestionar` porque algunos de sus artículos exigen el segundo.

## Anti-patterns a evitar (específicos de esta feature)

- Botón "Ayuda" fuera del pie del menú lateral: en escritorio no hay barra superior y en el teléfono la barra fina sólo tiene el botón ☰ y la marca (`barra-superior`).
- Mostrar o buscar artículos que la sesión no puede ver.
- Capturas con PII o datos reales: sólo datos sintéticos.

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- Change OpenSpec: [`openspec/changes/centro-de-ayuda/`](../../../openspec/changes/centro-de-ayuda/)

## Open questions de diseño

- Ninguna abierta.
