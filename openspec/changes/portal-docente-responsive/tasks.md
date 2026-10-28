# Tasks

## 1. Shell: navegación en pantallas angostas

- [x] 1.1 Extraer de `app/shell/Sidebar.tsx` la lista `<ul className="adoc-nav">` a un componente `ListaNavegacion` (`permissions`, `onNavegar` opcional) y montarlo en `Sidebar`. Verificar que `Sidebar.test.tsx` sigue en verde sin cambios.
- [x] 1.2 En `app/shell/TopBar.tsx`, agregar el botón "Menú" (ícono en `icons.tsx`, `aria-label="Abrir menú de navegación"`, `aria-expanded`) y el `Drawer` de `@ars-docendi/ui` con `side="left"`, que monta `ListaNavegacion` con `user.permissions` y se cierra con `onNavegar`. Verificar que `TopBar.test.tsx` sigue en verde.
- [x] 1.3 Agregar en `TopBar.test.tsx` el flujo de spec "Abrir el menú y navegar": tocar "Menú", elegir un ítem, verificar que navega y que el panel se cierra. Verificar con `pnpm --filter frontend test:run`.
- [x] 1.4 En `app/shell/shell.css`, debajo de 768 px: grilla de una columna, `.adoc-sidebar` oculta, botón "Menú" visible (oculto con 768 px o más), padding de `.adoc-main` en 16 px. Verificar en el browser a 375, 767, 768 y 1280 px que escritorio no cambia y que en móvil no hay barra lateral fija.

## 2. Portal en pantallas angostas

- [x] 2.1 En `shared/perfil/components/portal.css`, debajo de 560 px: paddings de sección y fila en `--space-3`, `.portal-aviso` con `left`/`right` en `--space-4` y `.portal-aviso .adoc-toast { width: auto }`, y área de toque mínima de 24 × 24 px en `.portal-tag-quitar`. Verificar `/portal` a 360 y 375 px: sin scroll horizontal (`document.documentElement.scrollWidth <= innerWidth`), con todas las acciones alcanzables y el aviso de guardado dentro de la pantalla.
- [x] 2.2 Verificar a 375 px los modales de Experiencia, Educación, Certificaciones, Proyectos y Contacto, la carga de CV y la edición de tags. Ajustar en `portal.css` cualquier desborde que aparezca.
- [x] 2.3 Verificar el editor de Personas (`PortalPersona`) a 375 px, ya que comparte `portal.css`.
- [x] 2.4 Agregar la sección "Pantallas angostas" a `docs/product/designs/portal-docente-design-spec.md` con los breakpoints, el panel de navegación y los ajustes del Portal. Verificar con `pnpm format:check` y `node scripts/docs/check-links.mjs`.

## 3. Botón "Menú" en escritorio

- [x] 3.1 En `TopBar.tsx`, mostrar el botón en cualquier ancho. En angosto abre el panel; en escritorio llama a `onToggleCollapse`, con `aria-label` "Colapsar/Expandir menú lateral" y `aria-expanded`. Quitar el botón de colapso comentado, `collapseIcon` y el CSS `.adoc-collapse-btn`. Verificar en el browser a 1280 px que colapsa a íconos y vuelve.
- [x] 3.2 En `AppLayout.tsx`, recordar la preferencia en `localStorage` con `try/catch`. Verificar que tras colapsar y recargar la barra arranca colapsada, y que a 375 px el panel muestra los nombres completos.
- [x] 3.3 En `TopBar.test.tsx`, cubrir el colapso en escritorio (llama a `onToggleCollapse` y no abre el panel) y simular el viewport angosto en el test del panel. Verificar con `pnpm --filter frontend test:run`.
- [x] 3.4 Animar el ancho de la barra al plegar y desplegar, sólo sin "reducir movimiento", con el ícono ☰ fijo. Verificar en el browser que el ícono no cambia y que la barra transiciona.
- [x] 3.5 Actualizar la sección "Pantallas angostas" de `portal-docente-design-spec.md` con el botón en escritorio.

## 4. Integración con develop (Centro de ayuda y marca nueva)

- [x] 4.1 Mergear `develop`. En `AppLayout.tsx`, combinar la contracción bajo `/ayuda` con la preferencia guardada: en `/ayuda` el botón despliega la barra sólo durante esa visita. Cubrirlo en `AppLayout.test.tsx` y verificar que pasan los tests de contracción por ruta.
- [x] 4.2 Mover `EnlaceAyuda` a `ListaNavegacion.tsx`, montarlo en el pie de la barra y al final del panel móvil, y verificar en `TopBar.test.tsx` que el panel lo ofrece.
- [x] 4.3 Conservar en `TopBar.tsx` la marca con isotipo y enlace a Mi portal junto al botón "Menú", y verificar el test de la marca.
- [x] 4.5 Debajo de 768 px, achicar el isotipo de la marca a 28 px para que la barra superior entre en 360 px, y subir la especificidad de `.adoc-sidebar-help` para que `.adoc-ui a` no lo pinte como enlace de texto. Verificar en el browser a 360 y 1280 px.
- [x] 4.4 Incluir en el delta de `barra-superior` el texto y los escenarios de `centro-de-ayuda` y `rediseno-pantalla-ingreso`. Verificar con `openspec validate --all`.

## 5. Ajustes tras probar en el teléfono

- [x] 5.1 Ocultar también el rol dentro del `RoleBadge` debajo de 768 px. Verificar a 360 px con el rol "Administrador del sistema" que la barra entra y que el menú de usuario abre dentro de la pantalla.
- [x] 5.2 Usar `100dvh` en los escenarios de `Modal` y `Drawer` (override en `index.css`, TD-010) y en `.adoc-app`, para que el pie con "Guardar" no quede fuera del alto visible del navegador móvil. Verificar a 375 × 560 que el cuerpo del diálogo scrollea hasta el final con "Guardar" visible.

- [x] 5.3 Quitar la línea "Ingeniería · UNLaM" de la barra superior en todos los anchos, con su CSS, y la mención en el artículo de ayuda "Cómo recorrer la pantalla". Verificar a 1280 y 360 px.

## 6. Verificación integral

- [x] 6.1 Correr `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build` y `pnpm exec openspec validate --all`, todos en verde.
- [x] 6.2 Revisar a 360 px la barra superior con un nombre de usuario largo. Desbordaba, así que debajo de 768 px se ocultan el nombre y el rol dentro del `RoleBadge` (ver design). Verificado también con el rol "Administrador del sistema".

## Workflow follow-up

- Archivar el change una vez mergeada la PR a `develop`.
