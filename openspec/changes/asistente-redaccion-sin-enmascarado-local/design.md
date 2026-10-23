## Context

See `proposal.md` — Why. What shapes the approach:

- Masking is applied in exactly one place: the redaction step of the SQL lane (`CarrilSql`, `RedactadoAsync`), which calls the pure function `Enmascarador.Enmascarar` and hands the masked result to the redactor. The real rows travel to the caller untouched. SQL generation, the rewriter and the repair round never see rows.
- The manifest classifies 8 of ~170 readable columns: 5 `sensible-valor` (`documento`, `cuil`, `fecha_nacimiento`, `telefono`, `upn`) and 3 `sensible-texto` (`pedidos.justificacion`, `pedidos.tipo_baja_detalle`, `pedido_historial.comentario`).
- The provider key `local` means "an OpenAI-compatible server at `UrlDelProveedorLocal`". The code cannot prove that URL is on the Department's own hardware.
- The cassette recorder is attached to the provider HTTP client whenever `DirectorioDeCassettes` is set, for any provider, and stores raw response bodies that are meant to be committed.
- The narrated text is not persisted: the history stores the question and the resolved SQL, and the conversational thread keeps no answer text.
- `RedaccionConPlantillas` builds the sentence from the same result the redactor receives.

## Goals / Non-Goals

**Goals:**

- One boolean, off by default, with a single place that decides whether it is in effect.
- A decision that is testable without a model: the assertion is on the prompt, not on what the model writes.
- No way for the option to send a real value to a non-local provider or into a cassette.

**Non-Goals:**

- Faster or cheaper turns. Measured: unmasking is slower (see proposal).
- Unmasking `sensible-texto` columns.
- Verifying that `UrlDelProveedorLocal` points at on-premises hardware.
- Fixing the redaction that hits its 300-token ceiling on 24 rows (it happens masked and unmasked alike; separate change).
- Adding evaluator items that touch sensitive columns (would invalidate the frozen baselines; separate change).

## Decisions

### D1 — One option, `RedaccionSinEnmascarar`, default `false`

It sits with its siblings (`RedaccionConPlantillas`, `StreamingDeRedaccion`) in `OpcionesAsistente` and follows their naming.

_Alternative considered:_ deriving the behavior from `Proveedor == local` with no option. Rejected: it would change the prompt of every existing local deployment silently, and the exposure in D5 deserves an explicit operator decision.

### D2 — "In effect" is computed once: option ∧ provider is `local` ∧ no cassette directory

A single predicate, evaluated from the options, is the only thing the redaction step consults. Anything else masks.

_Alternative considered:_ failing startup when the option is `true` with another provider, in line with `ValidadorDeOpcionesAsistente`. Rejected: `docs/architecture/modelo-local.md` §7 documents "switch back to `anthropic`" as the rollback, and a rollback that stops the service because of a leftover redaction flag is worse than a flag that is ignored loudly. Masking is the safe side, so the fail-safe is to mask and warn.

### D3 — Only `sensible-valor` is unmasked; `sensible-texto` stays suppressed

The masking function gains a mode: "mask everything" (today) or "suppress free text only". It stays a pure function.

_Alternative considered:_ unmasking both classes. Rejected for three reasons that hold with a local model: the text is written by other users and would enter a prompt as instructions (prompt injection, to which an 8B model is more exposed); it has no length bound, and the value columns alone already add 28 % of prompt on 24 rows; and the actor reads that text on the request screen, where it belongs.

### D4 — Cassettes disable the option instead of the option disabling cassettes

With a cassette directory set, the recorder would write response bodies containing real values to files that get committed. The predicate in D2 treats that as "not in effect".

_Alternative considered:_ relying on "only the evaluator records, and it runs against the synthetic fixture". That is how it works today: the recorder writes only when a fixture hash is registered, and only the evaluator registers one. Rejected as the sole guard: the hash comes from the fixture generator, not from the database contents, so nothing stops an evaluator run against a database with imported data.

### D5 — No host check on the local URL; the exposure is documented

The README and `modelo-local.md` state what turning the option on means: the values enter the model server's KV cache and request path, so the server must be on the Department's hardware and keep the mitigation for llama.cpp#27148 (`--cache-ram 0`).

_Alternative considered:_ accepting only loopback or private-range hosts. Rejected: the environments reach the server by a Docker network name (`compose.llm.yml`), DNS classification is not a boundary, and a check that can be satisfied by a reverse proxy would give false confidence.

### D6 — The startup log is the observable

One line at startup says which mode runs, and a warning says when the option is set but not in effect. It is what an operator and a test can read without inspecting prompts.

### D7 — Templates follow the same result

`PlantillaDeRedaccion` receives whatever the redaction step decided to send, so a one-column answer states the real values when the option is in effect and the markers otherwise. No separate switch.

### D8 — `.env.example` lists the option, apart from the 3070 profile

It is commented out in its own paragraph below the 3070 block, with a pointer to the README section on the output boundary. Uncommenting the profile block does not turn it on.

_Alternative considered:_ leaving it out of `.env.example`. Rejected by the user: the option should be discoverable where the local profile is configured.

## Risks / Trade-offs

- [The option is turned on against a "local" URL that is actually a hosted service] → Not detectable in code (D5). Mitigation: README warning next to the option, and the default is off.
- [An 8B model miscopies a digit and the sentence disagrees with the table] → 0 invented values in 9 measured calls, on synthetic and very regular data; weak evidence. Mitigation: the table renders the real rows and stays the source of truth; documented as a known limitation.
- [Real values in the model server's KV cache can surface in another conversation (llama.cpp#27148)] → Mitigated by `--cache-ram 0` in the llama-server profile (`compose.llm-3070.yml`); the docs make it a precondition of the option when the server is llama-server.
- [Slower redaction: +0.1 to +0.3 s on small results, +28 % prompt on 24 rows] → Accepted and stated; this is a quality option.
- [The existing `asistente-enmascarador` requirement is absolute and this change contradicts it] → Its delta is reworded in the same diff to name this option as the only exception.
- [`ProveedorLocal` logs the start of the server's error body on a 400] → The server's error text does not echo the prompt today; a test pins that the turn log carries no row value with the option in effect.

## Migration Plan

1. Ship with the default `false`: no behavior change in any environment.
2. To enable on an on-premises deployment: confirm the model server runs on the Department's hardware with `--cache-ram 0`, set `Asistente__RedaccionSinEnmascarar=true`, restart, and check the startup log line.
3. Rollback: set it to `false` or remove it, restart. Nothing is persisted.
