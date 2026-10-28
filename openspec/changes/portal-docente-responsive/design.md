# Design

## Context

- El shell (`app/AppLayout.tsx` + `app/shell/shell.css`) es una grilla `200px 1fr` sin ningún `@media`. Para colapsar la barra existía el estado `collapsed`, pero su botón estaba comentado en `TopBar.tsx`, así que no había ningún control para la navegación. El Centro de ayuda (`centro-de-ayuda`) contrae la barra a íconos bajo `/ayuda` y suma su acceso en el pie de la barra.
- `@ars-docendi/ui` v1.0.4 trae un `Drawer` controlado (`open`, `onOpenChange`, `side="left"`, cierre con Escape y con el fondo) que nadie usa en la app.
- El Portal (`shared/perfil/components/portal.css`) ya es fluido en lo grueso: usa flex, `min-width: 0` y `portal-form-grid` con `auto-fit, minmax(180px, 1fr)`, que en un teléfono pasa sola a una columna. Lo que rompe:
  - `.portal-aviso`, porque el `.adoc-toast` de la librería tiene `width: 360px` fijo y queda corrido 24 px a la derecha.
  - Los paddings de escritorio: `.adoc-main` usa `24px 28px` y las secciones `--space-4`.
  - `.portal-tag-quitar`, con un área de toque de unos 14 px.
- El `Modal` de la librería ya usa `width: 100%; max-width: 520px` en un escenario con 24 px de padding, así que entra en 375 px sin cambios.
- `shared/perfil` también se renderiza en el editor de Personas: los ajustes de `portal.css` aplican a las dos vistas.

## Goals / Non-Goals

**Goals:**

- Cumplir los requisitos de las specs delta en 360–767 px. En escritorio sólo se suma el botón "Menú", que colapsa la barra lateral.
- Resolverlo con CSS y componentes existentes, sin dependencias nuevas.

**Non-Goals:**

- Adaptar Designaciones, las grillas o las pantallas de administración. Con el shell nuevo ganan el ancho completo, pero no se garantiza que queden usables.
- Rediseñar la versión colapsada de 64 px de la barra lateral: se reutiliza tal cual.
- Cambios en `@ars-docendi/ui`.

## Decisions

**D1. Dos breakpoints: 768 px para el shell, 560 px para el Portal.**
Con 768 px o más (tablet en vertical y notebook) la barra lateral entra y deja unos 568 px de contenido, suficientes para el Portal. Debajo de eso la barra le quita demasiado a la pantalla. El Portal sólo se compacta debajo de 560 px, porque recién ahí los paddings de escritorio empiezan a pesar. 560 ya se usa en `designaciones/pages/detalle.css`.
_Alternativa_: un único breakpoint en 768. Se descartó porque compactaría el Portal en tablets, donde no hace falta.

**D2. El panel de navegación usa el `Drawer` de la librería, no una barra lateral desplazada con CSS.**
El `Drawer` ya resuelve el fondo, Escape, el portal al `body` y el `z-index` de modal. Una barra lateral con `transform` habría que reimplementarla con foco, overlay y bloqueo de scroll. Además, cumple con no inventar componentes.
_Alternativa_: reutilizar el `<nav class="adoc-sidebar">` que ya existe y desplazarlo con `position: fixed`. Se descartó porque duplica lo que hace el `Drawer`.

**D3. La lista de navegación se renderiza una sola vez como componente y se monta en los dos contenedores.**
Se extrae el `<ul className="adoc-nav">` de `Sidebar` a un componente `ListaNavegacion`, que recibe `permissions` y un `onNavegar` opcional. `Sidebar` lo monta en la barra fija y `TopBar` lo monta dentro del `Drawer`. Al tocar un enlace se llama a `onNavegar`, que cierra el panel. El filtrado por permisos (`filtrarNavegacion`) sigue siendo único.

**D4. Un solo botón "Menú" que cambia de función según el ancho; el panel vive en `TopBar`.**
El layout lo resuelve CSS: debajo de 768 px la barra fija se oculta con `display: none` y la grilla pasa a una columna `minmax(0, 1fr)` (con `1fr` no achica por debajo del min-content del contenido y estira la página). Lo que hace el botón lo decide `TopBar` con un `matchMedia` del mismo corte, leído con `useSyncExternalStore`: en angosto abre el `Drawer`, y en escritorio llama a `onToggleCollapse`, que colapsa la barra a 64 px. Su `aria-label` y `aria-expanded` siguen la función vigente. El panel sólo se muestra mientras el viewport es angosto, así que si la ventana se agranda con el panel abierto, se cierra solo. `TopBar` ya recibe `user.permissions` y maneja el menú de usuario, así que el panel queda junto al botón que lo abre.
_Alternativa_: dos botones, uno por ancho, y visibilidad sólo con CSS. Se descartó porque son dos controles para "el menú" en el mismo lugar.

**D7. Un único ícono ☰ y transición del ancho de la barra.**
El botón muestra siempre ☰, con la barra desplegada o plegada y en cualquier ancho, porque es el ícono que todos reconocen para el menú. El estado se ve en la barra misma, que transiciona entre 200 y 64 px con `--motion-base` / `--ease-standard`, sólo con `prefers-reduced-motion: no-preference`.
_Alternativas_: cambiar el ícono según el estado (» o el par `menu` / `menu_open`). Se descartaron para mantener un solo ícono conocido.

