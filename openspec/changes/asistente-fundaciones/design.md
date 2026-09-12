## Context

El asistente conversacional ejecuta SQL producida por un modelo de lenguaje en tiempo de ejecución. Eso rompe el supuesto sobre el que descansa la autorización del resto del sistema: que la consulta la escribimos nosotros, que su forma se conoce en compilación y que un `[Authorize]` en el controller más la lógica del servicio alcanzan para acotarla.

Con SQL generada, el conjunto de consultas posibles es infinito y desconocido hasta el momento de ejecutarla. No existe un punto de la aplicación donde poner el chequeo, porque cuando la consulta llega ya es demasiado tarde para inspeccionarla con garantías. La defensa tiene que estar **debajo**, en el motor de base de datos, donde se aplica a cualquier consulta sin importar qué código la produjo.

Este cambio construye exactamente esa capa, y nada más. No hay pipeline conversacional, no hay generación de SQL, no hay llamadas a ningún proveedor externo. El criterio es el del análisis previo: **si el sustrato no cierra, el módulo no se escribe**.

El sistema no tiene hoy ninguna pieza de esta capa. El barrido sobre los 19 archivos `.sql` de `database/` buscando `ROW LEVEL SECURITY|CREATE POLICY|GRANT |REVOKE |CREATE EXTENSION` devuelve un único hit, `CREATE EXTENSION IF NOT EXISTS btree_gist` en `006_designaciones_designaciones.sql`. Hay que construirla, no portarla.

## Goals / Non-Goals

**Goals:**

- Que el asistente, cuando exista, no pueda escribir en la base aunque su código tenga un bug: la imposibilidad la impone el motor, no la disciplina.
- Que cada consulta quede acotada al alcance del actor **y** condicionada a su permiso de dominio, evaluado en vivo contra `identity.rol_permisos`.
- Que ninguna columna personal salga de la base por una vía que no esté declarada en un manifiesto versionado.
- Que una tabla nueva agregada por cualquier módulo **rompa el CI** en lugar de quedar concedida en silencio.
- Que el módulo exista, se registre y responda `ping` sin tocar la base ni ningún servicio externo.

**Non-Goals:**

- Cualquier pipeline conversacional: enrutado, generación de SQL, validación, redacción, ambigüedad, hilo.
- Cualquier llamada a un proveedor de LLM, y cualquier configuración de claves o presupuesto.
- Superficie de usuario: endpoints de consulta, catálogo de capacidades, frontend.
- El enmascaramiento de datos sensibles hacia el modelo, que llega junto con el carril que lo necesita.
- Los registros operativo y analítico de consultas.
- El carril determinista contra la API de otros módulos, y por lo tanto los edges hacia sus `Contracts`.

## Decisions

### D1 — La frontera es el motor de base de datos, no una convención

**Opción elegida**: el asistente consulta los schemas `identity` y `designaciones` con un rol de PostgreSQL propio, sin pasar por repositorios ni por `Modules.<X>.Contracts`. La frontera la sostienen el rol sin privilegios de mutación, los `GRANT` por columna y las policies RLS.

**Por qué**: de los cinco pasos del checklist para agregar un edge, «implementar vía Contracts + DI» es el único que este módulo no puede cumplir, y no por comodidad: pasar por Contracts significaría que el modelo genere llamadas a métodos en vez de SQL, que es otro sistema. Agregarle a `IConsultasIdentity` un método de consulta libre sería peor: convertiría la barrera de lectura en una superficie de datos de propósito general para los cuatro módulos.

**Lo que no cierra, y se declara**: no existe lectura del texto actual de la frontera de identity bajo la cual esto no viole su letra. Por eso el cambio **enmienda la regla explícitamente** (D2) en lugar de argumentar que ya lo permitía. Una regla reinterpretada deja de restringir a nadie y deja a `/architecture-drift-check` sin nada contra qué evaluar.

