## Why

Ars Docendi ya tiene cuatro módulos con pantallas en uso (Mi portal, Tareas, Designaciones y Configuración/Sistema), pero ningún lugar donde una persona aprenda a usarlos. La barra superior perdió su botón "Ayuda" porque era UI ficticia (spec `barra-superior`): hoy no hay nada detrás. La UNLaM pone el sistema en producción para uso real de jefes de cátedra, secretaría, docentes y administración, y cada pedido mal cargado se paga en idas y vueltas.

El Centro de ayuda del backoffice de OSPESE ya resolvió el problema con una estructura probada (navegación por módulos, artículos paso a paso con capturas, búsqueda, preguntas frecuentes). Este change la replica en Ars Docendi, adaptada a sus permisos: cada persona ve solamente la ayuda de lo que puede usar.

## What Changes

- **Entrada**: ítem "Centro de ayuda" (ícono "?") en el pie fijo de la sidebar principal, disponible para todo usuario autenticado. La barra superior NO recupera un botón de ayuda.
- **Rutas nuevas** bajo `/ayuda`: inicio (módulos), `/ayuda/buscar?q=` (resultados), `/ayuda/:modulo` (artículos agrupados) y `/ayuda/:modulo/:articulo` (pasos, "¿Qué sucede después?", preguntas frecuentes, artículos relacionados). Deep links `#paso-N` y `#preguntas-frecuentes`. Un slug desconocido, o de contenido que la persona no puede ver, redirige al inicio de la ayuda.
- **Layout de ayuda**: en `/ayuda/*` la sidebar principal se contrae a íconos y aparece una segunda sidebar de documentación (264 px) con título, buscador, módulos colapsables con sus artículos y, bajo el artículo activo, la lista de pasos con scroll-spy.
- **Contenido como código**: objetos TypeScript tipados en `frontend/src/features/ayuda/contenido/`, sin backend ni CMS. Cada módulo y cada artículo declara los permisos que exige (cualquiera de ellos alcanza; vacío = todos), con las mismas cadenas que `app/shell/nav.ts`.
- **Visibilidad por permisos**: sidebar de ayuda, inicio, página de módulo, búsqueda y relacionados filtran por los permisos efectivos de la sesión. Es presentación: el backend sigue siendo la autoridad.
- **Búsqueda cliente**: normalización sin acentos ni mayúsculas, stop-words, AND entre términos, puntaje ponderado (título 10, palabras clave 6, resumen 3, cuerpo 1), combobox con hasta 5 resultados, teclado, Esc, botón de limpiar y "Ver todos los resultados".
- **Capturas**: archivos estáticos en `frontend/public/ayuda/<modulo>/`, tomados de la app corriendo contra un entorno descartable sembrado solo con `sintetico.sql` (sin SGA ni PII). Se abren a pantalla completa con clic (Esc o fondo cierran). Todo `alt` es obligatorio.
- **Contenido inicial** (ver `design.md`): 5 módulos y 30 artículos que documentan lo que existe en `develop`. Quedan fuera el Asistente conversacional (no está en `develop`) y Aulas (placeholder "en construcción").
- **Tests**: Vitest + Testing Library (integridad del contenido, búsqueda, visibilidad por permisos, navegación). No se agrega Playwright.
- **Docs en el mismo diff**: design spec del Centro de ayuda, `docs/architecture/stack.md`, `README.md`.

No se copia del original: embeds de video, medio "placeholder", `featured` / "Temas frecuentes", `formatArticleCount` ni variantes de componentes sin uso (regla 7 de `AGENTS.md`).

## Capabilities

### New Capabilities

- `centro-de-ayuda`: entrada en la sidebar, rutas, layout de dos sidebars, páginas de inicio, módulo y artículo, scroll-spy, deep links, visor de capturas y redirección de slugs desconocidos.
- `busqueda-ayuda`: búsqueda local sobre el contenido visible, combobox y página de resultados.
- `visibilidad-ayuda-por-permisos`: filtrado de módulos, artículos, búsqueda y relacionados por los permisos de la sesión.
- `contenido-ayuda`: modelo del contenido, reglas de integridad verificadas por test, cobertura de módulos documentados y política de capturas.

### Modified Capabilities

- `navegacion-por-permisos`: la sidebar suma un ítem fijo "Centro de ayuda", ajeno al registro filtrado por permisos, disponible para todo usuario autenticado.
- `barra-superior`: se aclara que el acceso a la ayuda vive en la sidebar; la prohibición de un botón "Ayuda" en la barra superior se mantiene.

## Impact

- **Módulos**: solo frontend (`app/AppLayout.tsx`, `app/router.tsx`, `app/shell/Sidebar.tsx`, `shell.css`, `icons.tsx`, y una feature nueva `features/ayuda`). No toca backend, API, Contracts, schema ni el grafo de dependencias. Sin consumidores cross-module.
- **Frontera de features**: `features/ayuda` no importa otras features. Las cadenas de permiso se validan contra `app/shell/nav.ts` solo en un test.
- **Dependencias**: ninguna nueva. Se usa `@ars-docendi/ui` y `react-router-dom` existentes.
- **Normativa**: no hay reglas institucionales nuevas (sin `BR-*`).
- **Mantenimiento**: el contenido puede quedar desactualizado cuando cambie una pantalla; se mitiga con la tarea de docs y ampliando la regla 9 de `AGENTS.md` para que esos cambios actualicen también el artículo de ayuda.
- **Docs**: `docs/product/designs/centro-de-ayuda-design-spec.md` (nuevo), `docs/architecture/stack.md`, `README.md`.
- **Rollback**: revertir el PR. No hay migraciones ni datos.
