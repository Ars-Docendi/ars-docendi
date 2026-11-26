# Tasks

## 1. Tema

- [x] 1.1 `shared/tema/tema.ts`: leer, guardar y aplicar la preferencia; seguir al sistema en vivo. Script en `index.html` que aplica el tema antes del primer pintado.
- [x] 1.2 Selector Claro / Oscuro / Sistema en el menú de usuario, con objetivos de 44 px en el teléfono. Test de Vitest del flujo.
- [x] 1.3 Paleta oscura y reemplazo de los blancos fijos de `@ars-docendi/ui` en `tema-oscuro.css`; colores fijos de la app a tokens. TD-033 registrado.
- [x] 1.4 Paso nuevo en el artículo de ayuda «Cómo recorrer la pantalla» (regla 9 de AGENTS.md).

## 2. Cierre

- [x] 2.1 Correr `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check`, `node scripts/docs/check-links.mjs` y `pnpm exec openspec validate --all`.
- [ ] 2.2 Revisión visual en desktop y teléfono, en claro y oscuro, de las pantallas principales.
