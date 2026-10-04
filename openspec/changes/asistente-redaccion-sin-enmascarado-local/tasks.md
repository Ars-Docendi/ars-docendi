## 1. Option and effective-state predicate

- [x] 1.1 Add `RedaccionSinEnmascarar` (bool, default `false`) to `OpcionesAsistente` with its XML doc; verify `dotnet build backend/ArsDocendi.slnx` passes.
- [x] 1.2 Add the single predicate "in effect" (option ∧ provider is `local` ∧ no cassette directory) next to the options (design D2); verify with a new unit test covering the four combinations in `ValidacionDeOpcionesTests`.
- [x] 1.3 Log the effective state once at startup, and a warning when the option is set but not in effect (design D6); verify with a test that captures the log for `local`, `anthropic` and `local` + cassette directory.

## 2. Masking mode

- [x] 2.1 Write the failing tests first in `EnmascaradorTests`: with the "suppress free text only" mode a `sensible-valor` value survives, a `sensible-texto` column disappears with its name, and a public-only result returns the same instance.
- [x] 2.2 Add the mode to `Enmascarador.Enmascarar` keeping it a pure function (design D3); verify the tests of 2.1 pass and every existing `EnmascaradorTests` case still passes unchanged.

## 3. Redaction step

- [x] 3.1 Write the failing turn-level tests in `EnmascaramientoDelTurnoTests`: option in effect → the document is in the redaction prompt; option with `anthropic` → it is not; option with a cassette directory → it is not; option in effect → the history comment (`sensible-texto`) is still absent.
- [x] 3.2 Make the redaction step of `CarrilSql` pick the masking mode from the predicate of 1.2, keeping the real rows in the turn result; verify the tests of 3.1 pass.
- [x] 3.3 Verify access control is untouched: `Un_actor_acotado_que_pregunta_por_telefonos_no_los_recibe` passes with the option in effect (add the option to that test's configuration as a second case).
- [x] 3.4 Verify the turn log carries no row value with the option in effect: extend `El_log_del_turno_no_contiene_valores_de_filas` with a case that has the option on.
- [x] 3.5 Verify the template path follows the same result (design D7): a one-column `sensible-valor` result with `RedaccionConPlantillas` states the real value when the option is in effect and the marker otherwise.

## 4. Specs and documentation

- [x] 4.1 Reword the requirement "Ninguna columna `sensible-valor` llega al modelo con su valor real" in `openspec/changes/asistente-enmascaramiento/specs/asistente-enmascarador/spec.md` to name this option as its only exception; verify `pnpm exec openspec validate --all --strict` passes.
- [x] 4.2 Add the option to the configuration table and to the "Optimizaciones para un modelo propio" section of `backend/src/Modules.Asistente/README.md`, and state the exposure and the `--cache-ram 0` precondition in "La frontera de salida"; verify `OpcionesDocumentadasTests` passes.
- [x] 4.3 Record the option, the measurement and the "not a performance gain" conclusion in `docs/architecture/modelo-local.md` §6, and the boundary exception in `docs/architecture/domains/asistente.md`; verify `pnpm format:check` passes.
- [x] 4.4 List the option in `.env.example`, commented out and set apart from the 3070 block so uncommenting the profile does not turn it on (user's decision); verify `pnpm format:check` passes.

## 5. Verification

- [x] 5.1 Run `dotnet test --solution backend/ArsDocendi.slnx` and confirm it is green, including `ArquitecturaAsistenteTests`.
- [x] 5.2 Run the redaction comparison through the real SQL lane (module container, eval database, local model) with the option on and off, and record in `docs/architecture/modelo-local.md` that the narrated values match the rows. Done through the module container rather than HTTP: the eval database has no dev-auth seed table, so the Host answers 500 to authenticated calls there. The Host was booted in both modes and its startup line read.
