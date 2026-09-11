# Golden principles (anti-slop)

Reglas opinionadas para mantener el código **legible para agentes** y **seguro de extender**. Las violaciones se flaggan en review y evaluation.

## Fronteras de módulos (Backend .NET)

- **Nunca** importar desde `Modules.<Otro>/Internal/` u otro path privado de otro dominio.
- **Nunca** referenciar la clase service concreta de otro dominio — usar **interfaces de Contracts** + DI.
- Antes de agregar dependencias cross-module, leer [`docs/architecture/dependency-graph.md`](../architecture/dependency-graph.md) y preservar el **DAG** (sin ciclos).
- Si un util lo usa **un solo** dominio, mantenerlo en `Modules.<X>/Internal/` — NO en `ArsDocendi.Shared`.

## Contracts y Shared

- `Modules.<X>.Contracts`: **solo DTOs, interfaces y tokens públicos** — sin lógica, sin I/O.
- `ArsDocendi.Shared`: **funciones puras** — sin I/O, sin network, sin estado mutable compartido.
  - **Única excepción**: la persistencia de los schemas `identity` y `audit` (`IdentityDbContext` + su migrador). Es infraestructura transversal de la que dependen los 4 módulos, no lógica de dominio. Ninguna otra I/O entra a Shared.
  - Corolario: los módulos **leen** identity para autorizar. Escribir `personas`, `roles`, `permisos` o `rol_permisos` es exclusivo de la superficie de administración — el invariante #1 no lo cubre porque no es una relación cross-module.
- Evitar "god utils": si Shared crece mucho, **partir por concern** (objetivo: ~20 exports públicos por archivo o split).

## Capas (.NET)

- **Controller → Service → Repository** únicamente.
- Controllers son delgados; reglas de negocio viven en services.
- **Prohibido**: controller → repository directo.

## Frontend (React + Vite)

- Features NO se importan entre sí. Lo común sube a `frontend/src/shared/`.
- Un solo `axios` instance, compartido en `frontend/src/shared/api/`. **Nunca** crear axios ad-hoc en una feature.
- Data fetching: **React Query** siempre. Nunca `useEffect` + fetch manual para datos del servidor.

## Boundaries de datos

- **Parsear en el borde** — validar DTOs en el límite HTTP/módulo; no propagar shapes sin validar.

## Autorización por rol

- Toda acción mutativa requiere autorización por rol. **Nunca** dejar un endpoint sin `[Authorize(Roles = ...)]` salvo health checks.
- **Test cada combinación rol × acción** que sea relevante (especialmente que un rol bajo NO puede aprobar algo de rol alto).

## Producto y UX

- **No stubs**: botones, forms, o rutas que aparentan estar hechos pero no funcionan.
- **No lorem ipsum** ni copy "TODO" visible al usuario en flujos productivos.
- Sin jerga técnica filtrada al usuario final (IDs de DB, stacktraces, etc).

## Higiene de código

- Preferir **archivos chicos**; tratar **~300 líneas** como cap soft por archivo (split con criterio cuando se excede).
- **Structured logging** (Serilog) en backend; nunca `Console.WriteLine` en código productivo.
- **Naming**: DTOs, entidades y schemas consistentes; evitar identificadores de 1-2 letras en lógica de dominio.

## Bug fixes

- **Red-green obligatorio**: cada bug fix arranca con un **test que falla** que captura el bug. El fix es el cambio MÁS chico que vuelve el test verde. Sin excepciones (excepto bugs puramente visuales).
- **No drive-by refactors**: al arreglar un bug, NO refactorizar código no relacionado ni agregar features.
- **Escalación**: si el fix requiere cambio en Contracts o cruza módulos, crear un change con `/opsx:propose` y agregar nota en `docs/plans/backlog.md`. Si revela brecha arquitectural, escalar creando un change con `/opsx:propose` e implementarlo con `/add-feature`.
- **Prevenir recurrencia**: si una clase de bug es prevenible por regla, **agregar la regla a este archivo**.

## Compliance reglamentario

- Toda regla de negocio que provenga de reglamentación institucional debe registrarse como `BR-<modulo>-NNN` en `docs/business-rules/<modulo>.md` con **cita de la normativa**.
- Toda BR-\* debe tener al menos un **test** que la verifique.
- Cambios en interpretación reglamentaria se documentan en el changelog del BR correspondiente.

## Documentación

- Cuando cambia comportamiento, API, o schema, actualizar el doc relevante **en el mismo PR**:
  - `api-contracts.md`, `data-model.md`, `dependency-graph.md`, `domains/<x>.md`, business-rules.
- Specs y plans son **artefactos de primera clase**, no opcionales para features de tamaño no trivial.
