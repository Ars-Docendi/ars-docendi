# Technical debt tracker

Brechas conocidas, atajos intencionales y follow-ups. Linkear a planes y specs cuando sea posible. Toda deuda con dueño + fecha.

| ID     | Área        | Severidad | Resumen       | Remediación | Owner | Abierto    |
| ------ | ----------- | --------- | ------------- | ----------- | ----- | ---------- |
| TD-001 | _(ejemplo)_ | low       | _(una línea)_ | _(acción)_  | —     | YYYY-MM-DD |

**Severidad:** `low` | `medium` | `high` | `blocker`

**Áreas frecuentes:** backend, frontend, infra, security, tests, docs, compliance.

## Reglas

- NO usar este archivo para esconder **stub UX**; eso falla evaluation o se arregla.
- Cerrar items moviendo a la sección "Resuelto" con fecha de cierre.
- Items `blocker` no pueden quedar abiertos al cerrar un PR a `main`.

## Resuelto

- **Runner de tests del frontend (TBD)** — el frontend no tenía runner configurado (gap conocido, citado en `react-features-guide`). Bootstrapeado con Vitest + Testing Library (`@testing-library/react` + `jest-dom` + `user-event`) + jsdom + `@vitest/coverage-v8`; scripts `test` / `test:run` en `frontend/package.json`; config en `frontend/vite.config.ts` (`test`) + setup en `src/test/setup.ts`. Cerrado 2026-06-20 (change `proyecto-docente-pedidos`, Fase 0).