**Descartado**: ubicar el asistente en `ArsDocendi.Shared` (el invariante #4 le prohíbe I/O, red y estado mutable, y el asistente es las tres cosas) · dentro del Host (el único lugar que escapa a los tres guards de `ArquitecturaIdentityTests` a la vez: el sitio menos restringido para el componente que más restricción necesita) · como servicio aparte (contra la topología documentada; sumaría un contenedor por cada ambiente efímero de PR).

---

### D2 — Enmendar el invariante, no reinterpretarlo

**Opción elegida**: se agrega un invariante nuevo, numerado #14, con condiciones verificables:

> **Invariante #14 — Frontera de motor para consulta generada.** Un módulo puede consultar schemas ajenos sin pasar por `Contracts` únicamente si la frontera está sostenida por el motor de base de datos y es falsable: rol de PostgreSQL sin GRANT de mutación, GRANT enumerados columna por columna contra un manifiesto versionado, policies RLS que conjunten el permiso de dominio, y tests que fallen si cualquiera de esas condiciones se degrada. Hoy aplica a `Modules.Asistente` y solo a él.

**Por qué**: la frontera de motor solo es más fuerte que la convención **si es falsable**. Un `GRANT` que nadie re-verifica se degrada en silencio el día que alguien apunte el módulo a la cadena del rol dueño, y no se rompe nada: el sistema sigue funcionando, solo deja de estar contenido. El invariante escrito más los tests son lo que convierte la afirmación en un hecho comprobable.

---

### D3 — Dos roles de lectura, no uno con filtrado en la aplicación

**Opción elegida**: `asistente_ro_<ambiente>` sin acceso a las columnas personales de `identity.personas`, y `asistente_ro_pii_<ambiente>` con acceso a ellas. La conexión se elige según el permiso del actor.

**Por qué**: con dos roles, un usuario sin el permiso **no puede leer la columna** — un `SELECT` sobre ella falla con _permission denied_. Con un solo rol y filtrado en la aplicación, la defensa se muda del motor al código y cualquier camino de código nuevo que se olvide del filtro filtra sin fallar.

**Trade-off aceptado**: la elección de conexión es una línea de código que puede tener un bug. Pero es **una línea** contra una función de enmascaramiento sobre result sets de forma arbitraria, y su bug es visible (el usuario ve datos que no debería) en vez de silencioso.

---

### D4 — `GRANT` columna por columna contra un manifiesto, nunca `ON ALL TABLES`

**Opción elegida**: un archivo versionado enumera **toda** tabla de los schemas expuestos, cada una marcada como `concedida` con la lista explícita de columnas, o `denegada-explicita` con el motivo. Un test compara el manifiesto contra los privilegios efectivos de la base.

**Por qué**: hay evidencia concreta de que la alternativa falla. En el trabajo previo, al sincronizar el esquema apareció `designaciones.idempotencia_comandos`, cuyo `response_body JSONB` guarda la respuesta HTTP completa de cada comando —con datos de personas adentro—. Un `GRANT ... ON ALL TABLES IN SCHEMA designaciones` **se la habría dado sola, sin error y sin aviso**. Y el problema no se puede resolver por columna: `response_body` es una sola columna de forma arbitraria, así que no hay sub-`GRANT` posible; la única salida es denegar la tabla entera.

Como el backend sigue creciendo, van a seguir apareciendo tablas así. El manifiesto es el mecanismo que hace que la próxima rompa el CI en vez de abrir un agujero.

**Por qué el test falla en tres direcciones y no en una**: un privilegio efectivo que no está en el manifiesto es una concesión no declarada; un privilegio declarado que ya no existe es un manifiesto que miente y deja de proteger; y una tabla sin clasificar es la puerta por la que entró `idempotencia_comandos`. Verificar solo la primera dirección deja las otras dos abiertas.

---

### D5 — El permiso de dominio va **dentro** del predicado RLS

**Opción elegida**: cada policy conjunta dos condiciones:

```sql
identity.asistente_tiene_permiso('designaciones.ver')
AND ( <predicado de ámbito: global | carrera | materia> )
```

**Por qué**: RLS decide **qué filas**, no si el usuario tiene derecho a la tabla. Son cosas distintas y en este sistema no coinciden. El rol `docente` tiene ámbito de materia pero sus únicos permisos son `portal.ver` y `portal.editar`: una policy que mirara solo el ámbito le abriría pedidos, historial y justificativos de rechazo de su materia, que la API REST le niega con 403. El asistente no ampliaría un permiso, **crearía acceso donde no hay ninguno**.

Un `[Authorize]` en el endpoint no cubre el hueco: cuando la SQL ya está corriendo, el `[Authorize]` es pasado. RLS es la única capa que se evalúa junto con cada fila.

---

### D6 — El permiso se lee en vivo; no se hardcodea ninguna lista de roles

**Opción elegida**: `identity.asistente_tiene_permiso(code)` resuelve `user_roles → rol_permisos → permisos` en cada evaluación, respetando `deleted_at`.

**Por qué**: la matriz de permisos está comentada en su propia migración como _«PROVISIONAL — PENDIENTE DE CONFIRMACIÓN CON EL CLIENTE»_ y es editable en runtime desde `/membresia-roles`, sin migración. Además `identity.roles` **no es un catálogo cerrado**: Secretaría puede crear roles propios para agrupar permisos.

Una lista negra del tipo `code <> 'docente'` **falla abierta**: cualquier rol nuevo pasa por default, tenga o no el permiso. Una lista blanca falla cerrada y obliga a desplegar cada vez que el cliente crea un rol. Las dos están mal por el mismo motivo: el asistente estaría manteniendo su propia copia de una decisión que vive en otro lado.

Leer el permiso en vivo mantiene al asistente sincronizado con el sistema en las dos direcciones: si Secretaría concede el permiso, el asistente responde; si lo revoca, deja de responder. Sin desplegar nada.

---

### D7 — `ENABLE ROW LEVEL SECURITY`, nunca `FORCE`

**Opción elegida**: las policies se declaran `FOR SELECT ... TO asistente_ro, asistente_ro_pii`, y las tablas quedan con `ENABLE` a secas.

**Por qué**: en PostgreSQL, RLS ya aplica a todo rol no propietario con `ENABLE`; `FORCE` existe únicamente para someter también al **propietario**. La aplicación conecta como el rol dueño, así que activar `FORCE` la sometería a policies que no la contemplan y **tiraría el backend entero**. El objetivo es acotar al asistente, no reescribir la autorización del sistema.

---

### D8 — Reparto del DDL entre provisioning y migraciones

**Opción elegida**:

| Pieza                                                                 | Dónde                                  | Por qué                                            |
| --------------------------------------------------------------------- | -------------------------------------- | -------------------------------------------------- |
| `CREATE ROLE`, `GRANT CONNECT`, `search_path`                         | `infra/scripts/provision-db.sh`        | El rol debe existir antes que cualquier otra cosa  |
| `GRANT USAGE`, `GRANT SELECT (columnas)`, `CREATE EXTENSION unaccent` | Migración del módulo                   | En el paso 1 de `spin-up.sh` la base está vacía    |
| Policies RLS sobre tablas de `designaciones`                          | Migración en `database/designaciones/` | El dueño del bounded context escribe su propio DDL |

**Por qué**: `spin-up.sh` ejecuta `provision-db.sh` en el paso 1 sobre una base **vacía** y las migraciones en el paso 3. Un `GRANT ... ON ALL TABLES` escrito en el provisioning **otorga exactamente nada y no falla**: el asistente arrancaría, generaría SQL válida, y PostgreSQL devolvería `permission denied` en cada consulta. Es un modo de falla que aparece tarde y confunde.

---

### D9 — Un rol por ambiente, con contraseña propia

**Opción elegida**: `asistente_ro_<ambiente>`, creado y destruido junto con la base del ambiente.

**Por qué**: los roles de PostgreSQL son objetos de **cluster**, y la instancia es una sola con una base por ambiente. Un rol único sería el mismo principal —y la misma contraseña— para producción y para cada ambiente efímero de PR, que corre código arbitrario de un pull request sobre la misma red de datos.

---

### D10 — Tipos envoltorio por cadena de conexión

**Opción elegida**: `CadenaDuena`, `CadenaSoloLectura` y `CadenaSoloLecturaPii` son tipos distintos, no `string`. La resolución vive en la composición del Host.

**Por qué**: hoy el sistema tiene una sola cadena de conexión que comparten los dos `DbContext`. Introducir tres cadenas sueltas convierte «pedí la equivocada» en un fallo silencioso que solo se descubre cuando alguien logra escribir. Con tipos distintos, el error se traslada de runtime a compilación.

---

### D11 — Permiso nuevo persistido en lugar de política compuesta

**Opción elegida**: `asistente.consultar` como permiso persistido en `identity.permisos`, con siembra explícita para los siete roles de sistema y su entrada en `Permisos.Todos`.

**Por qué**: el asistente es un módulo de asistencia que debe poder apagarse. Con un permiso propio se apaga por rol desde `/membresia-roles`, sin desplegar. Con una política compuesta sobre `designaciones.ver` —el precedente de `Politicas.DocentesVer`— quitarle el asistente a alguien significaría quitarle ver designaciones, que es un martillo demasiado grande.

**Dos trampas verificadas que el cambio evita**:

1. `sys_admin` **no hereda permisos nuevos**: su matriz se sembró con `ARRAY(SELECT code FROM identity.permisos)` evaluado en el momento en que corrió esa migración. La existencia de la migración `010` es la prueba de que el repositorio ya tropezó con esto. La siembra debe ser explícita para los siete roles.
2. Un permiso ausente de `Permisos.Todos` **falla en runtime, no en compilación**: `Program.cs` registra las políticas iterando ese array, así que una constante declarada pero ausente del array no produce política y el `[Authorize]` correspondiente lanza al primer request.

---

### D12 — El actor viaja en un GUC transaction-local

**Opción elegida**: cada ejecución abre conexión y transacción nuevas, declara `SET TRANSACTION READ ONLY`, y fija el actor con `set_config('app.asistente_user_id', <id>, true)`.

**Por qué**: el tercer parámetro `true` hace el ajuste **transaction-local**, así que el valor muere en el `COMMIT` y no sobrevive a la devolución de la conexión al pool. Con una variante de sesión, un turno podría heredar el actor del turno anterior. Abrir transacción nueva por ejecución además acota cualquier intento de reescribir el GUC a una sola sentencia.

---

### D13 — La identidad se toma de `ICurrentUser.UserId`, nunca del `oid` de Azure

**Opción elegida**: el valor que se escribe en el GUC es `identity.users.id`, obtenido de `ICurrentUser.UserId`.

**Por qué**: la cadena `Azure AD (oid, upn) → VinculadorPrimerLogin → identity.users.id → ICurrentUser.UserId` ya existe completa en el sistema. `identity.chatbot_actor()` compara contra `user_roles.user_id`, que es `identity.users.id`. Tomar el `oid` del token y meterlo en el GUC **rompe en silencio**: el tipo coincide (ambos son UUID) pero el valor no corresponde a ninguna fila, así que las policies filtran todo y el asistente responde «no hay datos» en vez de fallar.

**Prohibido explícitamente**: cualquier mecanismo que tome la identidad de algo enviado por el cliente. La documentación de contratos de API del sistema lo prohíbe textualmente, y un encabezado de identidad con fallback de desarrollo es exactamente esa clase de puerta.

## Risks / Trade-offs

- **La enmienda necesita acuerdo del equipo.** El invariante #14 es un cambio en la documentación de arquitectura y la migración de `identity` toca la superficie de administración. Si el equipo no acepta la enmienda, este cambio no se mergea y el asistente no se construye. Es el riesgo principal y es de proceso, no técnico.
- **Ser un módulo no-core hace más difícil, no más fácil, defender la excepción.** «Es solo un módulo de asistencia» es exactamente el argumento con el que se puede rechazar. La contracara es que un módulo no-core que abriera un agujero de autorización sería el peor intercambio posible del sistema: por eso D5 no es una recomendación sino una condición.
- **Las policies RLS agregan un predicado a cada consulta contra cuatro tablas.** Para el rol dueño el costo es cero porque no está sujeto a ellas. Para el asistente, el predicado incluye una función `SECURITY DEFINER` que consulta `rol_permisos`: hay que verificar el plan de ejecución y, si hace falta, marcarla `STABLE` para que el planner la evalúe una vez por consulta.
- **Un manifiesto exhaustivo tiene costo de mantenimiento.** Cada tabla nueva del sistema obliga a clasificarla. Es deliberado: ese costo es el que compra que ninguna entre por default.
- **El cambio no produce nada visible.** No hay pantalla, no hay respuesta, no hay demo. Su valor es que el resto pueda construirse encima; su gate es que los tests fallen cuando deben.
