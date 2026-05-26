---
status: draft # draft | active | completed | deferred
owner: ""
last_updated: YYYY-MM-DD
module: "" # designaciones | aulas | portal | tareas | shared
---

# Spec: &lt;feature-name&gt;

## Resumen

Un párrafo: qué se entrega y por qué importa para el departamento / los roles involucrados.

## Roles afectados

- [ ] Jefe de Cátedra
- [ ] Coordinador de Carrera
- [ ] Secretaría Académica
- [ ] Decanato
- [ ] Administrativos
- [ ] Docente

## Historias de usuario

- Como &lt;rol&gt;, quiero &lt;capacidad&gt; para que &lt;beneficio institucional&gt;.
  - **Criterios de aceptación**
    - [ ] Criterio 1 (verificable con test)
    - [ ] Criterio 2

## Reglas de negocio aplicables

Linkear a las BR-\* relevantes (existentes o a crear). Toda regla reglamentaria debe quedar registrada en `docs/business-rules/<modulo>.md` con cita normativa.

- `BR-<modulo>-NNN` — descripción + cita reglamentaria

## Requisitos de datos

- Entidades, campos, relaciones, retención, consideraciones PII (datos docentes son PII).

## API surface (si aplica)

- Endpoints, métodos, autorización por rol, idempotencia, forma de errores.
- Linkear a `docs/architecture/api-contracts.md` cuando se implemente.

## Surface afectadas

- [ ] Backend — `backend/src/Modules.<modulo>/`
- [ ] Backend Contracts — `backend/src/Modules.<modulo>.Contracts/` (cambio público que afecta consumidores)
- [ ] Frontend — `frontend/src/features/<modulo>/`
- [ ] Shared — `backend/src/ArsDocendi.Shared/` o `frontend/src/shared/`

## Requisitos de diseño

- IA / Layout
- Estados: loading, vacío, error, éxito, en espera de aprobación
- Referencia a `docs/product/design-principles.md`
- Referencia a `docs/product/designs/<feature-kebab>-design-spec.md` (cuando se defina herramienta UX)

## Definición de hecho (sprint contract)

- Checklist verificable que usa el **evaluador**. Incluir casos negativos (qué NO debe pasar, especialmente con roles no autorizados).

## Anti-patterns específicos de la feature

- e.g. "No agregar un segundo cliente HTTP ad-hoc; usar el `axios` instance de `frontend/src/shared/api/`."

## Dependencias

- Otras specs, módulos, servicios externos (Azure AD, API Guaraní).

## Preguntas abiertas

- _(Cualquier duda de producto o técnica sin resolver)_
