# Domain: asistente

## Propósito

Responder en español preguntas sobre datos que ya viven en la base del sistema, en
modo solo lectura y acotadas al alcance de quien pregunta. Cubre la familia de
casos de uso —cobertura de cátedra, composición del plantel— que hoy **no tiene
endpoint equivalente** en ningún módulo.

## Roles que interactúan

Todos los roles del sistema salvo `docente`, según la siembra del permiso
`asistente.consultar`:

- **Jefe de Cátedra** — pregunta por su materia
- **Coordinador de Carrera** — pregunta por su carrera
- **Secretaría Académica** — pregunta por todo el Departamento
- **Decanato** — ídem
- **Administrativo** — ídem, dentro de lo que sus permisos de dominio habilitan
- **Administrador de Sistemas** — el rol existe en la siembra, pero es `NOINHERIT`
  y no hereda permisos: en la práctica no ve nada hasta que se le asigne otro rol

El alcance no lo aplica el módulo: lo aplican las policies RLS con el actor fijado.
El asistente no tiene ninguna rama de código que decida qué puede ver quién.

## Bounded context

- **Pertenece**: la traducción de lenguaje natural a consulta, la validación de esa
  consulta, la política de abstención y la redacción de la respuesta. El catálogo
  de ejemplos pregunta-SQL.
- **No pertenece**: los datos. Todos son de otros bounded contexts —`identity` y
  `designaciones`— y el asistente los lee a través de dos roles de PostgreSQL con
  privilegios enumerados columna por columna. No hay ninguna entidad canónica acá.

## Entidades principales

Ninguna. El módulo no tiene schema propio ni `DbContext`.

Lee, con `GRANT SELECT` por columna contra un manifiesto deny-by-default:

| Schema          | Tablas                                                                                         |
| --------------- | ---------------------------------------------------------------------------------------------- |
| `identity`      | `carreras`, `materias`, `personas`, `users`, `roles`, `user_roles`, `permisos`, `rol_permisos` |
| `designaciones` | `cargos`, `periodos`, `pedidos`, `pedido_adjuntos`, `pedido_historial`, `designaciones`        |

Denegadas explícitamente: `designaciones.idempotencia_comandos` (su `response_body`
JSONB guarda respuestas HTTP completas con datos de personas), el schema `audit`
entero, y las columnas `pedidos.snapshot`, `pedido_adjuntos.uri`, `users.azure_oid`
y `user_roles.granted_by`.

Fuente de verdad: [`database/asistente/manifiesto-privilegios.json`](../../../database/asistente/manifiesto-privilegios.json).

## API pública (contract)

`Modules.Asistente.Contracts` **nace vacío**: ningún otro módulo consume al
asistente. Queda por convención, pendiente de decidir con el equipo si se conserva
o se declara la excepción.

## Endpoints HTTP

| Método | Path                  | Rol       | Descripción  |
| ------ | --------------------- | --------- | ------------ |
| GET    | `/api/asistente/ping` | (anónimo) | Health check |

El endpoint del turno —`POST /api/asistente/consultas`— llega con la épica de
superficie de usuario, junto con el contrato de cuatro estados y la
`Idempotency-Key`. Hoy el carril es un servicio del módulo.

## El carril SQL

Dos llamadas al modelo por turno; todo lo del medio, determinista.

| Pieza                  | Qué hace                                                            | Cuesta |
| ---------------------- | ------------------------------------------------------------------- | ------ |
| `IPerfilDelActor`      | Alcance global y acceso a datos personales; valida el actor         | 0      |
| `IProveedorDeEsquema`  | Prefijo estable del prompt, derivado de los `GRANT` efectivos       | 0      |
| `ISelectorDeEjemplos`  | Ejemplos por similitud léxica, en proceso                           | 0      |
| `GeneradorDeSql`       | **Llamada 1**: temperatura 0, prefijo cacheado                      | 1      |
| `ValidadorDeSql`       | Tokeniza y rechaza funciones y palabras clave prohibidas            | 0      |
| `IEjecutorDeConsulta`  | Transacción nueva `READ ONLY`, actor transaction-local, `LIMIT n+1` | 0      |
| `PoliticaDeAbstencion` | Guard de vacío y decisión de reintento                              | 0      |
| `RedactorDeRespuesta`  | **Llamada 2**: temperatura 0,3, sin caché                           | 1      |

### Cuatro capas de defensa, independientes entre sí

