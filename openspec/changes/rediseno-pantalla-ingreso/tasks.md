# Tasks

## 1. Identidad visual del panel

- [x] 1.1 Vectorizar el isotipo de Ars Docendi en `IsotipoArsDocendi.tsx` y reemplazar con él el recuadro "UNLaM" de `Marquee`; verificar en el navegador que se ve el isotipo en blanco
- [x] 1.2 Rehacer el layout: panel de bienvenida con degradé verde liso e isotipo blanco; formulario con la marca arriba y el botón primario verde; verificar en el navegador, en ambos temas, que se ve como en `design.md`
- [x] 1.3 Ajustar el responsive: banda con isotipo y bienvenida hasta 960 px y banda mínima hasta 480 px; verificar a 375, 768 y 1440 px que no hay scroll horizontal y que el botón de ingreso queda a la vista
- [x] 1.4 Reemplazar `frontend/public/favicon.svg` por el isotipo en el verde de marca; verificar que el navegador lo muestra en la pestaña

## 2. Copy, ayuda y condiciones de uso

- [x] 2.1 Aplicar en `LoginPage.tsx` el copy de la tabla de `design.md` (crest, bajada, título, botón, error y condiciones de uso) y eliminar el bloque de ayuda con el `mailto` de soporte; verificar que la pantalla no contiene "cuenta institucional", "Soporte", direcciones de mail ni enlaces a términos
- [ ] 2.2 Actualizar `sesionMicrosoft.test.tsx` y `developmentAuth.test.tsx` al rótulo "Ingresar con Microsoft" y cubrir con tests de render los escenarios de `specs/pantalla-ingreso/spec.md`; verificar con `pnpm --filter frontend test:run`

## 3. Términos de uso y privacidad

- [x] 3.1 Crear `frontend/src/shared/legal/TerminosPage.tsx` con las secciones de `specs/pantalla-ingreso/spec.md` y registrar la ruta pública `/terminos` en `router.tsx`; verificar en el navegador que se lee sin sesión
- [x] 3.2 Reemplazar la nota de condiciones del login por un pie con enlace a `/terminos`; verificar que el enlace navega a la página
- [ ] 3.3 Hacer revisar el texto por el área legal de la UNLaM y completar domicilio, inscripción registral y plazos de conservación; verificar la leyenda de la AAIP contra el texto vigente

## 4. Verificación

- [ ] 4.1 Correr `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check` y `pnpm exec openspec validate --all --strict`, y verificar que pasan
- [ ] 4.2 Revisar la pantalla en el navegador (escritorio y ancho móvil) en los estados normal, error, acceso denegado y redirigiendo, y adjuntar capturas al PR
