## 1. Code

- [x] 1.1 Remove step 5 and the `EnrutadorDeDominio` / `DecisionSombraDelTurno` dependencies from `CapaConversacional`
- [x] 1.2 Delete `Application/Determinista/`, `intenciones.json`, `CatalogoDelDominioReal`, `LectorDeVocabulario`, `DecisionSombraDelTurno` and their DI registrations
- [x] 1.3 Remove `IntencionSombra` from `TurnoParaRegistrar`, `RegistroDelTurno` and its column list

## 2. Database

- [x] 2.1 Keep `intencion_sombra` in the schema without a writer (no new migration; 002 is published)

## 3. Tests and docs

- [x] 3.1 Delete the tests and the golden table that only covered the removed pieces; adapt fixtures and builders
- [x] 3.2 Update the architecture docs, module README and TD-012
- [x] 3.3 Remove the four shadow-router specs via REMOVED deltas and archive the change