1. **El rol** no tiene ningún privilegio de mutación (`42501`).
2. **La transacción** se declara `READ ONLY` (`25006`).
3. **Las policies RLS** filtran las filas según el actor fijado.
4. **El validador** rechaza la consulta antes de ejecutarla.

Las tres primeras las impone el motor. La cuarta sube el costo de un ataque; el
motor es lo que lo hace inútil. Cada capa tiene su propio test, aislada de las
otras.

Hallazgo del camino: la envoltura en subconsulta hace **estructuralmente**
imposible colar DML, porque PostgreSQL admite una CTE que modifica datos solo en el
nivel superior de la sentencia.

### La abstención

Siete casos, y uno central: RLS convierte «no tenés permiso» en cero filas, que es
la misma firma que «el literal no matcheó». Antes de gastar el reintento se
consulta si el actor es global; si no lo es, un vacío no lo gasta y la respuesta
dice «no encontré nada en tu alcance», nunca «no hay».

Un resultado vacío se resuelve **sin llamar al modelo**. Con cero filas no hay nada
que narrar, así que la distinción queda garantizada por código y no por una
instrucción del prompt.

Restricción dura: ninguna respuesta declara cuántas filas quedaron afuera. El
indicador de truncado es un booleano y no un número.

## Reglas de negocio (BR-\*)

Ninguna propia. El asistente no decide nada del dominio: expone lo que otros
módulos ya decidieron. Las reglas que lo acotan son de seguridad, no de negocio, y
viven en el manifiesto de privilegios y en las policies RLS.

## Dependencias

- **Hacia adentro**: solo `ArsDocendi.Shared` (cadenas tipadas, permisos, migración
  de módulo). Ningún edge hacia otro módulo: el carril determinista de API, que sí
  agrega edges hacia `Modules.<X>.Contracts`, es de la épica E6.
- **Hacia afuera**: nadie lo consume.
- **Externas**: un proveedor de modelo de lenguaje, detrás de `IProveedorDeModelo`.
  Hoy la única implementación es la simulada. **El asistente no accede a ninguna
  otra fuente externa**: opera exclusivamente sobre la base del propio sistema.

## Specs activas

- `openspec/changes/asistente-fundaciones/` — roles, manifiesto, permiso, funciones
  del actor, RLS, cadenas tipadas y módulo base
- `openspec/changes/asistente-carril-sql/` — este carril
- `openspec/changes/asistente-evaluacion/` — el eje de capacidad y la exclusión del CI

## Evaluación

La métrica primaria del proyecto es **corrección con abstención**, y se mide con el
evaluador de [`backend/eval/`](../../../backend/eval/README.md).

Está partido en dos por **qué cuesta dinero**, no por qué es «de evaluación»:
`ArsDocendi.Evaluacion.Nucleo` —generador del fixture, dataset, puntuación,
preflight, reporte— está en la solución y tiene tests en el CI;
`ArsDocendi.Evaluacion` —el ejecutable, lo único que instancia un proveedor real—
está **fuera**, con un guard adentro que falla si vuelve a entrar. El CI corre los
tests de la solución sin filtro, y el síntoma de olvidarlo sería una factura, no un
test rojo.

Hoy no se puede correr: no hay ninguna implementación de proveedor de modelo real.
Está registrado como TD-008.

## Decisiones registradas

- **El esquema del prompt se deriva de los privilegios efectivos** — una lista
  embebida en código se desincroniza en silencio y falla en las dos direcciones:
  describe columnas revocadas y omite columnas nuevas.
- **Los `COMMENT ON` viven en el DDL de cada módulo dueño** — mismo criterio con
  que las policies RLS viven en el de `designaciones`.
- **Similitud léxica y no embeddings** — con decenas de ejemplos, un vector store
  es un servicio, un modelo y una llamada de red más por turno para elegir entre
  pocas opciones.
- **La fecha de referencia es un parámetro del turno** — hace el eval determinista
  y permite prohibir el reloj entero en el validador sin romper ningún caso.
- **El límite pide una fila de más** — sin la fila sonda, «devolvió N» y «se
  recortó» son indistinguibles y la redacción afirma totales falsos.
- **El actor va transaction-local** — uno de sesión sobreviviría al pool y un turno
  heredaría el actor del anterior, respondiendo con el alcance equivocado sin
  tirar error.
- **El carril es un servicio y no un endpoint** — construir el contrato antes de
  tener los cuatro estados y el hilo conversacional obligaría a inventarlo dos
  veces.
