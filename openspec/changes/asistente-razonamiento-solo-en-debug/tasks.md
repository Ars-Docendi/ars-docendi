## 1. Debug flag resolver

- [x] 1.1 Add `frontend/src/features/asistente/utils/modoDebug.ts`: a pure resolver
      function (`resolverModoDebugAsistente`) plus an exported `modoDebugAsistente`
      constant read from `import.meta.env.VITE_ASISTENTE_DEBUG`, mirroring the pattern in
      `frontend/src/shared/auth/developmentAuth.ts`. Default off; do NOT fall back to
      `import.meta.env.DEV`.
- [x] 1.2 Unit test the resolver directly (no `import.meta.env` mocking needed since it's
      a pure function of its arguments).

## 2. Gate the disclosure

- [x] 2.1 `Razonamiento.tsx`: accept a `debug` prop; return `null` when `debug` is false or
      `razonamiento` is missing.
- [x] 2.2 `Mensaje.tsx`: accept an optional `debug` prop defaulting to
      `modoDebugAsistente`; pass it to `Razonamiento`; adjust the "pie" block's mount
      condition so it doesn't render an empty disclosure slot when debug is off and there
      is nothing else (sql/copy actions) to show.
- [x] 2.3 Update `Mensaje.test.tsx`: tests asserting the disclosure now mount with
      `debug: true`; add a test for razonamiento present + debug off → no disclosure.
      Leave "Entendí: …" and "Ver la consulta" assertions untouched.

## 3. Docs

- [x] 3.1 Document `VITE_ASISTENTE_DEBUG` (name, default, effect, needs a Vite
      rebuild/restart to take effect) in whichever README documents `VITE_` vars.
- [x] 3.2 Update `docs/architecture/domains/asistente.md` and
      `docs/product/designs/asistente-conversacional-design-spec.md` where "Cómo lo
      interpreté" is described, noting it is debug-only.

## 4. Verification

- [x] 4.1 `pnpm --filter frontend test:run`
- [x] 4.2 `pnpm --filter frontend lint`
- [x] 4.3 `pnpm --filter frontend build`
- [x] 4.4 `pnpm exec prettier --check` on touched files
- [x] 4.5 `pnpm exec openspec validate asistente-razonamiento-solo-en-debug --strict`
