# Proposal

## Why

"Mi portal" se diseñó para escritorio y no se puede usar desde un celular. El shell reserva siempre 200 px a la barra lateral, así que en un teléfono de 375 px el perfil queda en una columna de unos 175 px. Además, el aviso de guardado tiene un ancho fijo de 360 px y se sale de la pantalla. Los [principios de diseño](../../../docs/product/design-principles.md) piden layouts responsivos y contemplan el uso ocasional desde el móvil. El Portal es la pantalla que más sentido tiene abrir desde el teléfono, porque el docente entra a corregir un dato puntual.

## What Changes

- **Shell en pantallas angostas**: por debajo de 768 px de ancho la barra lateral fija desaparece y el contenido ocupa todo el ancho. La navegación se abre como un panel lateral desde el botón "Menú" de la barra superior. La barra superior conserva la marca y el menú de usuario. La línea "Ingeniería · UNLaM" se oculta para dejar lugar.
- **Portal en pantallas angostas**: por debajo de 560 px se reduce el padding de página y de sección, el aviso de guardado ocupa el ancho disponible y los controles táctiles chicos (quitar tag, menú ⋮) tienen un área de toque mínima de 24 × 24 px. Los modales de alta y edición entran en el ancho del teléfono sin scroll horizontal.
- **Botón "Menú" también en escritorio**: el mismo botón colapsa la barra lateral a íconos y la vuelve a expandir. Retoma la versión colapsada que ya existía y cuyo botón estaba comentado. La preferencia se recuerda en el navegador. En el Centro de ayuda la barra arranca en íconos, pero el botón la puede desplegar.
- Fuera del botón, el escritorio no cambia.

El cambio del shell alcanza a todas las pantallas autenticadas, porque el shell es uno solo. Pero este change sólo garantiza que el **Portal** sea usable en móvil. Que el resto de las pantallas (Designaciones, grillas, administración) se adapten queda fuera de alcance.

## Capabilities

### New Capabilities

- `shell-pantallas-angostas`: cómo se presenta el shell autenticado (navegación y área de contenido) cuando el viewport es angosto.

### Modified Capabilities

- `barra-superior`: la barra superior suma el botón "Menú" en cualquier ancho. En pantallas angostas abre la navegación y en escritorio colapsa la barra lateral. Hoy el requisito dice que muestra "únicamente" la marca y el menú de usuario.
- `perfil-docente-portal`: nuevo requisito para que "Mi portal" sea usable en viewports angostos, sin scroll horizontal y con todas sus acciones alcanzables.

## Impact

- Frontend: `app/AppLayout.tsx`, `app/shell/TopBar.tsx`, `app/shell/Sidebar.tsx`, `app/shell/shell.css` y `shared/perfil/components/portal.css`. Como los componentes de `shared/perfil` también los usa el editor de Personas (`features/personas/components/PortalPersona.tsx`), esa vista recibe los mismos ajustes.
- Sin cambios de backend, API, schema ni dependencias. Se reutiliza el `Drawer` que ya trae `@ars-docendi/ui` v1.0.4.
- Documentación: [portal-docente-design-spec.md](../../../docs/product/designs/portal-docente-design-spec.md) suma la sección de pantallas angostas.
- Grafo de dependencias: sin impacto. Rollback: revertir el commit, que sólo toca CSS y el shell del frontend.
