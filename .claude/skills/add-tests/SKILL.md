---
name: add-tests
description: Agregar tests faltantes a un módulo o feature. Dos lanes: business (BR-* con cita reglamentaria) o technical (garantías de seguridad, datos o flujos sin BR). Antes de escribir, descarta lo que ya está cubierto y mantiene traceability test ↔ requisito.
argument-hint: [<modulo-o-feature> [--lane business|technical]]
---

# Add tests

Agregar los tests que faltan, y sólo esos. Los criterios de qué se testea y qué no están en [golden-principles.md → Tests](../../../docs/quality/golden-principles.md#tests); esta skill los aplica y no los repite.

## Lanes

| Lane          | Cuándo                                                                                               | Output                                                                       |
| ------------- | ---------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| **Business**  | Reglas de negocio `BR-*`                                                                             | Test que verifica una `BR-*` y su entry en `docs/business-rules/<modulo>.md` |
| **Technical** | Garantías sin `BR-*`: autorización, aislamiento por ámbito, concurrencia, atomicidad, PII, contratos | Test de la garantía, en la capa más barata que la verifique de verdad        |

## Runners

- Backend: `dotnet test backend/ArsDocendi.slnx` (xUnit v3, PostgreSQL con Testcontainers). Un test puntual: `dotnet test backend/ArsDocendi.slnx --filter "FullyQualifiedName~<Nombre>"`.
- Frontend: `pnpm --filter frontend test:run` (Vitest + Testing Library).
- Infra y tooling: `node --test infra/tests/*.test.mjs scripts/tests/migrations.test.mjs`.

## Steps

### 1. Descartar lo que ya está cubierto

Para cada caso candidato:

1. Grep por la regla, el código de error (`identity-…`, `periodo-…`), la constraint o el estado en `backend/tests/` y `frontend/src/**/*.test.*`.
2. Si otro test ya lo afirma en otra capa, **no lo dupliques**. Si el existente es débil (por ejemplo, sólo afirma el rol actuante y no el estado resultante), fortalecelo en el mismo archivo.
3. Descartá los casos que golden-principles marca como "no se agregan": cantidades, forma, framework, cosméticos, rendimiento, datos legados imposibles, contenido del seed.

Si no queda nada, terminá ahí: no hay gap que cubrir.

### 2. Lane Business (`BR-*`)

1. Leer `docs/business-rules/<modulo>.md`, sección **Mapping a tests**, e identificar las `BR-*` sin test o con un test que no afirma el `Statement`.
2. Por cada una, releer `Statement` y `Ejemplos` y elegir la capa:
   - Guard de la máquina de estados o validación pura: unit test (por ejemplo `MaquinaEstadosPedido` en `RevisionPedidosTests`).
   - Constraint, trigger o concurrencia: integración con PostgreSQL.
   - Una `BR-*` validada en ambos lados se prueba completa en backend; el frontend sólo prueba que anticipa el error en su módulo de validación (`*Validacion.test.ts`), no en el componente.
3. Escribir un caso positivo y uno negativo.
4. Actualizar la fila de **Mapping a tests** con el archivo y el método.

Si una `BR-*` no es testeable, documentar por qué, marcarla `_test-deferred_` y registrar la deuda en `docs/quality/tech-debt.md`.

### 3. Lane Technical

1. Identificar la garantía concreta: qué estado incorrecto o qué fuga se evita.
2. Elegir la capa según golden-principles. Para datos de la API en el frontend, usar el contrato de `frontend/src/test/contratos/` en lugar de un DTO inventado.
3. Escribir el test armando sus propios datos; no depender de cantidades ni de nombres del seed.

### 4. Verificar que cada test nuevo puede fallar

Romper a propósito el código que cubre (invertir una condición, quitar un guard, renombrar un campo) y confirmar que el test se pone rojo. Restaurar el código. Si el test siguió verde, reescribirlo o descartarlo.

## Verificación

1. Los tests nuevos pasan y fallaron con el código roto (paso 4).
2. La suite completa del área sigue verde.
3. Lane business: la `BR-*` tiene su fila en Mapping a tests.
4. Ningún test nuevo duplica uno existente; si alguno quedó redundante, se borró en el mismo cambio.

## Anti-patterns

- Agregar un test de `GET /api/<modulo>/ping` por módulo: la regla 3 de AGENTS.md la verifica el smoke de despliegue (`infra/scripts/smoke.sh`).
- Agregar render tests "porque el componente no tiene tests".
- Perseguir cobertura por cobertura.
- Repetir en el componente una regla que ya prueba su modelo, o en HTTP una que ya prueba el servicio.

## Abrir PR

Ver [open-pr.md](../../../docs/workflows/open-pr.md). Título sugerido: `test(<modulo>): cubrir BR-<modulo>-NNN`.

## Arguments

`$ARGUMENTS`: módulo o feature objetivo. Opcional `--lane business|technical`.
