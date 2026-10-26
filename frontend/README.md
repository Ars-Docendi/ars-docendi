# Frontend

SPA de Ars Docendi (React 19 + TypeScript + Vite). Setup y comandos en el [README raíz](../README.md); convenciones en [docs/architecture/frontend-remote-data.md](../docs/architecture/frontend-remote-data.md) y en la skill `react-features-guide`.

- `pnpm --filter frontend dev`: servidor de desarrollo; proxea `/api` a `http://localhost:5000`.
- `pnpm --filter frontend test:run`, `lint` y `build`: verificación. El build falla si el runtime importa mocks (`scripts/check-no-runtime-mocks.mjs`).
