---
name: react-features-guide
description: Convenciones React + Vite para features de Ars Docendi — features aisladas en src/features/<x>/, código común en src/shared/, React Query para data, axios compartido para HTTP, react-router-dom para routing, sin cross-imports entre features.
paths:
  - "frontend/src/**"
user-invocable: false
---

# React features guide

Path-scoped: se auto-activa al tocar paths frontend del proyecto.

## Layout

```
frontend/src/
├── app/                            # Router + composición de páginas
│   ├── routes.tsx                  # rutas top-level
│   └── AppLayout.tsx
├── shared/                         # CÓDIGO COMÚN — primitivos sin lógica de dominio
│   ├── api/                        # axios instance + tipos de error compartidos
│   ├── auth/                       # Azure AD MSAL integration
│   ├── ui/                         # componentes UI primitivos (Button, Card, Modal)
│   └── hooks/                      # hooks generales (useDebounce, etc.)
└── features/                       # FEATURES por módulo backend
    ├── designaciones/
    │   ├── index.ts                # exports públicos del feature
    │   ├── api/                    # llamadas axios al backend
    │   ├── components/
    │   ├── hooks/                  # React Query hooks (useDesignaciones, useAprobar)
    │   ├── pages/                  # páginas top-level del feature
    │   └── routes.tsx              # subrutas del feature
    ├── aulas/...
    ├── portal/...
    └── tareas/...
```

## Reglas duras

### Aislamiento de features

- **Las features NO se importan entre sí**.
  - `features/designaciones/` NO importa de `features/aulas/`.
- Lo que se necesita compartir sube a `shared/`.
- Si dos features necesitan lo mismo, probablemente debería estar en `shared/` o derivar del backend (un endpoint en el módulo correcto).

### HTTP

- **Un solo axios instance** compartido en `shared/api/`.
- Configuración (baseURL, interceptors, auth header) centralizada.
- Features importan ese instance, NO crean axios ad-hoc.

```typescript
// shared/api/client.ts
import axios from "axios";
export const api = axios.create({ baseURL: "/api" /* + interceptors */ });

// features/designaciones/api/designaciones.ts
import { api } from "@/shared/api/client";
export const getDesignaciones = () => api.get<Designacion[]>("/designaciones");
```

### Data fetching

- **React Query** (`@tanstack/react-query`) siempre para datos del servidor.
- **Nunca** `useEffect` + fetch manual para datos remotos.
- Hooks por feature: `features/<x>/hooks/use<X>.ts`.

```typescript
// features/designaciones/hooks/useDesignaciones.ts
import { useQuery } from "@tanstack/react-query";
import { getDesignaciones } from "../api/designaciones";

export const useDesignaciones = () =>
  useQuery({
    queryKey: ["designaciones"],
    queryFn: () => getDesignaciones().then((r) => r.data),
  });
```

### Routing

- **react-router-dom 7**. Rutas top-level en `app/routes.tsx`.
- Cada feature exporta sus subrutas en `features/<x>/routes.tsx`.
- `app/routes.tsx` compone las subrutas.

### Estados visibles obligatorios

Toda página/componente que carga datos debe manejar:

- **Loading**
- **Empty** (sin datos)
- **Error**
- **Success** (estado normal)
- **Awaiting approval** (cuando aplique al workflow)

(Ver `docs/product/design-principles.md`.)

### Autorización por rol (UI)

- Mostrar/ocultar acciones según rol (info del token Azure AD).
- **NO confiar solo en UI** — backend valida con `[Authorize]`. UI esconde por UX, no por seguridad.

### TypeScript

- `strict: true` (chequear `tsconfig.json`).
- DTOs del backend tipados (idealmente generados desde OpenAPI / Swagger en el futuro; por ahora manuales en `features/<x>/api/types.ts`).

### Naming

- Componentes: PascalCase (`DesignacionCard.tsx`).
- Hooks: `use<X>` camelCase.
- Files de páginas: PascalCase con sufijo `Page` (`DesignacionesPage.tsx`).

## Comandos clave

```bash
# Dev server
pnpm --filter frontend dev

# Build
pnpm --filter frontend build

# Lint
pnpm --filter frontend lint

# Lint --fix (también lo hace pre-commit)
pnpm --filter frontend lint -- --fix

# Test runner: TBD (no configurado todavía, gap conocido)
```

## Anti-patterns

- Cross-import entre features.
- axios ad-hoc en un feature (usar el del shared).
- `useEffect` + fetch para datos del servidor.
- Stub components que parecen completos sin lógica real.
- Mostrar errores con stacktrace al usuario final.

## Docs relevantes

- [docs/product/design-principles.md](../../../docs/product/design-principles.md)
- [docs/quality/golden-principles.md](../../../docs/quality/golden-principles.md)
