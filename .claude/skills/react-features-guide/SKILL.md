---
name: react-features-guide
description: Convenciones React + Vite para features de Ars Docendi — features aisladas en src/features/<x>/, código común en src/shared/, React Query para datos del servidor, cliente HTTP único, sin cross-imports entre features.
paths:
  - "frontend/src/**"
user-invocable: false
---

# React features guide

Path-scoped: se auto-activa al tocar `frontend/src/`. La estructura vigente es la del código; antes de crear archivos, imitá una feature existente (`features/tareas/` o `features/designaciones/`). Las convenciones de datos remotos están en [frontend-remote-data.md](../../../docs/architecture/frontend-remote-data.md).

## Reglas duras

- **Aislamiento**: una feature no importa de otra. Lo común sube a `src/shared/`.
- **HTTP**: sólo `shared/api/client.ts` crea la instancia de Axios; las features exponen funciones tipadas en `features/<x>/api/` y no la reinstancian.
- **Datos del servidor**: siempre React Query (hooks en `features/<x>/hooks/`). Nunca `useEffect` + fetch.
- **Rutas**: cada feature exporta las suyas en `features/<x>/routes.tsx` y `app/router.tsx` las compone.
- **Estados visibles**: carga, vacío, error y éxito en toda vista que consulta datos ([design-principles](../../../docs/product/design-principles.md)).
- **Autorización**: la UI oculta acciones según los `permisos` de la sesión (`/api/auth/sesion`, `useCurrentUser`) o las `accionesPermitidas` del recurso. Es UX, no seguridad: el backend valida siempre.
- **Sin datos locales de negocio**: nada de mocks ni arrays de registros en runtime. El build lo verifica (`check-no-runtime-mocks.mjs`).
- **Componentes**: se usa `@ars-docendi/ui` y `shared/ui/` antes que crear primitivos nuevos.
- **Tests**: Vitest + Testing Library. Los DTOs se toman de `src/test/contratos/` y se aplican los criterios de [golden-principles → Tests](../../../docs/quality/golden-principles.md#tests).

## Comandos

```bash
pnpm --filter frontend dev
pnpm --filter frontend test:run
pnpm --filter frontend lint
pnpm --filter frontend build
```
