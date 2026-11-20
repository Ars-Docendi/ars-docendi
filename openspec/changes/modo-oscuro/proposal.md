## Why

El sistema sólo se ve en claro. Quien trabaja de noche o con el dispositivo en modo oscuro recibe una pantalla blanca que encandila, y el celular (PWA instalada) contrasta con el resto del sistema operativo. `@ars-docendi/ui` trae un `[data-theme="dark"]`, pero nada lo activa y no es usable tal cual: invierte la escala neutral paso por paso, así los fondos de hover con `--neutral-200` quedan casi blancos bajo texto claro, y ~25 componentes fijan `background: #fff`.

## What Changes

- **Preferencia de tema** en el menú de usuario (barra lateral y panel móvil): Claro, Oscuro o Sistema. Sistema es el valor por defecto y sigue `prefers-color-scheme` en vivo. Se guarda por navegador en `localStorage` (`ars-docendi:tema`) y se aplica antes del primer pintado para no destellar en claro, también en la pantalla de ingreso.
- **Paleta oscura** propia en `frontend/src/tema-oscuro.css`: mismo tono cálido y croma que el claro, conservando el ROL de cada paso de la escala (100–300 fondos de interacción, 400 bordes, 600–800 texto, 900 velo de overlays) y el verde institucional con más luminosidad para sostener contraste.
- **Blancos fijos**: los de `@ars-docendi/ui` se reemplazan por `--color-bg-raised` con la misma especificidad que la regla original (sus estados siguen ganando); los de la app pasan a tokens.

## Capabilities

### New Capabilities

- `tema-de-la-interfaz`: elección, persistencia y aplicación del tema claro u oscuro.

### Modified Capabilities

## Impact

- Frontend solamente (`index.html`, `main.tsx`, `shared/tema`, `app/shell`, CSS con colores fijos). Sin backend, API, schema ni reglas de negocio.
- `@ars-docendi/ui` no se modifica: el override queda registrado como TD-033 para subirlo a ui-lib.
- Centro de ayuda: paso «Elegí el tema claro u oscuro» en «Cómo recorrer la pantalla».
