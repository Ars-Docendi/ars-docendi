# Gates transversales ejecutados

## Tooling de migraciones

`node --test scripts/tests/migrations.test.mjs`: 38 descubiertas/aprobadas, cero fallos/omitidas.

`node scripts/migrations/validate.mjs --base-ref f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b`: éxito, `proteccionGit: true`. Esta es la referencia anterior fijada en los artefactos aprobados. El inventario del candidato evoluciona mientras se agregan incrementales; debe repetirse al consolidar.

La validación contra `origin/develop` abortó porque esa referencia no contiene manifiesto y necesita un corte revisado explícito. No se sustituyó por HEAD ni se generó un nuevo manifiesto como bypass. El control de CI contra su base/corte aprobado permanece separado del control local contra la versión anterior auditada.

## OpenSpec

`openspec validate corregir-integridad-y-rendimiento-post-auditoria --strict --json`: change aprobado, cero issues.

Ejecución histórica previa a la consolidación final: `openspec validate --all --strict --json` reportó 60 items, 28 aprobados y 32 fallidos por advertencias de requisitos de más de 500 caracteres. `git diff --name-only -- openspec/specs` no produjo rutas: las specs principales no fueron modificadas por ese apply. **Esta salida quedó sustituida** por la revalidación strict final del 14-oct-2026, documentada abajo, que informó 60/60 válidos y cero fallos.

## Gate final del 14-oct-2026

- Build Release de IntegrationTests: exit 0, 0 warnings/errores; VSTest con adapter explícito: **421/421 PASS**, cero fallos/omisiones/errores/abortos/timeouts/no ejecutados. TRX: `.artifacts/apply-post-auditoria/final-gate/results/backend-full-post-auditoria.trx`.
- Frontend completo: **403/403 PASS en 58 archivos**; ESLint, build, `pnpm format:check` y `git diff --check` terminaron exit 0.
- DAG de proyectos: 13 proyectos, 27 referencias, cero ciclos/referencias sin resolver/dependencias internas cruzadas entre módulos; `ArquitecturaIdentityTests` PASS.
- OpenSpec strict del change y global: el primero válido; `validate --all --strict --json` reportó **60/60 items válidos, cero fallos**. Esto reemplaza la observación histórica previa de 32 advertencias.
- Migraciones: tooling 38/38; validador 24 migraciones/67 archivos con protección Git; cero diferencias en 37 SQL/wrappers históricos. El ensayo del candidato está en `upgrade-implementacion.md`.

El resultado de compilación diagnóstica `MSB1009` con ruta relativa fue un incidente histórico de cwd compartido, no una falla de código ni una prueba ejecutada; no afecta estos gates posteriores.
