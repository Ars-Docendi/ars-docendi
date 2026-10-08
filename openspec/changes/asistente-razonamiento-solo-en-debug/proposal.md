## Why

The assistant's reasoning disclosure ("Cómo lo interpreté") currently renders for every
end user whenever the backend sends `respuesta.razonamiento`. The product owner decided
this disclosure must be visible only in debug mode, gated by a frontend environment
variable. The backend keeps sending `razonamiento` in every response either way — that is
an accepted, explicit trade-off: the field stays visible in devtools/network responses,
and Vite inlines env vars at build time (per-environment, not per-user). Frontend-only
gating was chosen deliberately over a backend change.

## What Changes

- Add a frontend-only debug flag, `VITE_ASISTENTE_DEBUG` (`"true"` enables it, default
  off). Unlike `VITE_DEVELOPMENT_AUTH_ENABLED`, this flag does NOT turn on automatically
  under `import.meta.env.DEV` — it must be explicitly opted into even in a dev build.
- `Razonamiento` (and its caller, `Mensaje`) render the "Cómo lo interpreté" disclosure
  only when that flag is on. When it is off, the disclosure never mounts, regardless of
  whether `respuesta.razonamiento` is present.
- Out of scope: the "Entendí: …" interpreted-question line and "Ver la consulta" (already
  gated by the `asistente.ver_consulta` permission) are untouched.
- Out of scope: no backend change. `razonamiento` keeps traveling in the HTTP response.

## Capabilities

### New Capabilities

- `asistente-conversacion`: the existing capability `asistente-conversacion` has not been
  archived into `openspec/specs/` yet (it is still pending in the unarchived change
  `asistente-rediseno-conversacion`). Because `openspec instructions` treats any path
  absent from `openspec/specs/` as a _new_ capability, this change's delta spec file uses
  `## ADDED Requirements` at `specs/asistente-conversacion/spec.md` instead of
  `## MODIFIED Requirements`. A `MODIFIED` delta against a non-existent main spec would be
  refused at archive time. The added requirement narrows — not replaces — the pending
  requirement "El razonamiento se muestra colapsado cuando viene" from
  `asistente-rediseno-conversacion`: once both changes are archived (in either order),
  the two requirements coexist as complementary facts about the same disclosure (shown
  collapsed when it arrives AND only in debug mode).

### Modified Capabilities

(none — see note above)

## Impact

- `frontend/src/features/asistente/components/Razonamiento.tsx` and `Mensaje.tsx`: gate
  the disclosure behind a debug flag read via a new pure resolver,
  `frontend/src/features/asistente/utils/modoDebug.ts` (mirrors the pattern in
  `frontend/src/shared/auth/developmentAuth.ts`), injected as a prop so component tests
  don't depend on `import.meta.env`.
- `frontend/src/features/asistente/Mensaje.test.tsx`: tests asserting the disclosure now
  mount with the debug flag explicitly on; a new test covers razonamiento present + debug
  off → no disclosure.
- Docs: `frontend/README.md` (or root `README.md`, whichever documents `VITE_` vars),
  `docs/architecture/domains/asistente.md`, and
  `docs/product/designs/asistente-conversacional-design-spec.md` gain a note that the
  disclosure is debug-only and how to enable it.
- No backend, API, schema, or dependency-graph impact.
