## Why

The shadow domain router decided on every turn which catalog intent would have captured the question, recorded it in `asistente.registro_operativo.intencion_sombra`, and then let the turn continue down the SQL lane. It never changed an answer, and because it only classified questions it could not act as a fallback when the model fails. It is dead weight in the pipeline, the schema, the docs and the test suite.

## What Changes

- **BREAKING (internal)**: remove `EnrutadorDeDominio`, `ResolutorDeIntenciones`, `CatalogoDeIntenciones` (with `Recursos/intenciones.json`), the domain catalog (`ICatalogoDelDominio`, `CatalogoDelDominioReal`, `LectorDeVocabulario`), `DecisionSombraDelTurno` and step 5 of `CapaConversacional`.
- Remove `IntencionSombra` from `TurnoParaRegistrar` and from the operational record writer.
- New incremental DDL `database/asistente/011_asistente_retirar_intencion_sombra.sql` drops `registro_operativo.intencion_sombra`; the DDL guard in `ArquitecturaAsistenteTests` ratifies exactly that one `DROP COLUMN`.
- Remove the golden table and the tests that only covered the removed pieces; update docs.
- Remove the four capabilities that only specified the shadow router: `asistente-catalogo-intenciones`, `asistente-enrutador-dominio`, `asistente-resolucion-de-slots`, `asistente-medicion-del-enrutador-sombra`.

No answer path changes. A deterministic plan without a model, if wanted, is a separate change.

## Impact

- Code: `Modules.Asistente` (Application/Turno, Infrastructure, `ModuleExtensions`), integration tests.
- Database: `asistente.registro_operativo` loses one nullable column; no GRANT or privilege manifest named it.
- Docs: `docs/architecture/domains/asistente.md`, `docs/architecture/data-model.md`, `Modules.Asistente/README.md`, `docs/quality/tech-debt.md`.
