# Proposal

## Why

La pantalla de ingreso no comparte la identidad visual del sistema: en lugar de la marca de Ars Docendi usa un recuadro con el texto "UNLaM", y su descripción omite el asistente conversacional. Además, su copy pide una "cuenta institucional" aunque el ingreso acepta cualquier cuenta Microsoft dada de alta, y remite a `soporte.adocendi@unlam.edu.ar`, una casilla que no existe. Si alguien tiene problemas para entrar, termina escribiendo a una dirección que nadie lee.

## What Changes

- El panel izquierdo muestra el isotipo de Ars Docendi (la "A" y la "D" con un birrete) en lugar del recuadro "UNLaM".
- El panel izquierdo pasa a ser una bienvenida en el verde de la marca ("Te damos la bienvenida a Ars Docendi" y qué se puede hacer), y el formulario de la derecha se rediseña con la marca arriba.
- El favicon deja de ser el logo por defecto de Vite y pasa a ser el isotipo de Ars Docendi.
- La pantalla se adapta a tablet y celular: el panel pasa a una banda compacta y el botón de ingreso queda a la vista sin scroll.
- Se reescribe el copy de la pantalla (encabezado, bajada, botón, ayuda y descripción del sistema) para que coincida con el ingreso real con Microsoft. La descripción suma el asistente conversacional.
- El texto de condiciones de uso se reemplaza por un pie con enlace a una página pública nueva de términos de uso y privacidad, con lo que exige la Ley 25.326 para un sistema que guarda datos personales y documentos de los docentes.
- Se elimina el bloque de ayuda con el mail de soporte, que no existe. El error de autenticación remite a Secretaría Académica, igual que el acceso denegado.
- Se elimina la versión `v0.1.1`, escrita a mano y desactualizada, del pie del panel.
- No cambian el flujo de ingreso, los estados, el selector de desarrollo ni el backend.

## Capabilities

### New Capabilities

- `pantalla-ingreso`: contenido visible y presentación institucional de la pantalla de login (identidad, textos y términos de uso y privacidad), independiente del mecanismo de autenticación.

### Modified Capabilities

<!-- Ninguna: `inicio-sesion-microsoft` no cambia; su texto de acceso denegado ya remite a Secretaría Académica. -->

## Impact

- Frontend: `frontend/src/shared/auth/LoginPage.tsx` y `LoginPage.css`, más el componente nuevo `IsotipoArsDocendi.tsx` con el isotipo vectorizado, la página `frontend/src/shared/legal/TerminosPage.tsx` y la ruta pública `/terminos` en `router.tsx` y `frontend/public/favicon.svg`.
- Tests: `sesionMicrosoft.test.tsx` y `developmentAuth.test.tsx` buscan el botón por su texto actual y hay que actualizarlos.
- Módulos backend, APIs, schema y grafo de dependencias: sin impacto.
- Rollback: revertir el commit. El cambio es sólo de presentación.