**D6. La preferencia de barra colapsada se guarda en `localStorage`.**
`AppLayout` la lee al montar y la escribe al alternar, con `try/catch`. Si el almacenamiento no está disponible (modo privado, bloqueado), la preferencia dura sólo la sesión. Es una comodidad por navegador, no un dato del usuario, así que no va al backend.

**D5. Ajustes del Portal acotados a `portal.css`.**
Debajo de 560 px:

- Paddings de `.portal-seccion-head`, `.portal-seccion-body`, `.portal-fila-vacia` y `.portal-fila` en `--space-3`.
- `.portal-aviso` con `left`/`right` en `--space-4`, y `.portal-aviso .adoc-toast` con `width: auto`.
- `.portal-tag-quitar` con `min-width`/`min-height: 24px`, centrado.
- `.adoc-file-row` (CV y documentos) en grilla: el tamaño baja debajo del nombre. Si no, la columna del tamaño deja el nombre en 0 px.

El padding de `.adoc-main` (16 px debajo de 768) se ajusta en `shell.css` porque es del shell. El override del ancho del toast queda limitado a `.portal-aviso` para no alterar otros usos.

**D8. Convivencia con el Centro de ayuda.**
Bajo `/ayuda` la barra arranca en íconos, como define `centro-de-ayuda`. El botón "Menú" la puede desplegar durante esa visita sin tocar la preferencia guardada. Al salir vuelve la preferencia, y cada nueva entrada a `/ayuda` arranca en íconos (ajuste de estado en render comparando contra la ruta previa, sin efectos). Así el botón nunca queda sin efecto. El acceso "Centro de ayuda" se movió a `ListaNavegacion.tsx` como `EnlaceAyuda`, para montarlo en el pie de la barra y al final del panel móvil.
_Alternativa_: ocultar o deshabilitar el botón en `/ayuda`. Se descartó para que el botón no aparezca y desaparezca según la pantalla.

**D9. Sin barra superior en escritorio; usuario al pie de la barra lateral.**
La barra superior sólo tenía el botón, la marca y el menú de usuario, y ocupaba 56 px de alto en todas las pantallas. Con 768 px o más se quita: el botón y la marca van arriba de la barra lateral, y "Centro de ayuda" y la tarjeta de usuario a un pie fijo; la lista de navegación scrollea entre los dos. La tarjeta (`MenuUsuario`) es un botón propio y no el `RoleBadge` de la librería, porque el badge es una píldora de 28 px pensada para una barra horizontal. Muestra el avatar, el nombre corto y el rol, con un chevron que apunta hacia donde se abre el menú. El nombre corto toma el primer nombre y el primer apellido del formato institucional "Apellidos, Nombres"; el completo queda en `title` y en la etiqueta accesible. Debajo de 768 px queda una barra superior fina (48 px) con el botón y la marca, porque ahí no hay barra fija, y la tarjeta va al pie del `Drawer`. La marca se extrae a `MarcaArsDocendi` para usarla en los dos lugares.
_Alternativa_: conservar el `RoleBadge` al pie. Se descartó porque en 200 px corta nombre y rol y no se lee como algo que se puede tocar.

## Risks / Trade-offs

- [Nombres y roles largos ("Administrador del sistema") no entran en la barra] → La línea "Ingeniería · UNLaM" se quitó en todos los anchos. Con D9 el usuario ya no está en la barra superior: la tarjeta del pie muestra el nombre corto en hasta dos líneas y corta el rol con puntos suspensivos, y ambos completos quedan en el texto emergente y en la identidad del Portal.
- [El nombre corto toma la primera palabra de cada parte, así que un apellido con partícula ("De la Fuente") queda como "De"] → Se acepta por ahora. Si aparece en el SGA, el backend debería exponer nombre y apellido por separado.
- [El cambio del shell también se ve en pantallas que no se adaptaron] → Con contenido a ancho completo quedan mejor que hoy. Fuera del Portal no se promete que sean usables.
- [Personas hereda los ajustes de `portal.css`] → Se verifica también el editor de Personas a 375 px.
- [Tres changes en curso (`centro-de-ayuda`, `rediseno-pantalla-ingreso` y este) modifican el mismo requisito de `barra-superior`, y el que se archive último pisa a los demás] → El delta de este change parte del texto de los otros dos y lo reemplaza con D9. `rediseno-pantalla-ingreso` y `centro-de-ayuda` se archivan antes que este, para que este quede último.

- [Los escenarios de `Modal` y `Drawer` de la librería, y `.adoc-app`, miden `100vh`, que en el navegador móvil no descuenta la barra de direcciones ni el teclado: el pie con "Guardar" quedaba fuera de lo visible y el overlay bloquea el scroll de la página] → Se usa `100dvh` (override en `index.css`, registrado como TD-010 para subirlo a ui-lib). La emulación de escritorio no tiene barra de direcciones, así que esto sólo se confirma en un teléfono real.

## Migration Plan

Sólo frontend, sin datos ni configuración. El rollback es revertir el commit.
