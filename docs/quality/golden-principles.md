# Golden principles (anti-slop)

Reglas opinionadas para mantener el código **legible para agentes** y **seguro de extender**. Las violaciones se flaggan en review y evaluation.

## Fronteras de módulos (Backend .NET)

- **Nunca** referenciar el proyecto `Modules.<Otro>`: sólo su `Modules.<Otro>.Contracts`.
- **Nunca** referenciar la clase service concreta de otro dominio — usar **interfaces de Contracts** + DI.
- Antes de agregar dependencias cross-module, leer [`docs/architecture/dependency-graph.md`](../architecture/dependency-graph.md) y preservar el **DAG** (sin ciclos).
- Si un util lo usa **un solo** dominio, mantenerlo dentro de `Modules.<X>` — NO en `ArsDocendi.Shared`.

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

## Autorización por permisos efectivos

- Las rutas administrativas deben requerir `[Authorize(Policy = Permisos.<...>)]` y validar permisos efectivos; no acoplar acceso a nombres de roles con `[Authorize(Roles = ...)]`.
- Toda ruta debe autorizar en backend salvo los pings de salud declarados explícitamente públicos.
- Testear la **denegación cuando falta el permiso** de cada operación sensible. No hace falta la matriz completa de roles × rutas: un caso denegado y uno permitido por permiso.
- La visibilidad de navegación y los guards del frontend mejoran la UX, pero nunca sustituyen la autorización backend.

## Producto y UX

- **No stubs**: botones, forms, o rutas que aparentan estar hechos pero no funcionan.
- **No lorem ipsum** ni copy "TODO" visible al usuario en flujos productivos.
- Sin jerga técnica filtrada al usuario final (IDs de DB, stacktraces, etc).

## Higiene de código

- Preferir **archivos chicos**; tratar **~300 líneas** como cap soft por archivo (split con criterio cuando se excede).
- **Structured logging** (Serilog) en backend; nunca `Console.WriteLine` en código productivo.
- **Naming**: DTOs, entidades y schemas consistentes; evitar identificadores de 1-2 letras en lógica de dominio.

## Tests

Un test se justifica sólo si, al fallar, el sistema queda incompleto o incorrecto: protege una regla de negocio (BR-\*), una garantía de seguridad o de datos, o un flujo que usa una persona. Cada test se paga en cada corrida de CI.

**Antes de agregar un test**

1. **Buscá si ya está cubierto.** Grep por la regla, el código de error o la constraint en `backend/tests/` y `frontend/src/**/*.test.*`. Si otro test ya lo afirma, extendé ese en lugar de crear uno nuevo.
2. **Una regla, una capa.** Elegí la capa más barata que la verifique de verdad, y no la repitas en otra:
   - Máquina de estados, validación o filtro puro: unit test (sin base ni host).
   - Constraint, trigger, concurrencia o transacción: integración con PostgreSQL.
   - Autorización, serialización o cableado HTTP: un test HTTP por flujo, no uno por regla.
   - Frontend: la lógica pura se prueba en su módulo (`*Modelo`, `filtros*`, `*Validacion`); el componente sólo prueba la interacción que lo conecta. Un helper compartido (`filaClickeable`, `FiltroEncabezado`) se prueba una vez, no en cada tabla que lo usa.
3. **Verificá que el test puede fallar.** Rompé a propósito el código que cubre (invertí una condición, quitá un guard) y confirmá que el test se pone rojo. Si sigue verde, no prueba nada.

**No se agregan tests que**

- Cuentan cosas: endpoints, pestañas, opciones de un catálogo, filas del seed o columnas de una tabla. Si la cantidad es una regla de negocio, se prueba la regla: "rechaza una dedicación inactiva", no "hay seis dedicaciones".
- Verifican forma en lugar de comportamiento: existencia de índices o columnas (`information_schema`, `pg_index`), nombres de métodos por reflexión, o regex sobre el texto de scripts, workflows o YAML. Un script se prueba ejecutándolo con sus fronteras externas sustituidas, como `infra/tests/spin-up.test.mjs`.
- Prueban el framework o la base: que una PK rechace duplicados, que una columna nullable acepte null, que `SHA256` sea estable o que `NavLink` marque `aria-current`.
- Defienden un estado que la base ya hace imposible, por ejemplo desactivando un trigger para insertar datos "legados". La garantía la prueba el test de la constraint.
- Validan el contenido de los fixtures de desarrollo (`sintetico.sql`) en lugar de la lógica. Cada test arma los datos que su regla necesita.
- Son cosméticos: títulos, breadcrumbs, textos de botones, ausencia de una columna o ícono, recortes, tooltips o posición. Esto pertenece al design spec, no a la suite.
- Son mediciones de rendimiento (`EXPLAIN ANALYZE`, miles de filas sintéticas, conteo de consultas SQL). Una medición se corre una vez y se guarda como evidencia del change, fuera de `dotnet test` y de CI.
- Son evidencia de un change o de una migración ya aplicada en todos los ambientes, o fijan un SHA histórico. El historial de migraciones ya está protegido por `migraciones-protegidas.json`.

**Costo**

- Los tests de backend comparten un PostgreSQL clonado desde una base plantilla (`PostgresFixture`). No levantes contenedores propios por test o por clase: si necesitás otro servicio, usá un fixture compartido (`IClassFixture`), como SeaweedFS en Storage.
- xUnit paraleliza por clase. No agrupes muchos tests lentos en una sola clase `partial`: separalos en clases que hereden los helpers de una base común (`TareasHttpBase`, `VigenciasPedidosBase`).
- Un `[Theory]` con muchos `InlineData` sólo se justifica si cada caso ejerce una rama distinta del código.

**Contratos frontend↔backend**

- Los tests del frontend no inventan DTOs de la API cuando existe un contrato en `frontend/src/test/contratos/`. Ese JSON lo genera el backend desde una respuesta real (`ContratoFrontend.Verificar`); se regenera con `ACTUALIZAR_CONTRATOS=1 dotnet test backend/ArsDocendi.slnx`.

**Mantenimiento**

- Al cambiar o borrar comportamiento, borrá también los tests que sólo lo cubrían. Un test que ya no protege nada es deuda.
- Si un test nuevo vuelve redundante a otro, borrá el más débil en el mismo PR.

## Bug fixes

- **Red-green obligatorio**: cada bug fix arranca con un **test que falla** que captura el bug. El fix es el cambio MÁS chico que vuelve el test verde. Sin excepciones (excepto bugs puramente visuales).
- **No drive-by refactors**: al arreglar un bug, NO refactorizar código no relacionado ni agregar features.
- **Escalación**: si el fix requiere cambio en Contracts o cruza módulos, o revela una brecha arquitectural, crear un change con `/opsx:propose` e implementarlo con `/add-feature`.
- **Prevenir recurrencia**: si una clase de bug es prevenible por regla, **agregar la regla a este archivo**.

## Compliance reglamentario

- Toda regla de negocio que provenga de reglamentación institucional debe registrarse como `BR-<modulo>-NNN` en `docs/business-rules/<modulo>.md` con **cita de la normativa**.
- Toda BR-\* debe tener al menos un **test** que la verifique.
- Cambios en interpretación reglamentaria se documentan en el changelog del BR correspondiente.

## Documentación

- Cuando cambia comportamiento, API, o schema, actualizar el doc relevante **en el mismo PR**:
  - `api-contracts.md`, `data-model.md`, `dependency-graph.md`, `domains/<x>.md`, business-rules.
- Specs y plans son **artefactos de primera clase**, no opcionales para features de tamaño no trivial.
