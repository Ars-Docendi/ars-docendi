# Arquitectura actual del Asistente (Text-to-SQL) de Ars Docendi

Notas de análisis de código local. Todas las rutas son relativas al worktree `feature/asistente-modelo-local` (`/tmp/claude-0/-home-user-ars-docendi/caaad005-9e8c-59cb-9cfd-725e9fa71d55/scratchpad/asistente`, HEAD `bc8fc86`), salvo el documento de Ollama, que vive en la rama `origin/infra/ollama`. Las citas usan la ruta del archivo como "URL".

## 1. Pipeline del turno de punta a punta: etapas, cuáles llaman al LLM, cuántas llamadas y qué lleva el prompt

### Takeaway
Un turno pasa por una capa conversacional (casi toda determinista) y después por un "carril SQL". El carril tiene dos llamadas al modelo como base: generar el SQL y redactar la respuesta. Hay una tercera opcional, la reescritura del seguimiento, y reintentos o reparaciones acotados. El techo duro es de 4 llamadas por turno y 12 requests HTTP. El prompt de generación tiene un prefijo estable y cacheable de unos 12.000 tokens (instrucciones, vocabulario de catálogos cerrados y esquema con comentarios) más un mensaje variable de unos 0,3 a 1,3k tokens.

### Cited Findings
- Entrada: `POST /api/asistente/consultas`. La pregunta "se reescribe contra el hilo, se traduce a una consulta, se valida, se ejecuta acotada al actor, se enmascara y se redacta". — [README del módulo](backend/src/Modules.Asistente/README.md)
- **Capa conversacional** (`CapaConversacional.ResponderAsync`), en este orden:
  1. `IAlmacenDeHilos`: hilo en memoria con TTL de 2 h, atado al actor; guarda preguntas y la consulta, nunca filas.
  2. `EnrutadorSocial`: saludos y meta-preguntas, "0 tokens".
  3. `ReconocedorDeAclaracion`: 0 tokens.
  4. `DetectorDeCambioDeTema`: suelta el segmento de historial.
  5. `ReescritorDePreguntas`: "única llamada al modelo de la capa; solo con historial".
  6. `DetectorDeAmbiguedad`: "0 tokens · un SELECT, no el modelo".
  7. Delegación al carril SQL.

  — [README del módulo](backend/src/Modules.Asistente/README.md)
- Además corre un `EnrutadorDeDominio` determinista **en modo sombra**: decide y registra la intención en `asistente.registro_operativo.intencion_sombra`, pero el turno sigue por SQL igual. — [README del módulo](backend/src/Modules.Asistente/README.md); [design enrutador](openspec/changes/asistente-enrutador-de-dominio/design.md)
- **Carril SQL**: "Dos llamadas al modelo por turno y ocho piezas deterministas alrededor... cada pieza determinista que se agrega al medio es una pieza que no puede alucinar". Las piezas son:
  1. `IPerfilDelActor`: alcance global y acceso a datos personales.
  2. `GeneradorDeSql`: LLAMADA 1, temperatura 0, prefijo cacheado. El esquema sale de `IProveedorDeEsquema`, los ejemplos de `ISelectorDeEjemplos` (por similitud léxica) y la fecha "hoy" de `IFechaDeReferencia`, como parámetro.
  3. `ValidadorDeSql`.
  4. `IEjecutorDeConsulta`.
  5. `PoliticaDeAbstencion`.
  6. `Enmascarador`.
  7. `RedactorDeRespuesta`: LLAMADA 2, temperatura 0,3, sin caché.

  — [README del módulo](backend/src/Modules.Asistente/README.md)
- La auditoría del 2026-09-06 lo describe como "siete pasos y **dos llamadas al modelo** — el resto es determinista". En la práctica numera: reescritura (llamada 1, solo con historial), generación (llamada 2) y redacción (llamada 3). — [Auditoría](docs/quality/auditoria-asistente.md)
- Cotas explícitas: `MaximoDeLlamadasPorTurno` = 4 (global del turno, no por capa) y `MaximoDeIntentosDeTransporte` = 3. "Peor caso de un turno: `4 × 3 = 12` requests HTTP". — [README del módulo](backend/src/Modules.Asistente/README.md)
- Tamaños medidos sobre los 109 cassettes grabados contra Claude:

  | Llamada | Prefijo | Entrada total | Salida (mediana / máx.) |
  | --- | --- | --- | --- |
  | Reescritura | ~350 tokens | 694–733 | 23 / 219 |
  | Generación de SQL | **~12.000 tokens** ("instrucciones + vocabulario + esquema, 2 variantes por rol") | 12.294–13.399 | 132 / 1.033 (con razonamiento) |
  | Redacción | ~190 tokens | 363–898 | 83 / 272 |

  Un reintento por consulta vacía es idéntico a la generación. — [modelo-local.md §1](docs/architecture/modelo-local.md)
- "El prefijo de 12k es el costo dominante". Es estable byte a byte (`RenderizadorDeEsquema` es determinista) y "solo hay dos variantes (rol básico y rol con datos personales)". Sin razonamiento, un turno típico genera unos 210 tokens. — [modelo-local.md §1](docs/architecture/modelo-local.md)
- Composición del prefijo:
  1. Instrucciones fijas: 8 "REGLAS QUE NO SE NEGOCIAN" (solo SELECT, sin reloj, sin `set_config`, una sentencia, solo tablas listadas, sin LIMIT, tablas calificadas por esquema, `public.unaccent(col) ILIKE` en lugar de `=` para texto tipeado), más secciones sobre seguimientos, alcance ("No escribas ningún filtro de permisos") y formato JSON de salida.
  2. "VALORES POSIBLES" de catálogos cerrados.
  3. "ESQUEMA DISPONIBLE".
  4. Glosario opcional.

  — [InstruccionesDeGeneracion.cs](backend/src/Modules.Asistente/Infrastructure/Catalogo/InstruccionesDeGeneracion.cs); [RenderizadorDeEsquema.cs](backend/src/Modules.Asistente/Infrastructure/RenderizadorDeEsquema.cs)
- El esquema del prompt se deriva de los privilegios efectivos de la conexión (`information_schema.column_privileges`) y de los `COMMENT ON` de tablas y columnas. Se calcula de forma perezosa, se cachea por rol y solo se recalcula al reiniciar el proceso. — [design carril-sql D1–D3](openspec/changes/asistente-carril-sql/design.md); [README del módulo](backend/src/Modules.Asistente/README.md)
- Ejemplos few-shot: 20 pares pregunta/SQL verificados en `Recursos/ejemplos-sql.json` (9 cruce_de_tablas, 7 agregacion, 3 filtro_temporal, 1 consulta_simple). Por defecto el selector léxico manda hasta cuatro en el mensaje de usuario; con `EjemplosEnElPrefijo` van todos en el prefijo. — [ejemplos-sql.json](backend/src/Modules.Asistente/Recursos/ejemplos-sql.json); [Auditoría](docs/quality/auditoria-asistente.md); [modelo-local.md §8](docs/architecture/modelo-local.md)
- Salida de la generación: JSON restringido por esquema con `pregunta_interpretada`, `es_contestable`, `sql`, `razonamiento`, `categoria` (enum de 6 valores), `motivo` y `termino`. En el proveedor local se aplica con `response_format: json_schema`. — [GeneradorDeSql.cs](backend/src/Modules.Asistente/Application/CarrilSql/GeneradorDeSql.cs); [modelo-local.md §6](docs/architecture/modelo-local.md)
- Glosario institucional: 24 términos, opcional (`GlosarioEnElPrefijo`, apagado). Mide 4.836 caracteres. El prefijo completo con glosario y esquema compacto mide 31.018 caracteres, y la solicitud más larga crece de 9.797 a 11.034 tokens. — [modelo-local.md §6](docs/architecture/modelo-local.md)
- Con el perfil local optimizado (esquema compacto y los 20 ejemplos en el prefijo), "el pedido más largo usa 9,8k tokens". — [modelo-local.md §8](docs/architecture/modelo-local.md)
- Optimizaciones opt-in que reducen llamadas:
  - `RedaccionConPlantillas`: redacción sin modelo para un valor o una lista corta.
  - `VigenciaDeCacheDeConsultasMinutos`: caché de SQL por pregunta + rol + día; "cachea la consulta y no las filas: se re-ejecuta siempre bajo RLS".
  - `ReescrituraEnLaGeneracion`: una llamada menos por seguimiento.
  - `RepararConsultaFallida`: una ronda extra con el error saneado.

  — [modelo-local.md §6](docs/architecture/modelo-local.md); [README del módulo](backend/src/Modules.Asistente/README.md)
- Sin proveedor (breaker abierto, cupo agotado o mantenimiento) "cinco de los ocho pasos del pipeline no lo necesitan". Saludos, menús de aclaración y respuestas a un menú siguen funcionando; solo lo que exige generar SQL degrada. — [README del módulo](backend/src/Modules.Asistente/README.md)

### Inferences
- En el caso común, un turno de datos sin seguimiento cuesta 2 llamadas (generación + redacción) y uno con seguimiento cuesta 3. Con el perfil local (plantillas, caché y reescritura en la generación) puede bajar a 1 o incluso 0 llamadas cuando hay acierto de caché y el resultado es trivial.
- El LLM interviene en tres puntos: reescribir, traducir a SQL y redactar. La clasificación de intención, la ambigüedad, el cambio de tema, el small talk, la validación, la autorización y la abstención son deterministas.

### Gaps
- No hay una tabla única y consolidada de cuántas llamadas promedio hace un turno en tráfico real: el registro operativo existe, pero no encontré mediciones publicadas.

## 2. Cómo se genera, valida, ejecuta y redacta el SQL, y qué objetos de la base ve el modelo

### Takeaway
El modelo genera SQL PostgreSQL libre contra las tablas reales de los schemas `identity`, `designaciones` y `portal`. No hay vistas ni un schema dedicado para el asistente. Lo que ve está acotado por `GRANT` columna por columna a dos roles de solo lectura. El SQL pasa por un validador léxico propio (lista blanca de comienzo y listas negras de funciones y palabras clave) y se ejecuta en una transacción nueva `READ ONLY`, con el actor fijado en un GUC transaction-local, timeouts y `LIMIT tope+1`. Después cada columna se clasifica por (OID, attnum) para enmascarar antes de redactar.

### Cited Findings
- **Objetos visibles** (manifiesto deny-by-default):
  - schemas expuestos: `identity`, `designaciones` y `portal`;
  - schemas denegados: `audit`, `storage`, `asistente` y `public`;
  - 21 tablas concedidas: 8 de identity (carreras, materias, personas, users, roles, user_roles, permisos, rol_permisos), 7 de designaciones (cargos, dedicaciones, periodos, pedidos, pedido_adjuntos, pedido_historial, designaciones) y 6 de portal (perfiles, educaciones, certificaciones, experiencias, habilidades, docente_habilidades);
  - 6 tablas denegadas explícitamente: `designaciones.idempotencia_comandos`, `identity.__EFMigrationsHistory`, `portal.contactos`, `portal.cvs`, `portal.proyectos` y `portal.proyecto_documentos`;
  - "Ningún GRANT USAGE sobre secuencias ni GRANT EXECUTE sobre funciones, salvo las cuatro funciones SECURITY DEFINER de resolución del actor".

  — [manifiesto-privilegios.json](database/asistente/manifiesto-privilegios.json)
- **No hay vistas dedicadas.** El modelo consulta tablas base. El dominio se le explica con `COMMENT ON` que viven en el DDL de cada módulo dueño (`database/identity/013_*.sql`, `database/designaciones/010_*.sql`, `database/portal/004_*.sql`): "No son documentación: el proveedor de esquema los lee del catálogo y los pone en el prompt". — [README del módulo](backend/src/Modules.Asistente/README.md); listado de `database/`
- **Funciones SQL del asistente** (no las llama el modelo; las usan las policies RLS): `identity.asistente_actor`, `asistente_es_global`, `asistente_materias_visibles` y `asistente_tiene_permiso` (en `012_identity_funciones_asistente.sql`), más `asistente_persona` (016) y `asistente_alcanza_a` (017). — [database/identity/](database/identity/012_identity_funciones_asistente.sql)
- **Validador** (`ValidadorDeSql` + `TokenizadorSql`), "segunda capa de defensa... La primera es el motor":
  - Comienzos admitidos: solo `select` y `with` (lista blanca).
  - Funciones prohibidas: reloj (`now`, `current_date`... 8 funciones), `set_config`, `current_setting`, lectura de archivos (`pg_read_file`, `lo_import`...), `dblink*`, `pg_sleep*` y `pg_terminate_backend`/`pg_cancel_backend`.
  - Palabras clave prohibidas: `insert`, `update`, `delete`, `create`, `alter`, `drop`, `grant`, `copy`, `into`, `do`, `call`, `execute`, `explain`, `begin`, `set`, `lock`, `vacuum`, entre otras.
  - Prefijo `pg_` y `information_schema` denegados por nombre.
  - Una sola sentencia.
  - El contenido de los identificadores entre comillas dobles se emite como token y se chequea contra funciones. Motivo: `"set_config"(...)` permitía fijarse otro actor y saltear RLS ("26 filas contra 138" en el prototipo).

  — [ValidadorDeSql.cs](backend/src/Modules.Asistente/Application/CarrilSql/ValidadorDeSql.cs); [design carril-sql D7–D8](openspec/changes/asistente-carril-sql/design.md)
- Un rechazo del validador termina en "no contestable, SIN reintento ciego". La opción `RepararConsultaFallida` da una ronda de reparación solo ante un error del motor, y el error llega saneado. — [README del módulo](backend/src/Modules.Asistente/README.md); [modelo-local.md §6](docs/architecture/modelo-local.md)
- **Ejecución**:
  1. Conexión y transacción nuevas por cada ejecución.
  2. `PreambuloDelActor.AplicarAsync` con `TimeoutDeSentenciaMs` (default 8000 ms); `TimeoutDeComandoSegundos` = 15 del lado del cliente.
  3. `set_config('app.asistente_user_id', <identity.users.id>, true)`, transaction-local.
  4. Envoltura `SELECT * FROM (<sql>) AS resultado_asistente LIMIT tope+1`, con `TopeDeFilas` = 200. La fila sonda detecta truncado y se descarta.
  5. Clasificación de columnas por (OID, attnum).
  6. SQLSTATE 42501 del motor → abstención, "nunca error crudo".

  — [EjecutorDeConsulta.cs](backend/src/Modules.Asistente/Infrastructure/EjecutorDeConsulta.cs); [OpcionesAsistente.cs](backend/src/Modules.Asistente/Configuracion/OpcionesAsistente.cs); [design carril-sql D9–D10](openspec/changes/asistente-carril-sql/design.md)
- "READ ONLY es la tercera capa, después del rol y del validador". — [design carril-sql D10](openspec/changes/asistente-carril-sql/design.md)
- Fecha: "hoy" se inyecta como literal en el mensaje de usuario; el reloj está prohibido en el validador (reproducibilidad y seguridad). — [design carril-sql D6](openspec/changes/asistente-carril-sql/design.md)
- Literales: el prefijo lleva los valores reales de un puñado de columnas de catálogo cerrado (`LectorDeValoresDeCatalogo`, lista declarada y no detectada). `identity.materias` queda afuera a propósito, porque no es un catálogo cerrado. Caso motivador: "ingeniería informática" vs "Ingeniería **en** Informática" daba cero filas con SQL válido. — [README del módulo](backend/src/Modules.Asistente/README.md)
- Menciones explícitas `@materia`/`#docente`: el id nunca llega al modelo. Se usan marcadores `$refN`, que el validador tokeniza y el ejecutor liga como parámetro `uuid`. — [README del módulo](backend/src/Modules.Asistente/README.md)
- Redacción:
  - temperatura 0,3, sobre filas ya enmascaradas;
  - el indicador de truncado es booleano y "tiene prohibido afirmar conteos cuando está en verdadero";
  - nunca se declara cuántas filas quedaron afuera (canal de inferencia);
  - los rechazos no mencionan esquema, tablas ni columnas (D15).

  — [design carril-sql D9, D14, D15](openspec/changes/asistente-carril-sql/design.md)
- El SQL viaja al cliente solo con el permiso `asistente.ver_consulta`. — [README del módulo](backend/src/Modules.Asistente/README.md)

### Inferences
- La "superficie" que el modelo debe dominar es el esquema físico (con nombres mixtos inglés/español, ver TD-005 en [tech-debt.md](docs/quality/tech-debt.md)), no una capa semántica curada. La semántica se aporta con comentarios de columna, valores de catálogo, ejemplos y, opcionalmente, el glosario.

### Gaps
- La auditoría (2026-09-06) recomendaba validar por AST en vez de por tokens; no encontré que se haya migrado a un parser AST. El validador sigue siendo léxico.

## 3. Autorización y alcance, enmascaramiento de PII, sensibilidad, abstención y defensas contra inyección de prompt

### Takeaway
La autorización la impone PostgreSQL, no la aplicación:
- dos roles de solo lectura (básico y con PII), con GRANT por columna contra un manifiesto verificado en CI;
- policies RLS que conjuntan el permiso de dominio leído en vivo (`identity.asistente_tiene_permiso`) con el ámbito del actor (global, carrera o materia);
- el actor viaja en un GUC transaction-local.

El enmascaramiento es una frontera de salida aparte, que decide qué valores llegan al proveedor del LLM. La abstención está codificada en 7 casos para no afirmar nada falso, sobre todo para no confundir "cero filas por RLS" con "no hay". Contra la inyección de prompt, el contenido no confiable que vuelve al modelo es un riesgo reconocido y solo mitigado en parte.

### Cited Findings
- Invariante #14 ("Frontera de motor para consulta generada"): un módulo puede consultar schemas ajenos sin pasar por `Contracts` "únicamente si la frontera está sostenida por el motor de base de datos y es falsable: rol de Postgres sin GRANT de mutación, GRANT enumerados columna por columna contra un manifiesto versionado, policies RLS que conjunten el permiso de dominio, y tests que fallen". — [design fundaciones D2](openspec/changes/asistente-fundaciones/design.md)
- Dos roles: `asistente_ro_<ambiente>` (sin documento, cuil, fecha_nacimiento, teléfono ni upn) y `asistente_ro_pii_<ambiente>` (con ellos). "Con dos roles, un usuario sin el permiso **no puede leer la columna**". La conexión PII exige permiso **y** alcance global, porque `identity.personas` no tiene RLS. — [design fundaciones D3, D9](openspec/changes/asistente-fundaciones/design.md); [manifiesto-privilegios.json](database/asistente/manifiesto-privilegios.json); [README del módulo](backend/src/Modules.Asistente/README.md)
- El manifiesto falla en CI en tres direcciones: privilegio no declarado, privilegio declarado inexistente y tabla sin clasificar. Caso motivador: `idempotencia_comandos.response_body` (JSONB con datos de personas) se habría concedido con `GRANT ON ALL TABLES`. — [design fundaciones D4](openspec/changes/asistente-fundaciones/design.md)
- RLS: las policies de `designaciones.pedidos`, `designaciones` (tabla), `pedido_historial` y `pedido_adjuntos` aplican `identity.asistente_tiene_permiso('designaciones.ver') AND materia_id IN (SELECT identity.asistente_materias_visibles())`. — [009_designaciones_rls_asistente.sql](database/designaciones/009_designaciones_rls_asistente.sql)
- El permiso va dentro del predicado porque "el rol `docente` tiene ámbito de materia pero sus únicos permisos son `portal.ver` y `portal.editar`: una policy que mirara solo el ámbito le abriría pedidos... que la API REST le niega con 403". — [design fundaciones D5](openspec/changes/asistente-fundaciones/design.md)
- Permisos leídos en vivo, sin listas de roles hardcodeadas, porque Secretaría puede crear roles en runtime. Las policies usan `ENABLE` y nunca `FORCE`, porque `FORCE` tumbaría el backend, que conecta como dueño. — [design fundaciones D6–D7](openspec/changes/asistente-fundaciones/design.md)
- Portal: las 6 policies llaman a `identity.asistente_alcanza_a(persona_id)`, que evalúa "es mi propio perfil OR (permiso AND global) OR (permiso AND la persona tiene designación vigente en una de mis materias visibles)". — [017_identity_funcion_alcance_de_persona.sql](database/identity/017_identity_funcion_alcance_de_persona.sql); [005_portal_rls_ambito.sql](database/portal/005_portal_rls_ambito.sql); [design portal-por-ambito](openspec/changes/asistente-portal-por-ambito/design.md)
- La identidad sale de `ICurrentUser.UserId` (`identity.users.id`), nunca del `oid` de Azure ni de nada que envíe el cliente. `identity.asistente_actor()` levanta excepción si el UUID no es un usuario activo. — [design carril-sql D11](openspec/changes/asistente-carril-sql/design.md)
- Permisos propios del asistente:
  - `asistente.consultar`: puerta del turno.
  - `asistente.ver_consulta`: ver el SQL.
  - `asistente.leer_historial_ajeno`: soporte, auditado.
  - `asistente.administrar`: sys_admin.
  - Además hay un acceso operativo por rol y usuario (`asistente.presupuesto_rol.acceso_habilitado` y `acceso_usuario_revocado`) que "sólo puede **restringir**".

  — [README del módulo](backend/src/Modules.Asistente/README.md); [design acceso-granular](openspec/changes/asistente-acceso-granular/design.md)
- **Enmascaramiento**:
  - `manifiesto-sensibilidad.json` clasifica 151 columnas: 143 `publica`, 5 `sensible-valor` y 3 `sensible-texto`.
  - Las 5 `sensible-valor` son personas.documento, cuil, fecha_nacimiento y telefono, y users.upn. Al modelo le llega «documento 1», un contador y no un hash.
  - Las 3 `sensible-texto` son pedidos.justificacion, pedidos.tipo_baja_detalle y pedido_historial.comentario. Se suprimen enteras, nombre incluido.
  - La columna se identifica por (OID de tabla, attnum), no por el alias.

  — [manifiesto-sensibilidad.json](database/asistente/manifiesto-sensibilidad.json); [README del módulo](backend/src/Modules.Asistente/README.md)
- Asimetría declarada: "El enmascaramiento protege el camino de VUELTA, no el de ida. La pregunta cruda del usuario viaja al proveedor". — [manifiesto-sensibilidad.json](database/asistente/manifiesto-sensibilidad.json)
- Opt-in `RedaccionSinEnmascarar` (default false): solo con proveedor `local` y sin cassettes manda valores reales a la redacción. Exige hardware del Departamento y `--cache-ram 0` en llama-server, por el issue llama.cpp #27148 de KV entre slots. — [README del módulo](backend/src/Modules.Asistente/README.md); [modelo-local.md §6](docs/architecture/modelo-local.md)
- **Fallas conocidas del enmascarado (TD-009)**: una expresión sobre una columna (`substring(telefono,1,4)`, `lower(comentario)`) deja el par (OID, attnum) vacío y se trata como pública. Las 3 `sensible-texto` las lee también el rol básico, así que "el texto libre viaja al proveedor". Se decidió dejarlo abierto el 2026-09-08 (ARS-105). — [tech-debt.md TD-009](docs/quality/tech-debt.md); [Auditoría Eje 3](docs/quality/auditoria-asistente.md)
- **Abstención**: "Los siete casos de abstención (RF-17)" en `PoliticaDeAbstencion`.
  - Ante resultado vacío con actor global hay un reintento. Con actor acotado no, porque "RLS convierte 'no tenés permiso' en cero filas... exactamente la misma firma".
  - `AlcanzaTodo` se decide por turno y por dominio tocado (designaciones vs portal). Antes era un booleano y producía falsos «no encontré ningún registro».
  - Hay plantillas de rechazo por motivo (`fuera_de_tema`, `otro_sistema`, `muy_general`, `no_cubierto`).

  — [PoliticaDeAbstencion.cs](backend/src/Modules.Asistente/Application/Abstencion/PoliticaDeAbstencion.cs); [design carril-sql D13](openspec/changes/asistente-carril-sql/design.md); [README del módulo](backend/src/Modules.Asistente/README.md)
- **Inyección de prompt**: la auditoría lo declara como riesgo residual que "**ninguna** de esas capas cubre". El asistente lee texto libre de usuarios (justificativos, comentarios), "contenido **no confiable** que vuelve al contexto del modelo", y "la _lethal trifecta_ de Willison... está completa hoy". La mitigación estructural es que el modelo solo puede leer bajo RLS con un rol sin mutación; el validador "sube el costo de un ataque; el motor es lo que lo hace inútil". — [Auditoría Eje 3](docs/quality/auditoria-asistente.md); [design carril-sql Riesgos](openspec/changes/asistente-carril-sql/design.md)
- Hallazgos de seguridad de la auditoría, marcados como resueltos al 2026-09-08: el escape `U&"…"` que evadía el validador y el `pg_catalog` alcanzable por la consulta generada. — [Auditoría (encabezado)](docs/quality/auditoria-asistente.md)
- Cachés y fuga entre usuarios:
  - semantic caching sobre respuestas descartado ("Letal con RLS");
  - la caché de SQL no guarda filas;
  - idempotencia acotada por (actor, clave);
  - candado de turno por actor (advisory lock).

  — [definición, §6](docs/product/designs/asistente-conversacional-definicion.md); [README del módulo](backend/src/Modules.Asistente/README.md)

### Inferences
- El modelo de seguridad es "el LLM es un usuario de base de datos no confiable con privilegios mínimos". Cualquier alternativa (tools o MCP sobre endpoints) tendría que replicar o reutilizar este acotamiento por ámbito, que hoy vive en RLS para el asistente y en código de aplicación para la API REST (ver §9).

### Gaps
- No encontré pruebas adversariales de inyección indirecta (instrucciones dentro de `pedido_historial.comentario`) documentadas como test.

## 4. Por qué se descartó pasar por `Modules.X.Contracts` y otras alternativas descartadas

### Takeaway
El equipo eligió SQL generado contra el motor y descartó explícitamente generar llamadas a métodos de `Contracts`, porque sería "otro sistema" y porque un método de consulta libre en `IConsultasIdentity` convertiría la barrera en una superficie de datos genérica. Igual existe un carril determinista hacia la API (épica E6) construido a medias: un catálogo de 5 intenciones con un enrutador en sombra, sin conectar, a la espera de que el equipo apruebe los edges (ARS-46). También quedaron descartados: vector store y embeddings, clasificar la intención con el LLM, schema pruning, multiagente, self-consistency, semantic cache y otros.

### Cited Findings
- Cita textual: "de los cinco pasos del checklist para agregar un edge, «implementar vía Contracts + DI» es el único que este módulo no puede cumplir, y no por comodidad: pasar por Contracts significaría que el modelo genere llamadas a métodos en vez de SQL, que es otro sistema. Agregarle a `IConsultasIdentity` un método de consulta libre sería peor: convertiría la barrera de lectura en una superficie de datos de propósito general para los cuatro módulos." — [design fundaciones D1](openspec/changes/asistente-fundaciones/design.md); repetido en [definición §7](docs/product/designs/asistente-conversacional-definicion.md) ("y no por pereza")
- Otros lugares descartados para el asistente: `ArsDocendi.Shared` (prohíbe I/O), dentro del Host ("el sitio menos restringido") y servicio aparte (un contenedor más por ambiente de PR). — [design fundaciones D1](openspec/changes/asistente-fundaciones/design.md)
- Tabla de descartes de la definición, "No volver a proponerlos sin evidencia nueva":
  - Clasificar la intención con el LLM: "60% de F1 en triage de 5 clases; 77,4% en 9 vías".
  - Small talk generativo.
  - Sugerencias generadas por el LLM.
  - Semantic caching sobre respuestas: "Letal con RLS".
  - Schema pruning o selector de esquema: "Rompe la estabilidad del prefijo cacheado".
  - Descomposición multiagente: "Factor ~50× de costo".
  - Self-consistency: "20–30× de costo para comprar ~2 puntos".
  - Score de confianza numérico.
  - `FORCE RLS`.
  - Rate limit por IP.
  - Clave real en ambientes de PR.
  - Umbral agregado como gate.

  — [definición §6](docs/product/designs/asistente-conversacional-definicion.md)
- Vector store: "Con un catálogo del orden de decenas de ejemplos, un vector store es infraestructura nueva —un servicio más, un modelo de embeddings más, una llamada de red más por turno— para elegir entre pocas opciones. La similitud léxica corre en proceso, cuesta cero y es **inspeccionable**". — [design carril-sql D4](openspec/changes/asistente-carril-sql/design.md)
- Recuperación de valores (propuesta ARS-165, opt-in): "No model call, no embeddings, no PostgreSQL extension". Descarta `pg_trgm` porque exige una extensión, GRANT EXECUTE y un umbral poco razonable; elige Damerau-Levenshtein en proceso sobre entidades visibles al actor. — [proposal](openspec/changes/asistente-recuperacion-de-valores/proposal.md); [design D3](openspec/changes/asistente-recuperacion-de-valores/design.md)
- Schema linking: "la literatura es mixta: en esquemas de tamaño institucional, el schema linking empeoró a modelos de 7–32B (nl2sql-onprem-bench). Además rompe el prefijo estable". Self-consistency en on-prem: "+0,13 puntos con p95 de 10,6 a 50 s". — [modelo-local.md §6](docs/architecture/modelo-local.md)
- Carril determinista hacia la API:
  - Motivación: para preguntas que la API "ya sabe responder", generar SQL "es pagar un modelo para reconstruir una consulta que ya está escrita, probada y con sus reglas de negocio adentro".
  - Catálogo cerrado de 5 intenciones con términos y slots, cada una con un destino lógico como cadena (por ejemplo `designaciones/pedidos-por-persona`) que "no lo llama".
  - Las 5 intenciones: `estado-del-pedido-de-una-persona`, `pedidos-en-un-estado`, `pedidos-de-una-novedad`, `plantel-de-una-materia` y `designaciones-de-un-cargo`.

  — [proposal catálogo de intenciones](openspec/changes/asistente-catalogo-de-intenciones/proposal.md); [intenciones.json](backend/src/Modules.Asistente/Recursos/intenciones.json)
- Política del enrutador: "El default es SQL, nunca API... Enrutar mal hacia la API devuelve cero filas, y «cero filas» es indistinguible de «no hay»". Corre en modo sombra porque los edges hacia `Contracts` requieren acuerdo del equipo, y "pedir esa aprobación sin datos es pedirla a ciegas". — [proposal/design enrutador](openspec/changes/asistente-enrutador-de-dominio/proposal.md)
- Estado: "E6 es la que falta... `Modules.Asistente.Contracts` no tiene ningún `.cs`" (README, desactualizado en este punto: hoy hay `IConsultaDeMantenimiento.cs` e `IConsultasDeAuditoriaDeAdministracion.cs` en ese proyecto). La tabla dorada del enrutador sobre los datasets da **0 de 39** capturas. — [README del módulo](backend/src/Modules.Asistente/README.md); `backend/src/Modules.Asistente.Contracts/`
- El invariante #14 "sigue sin acuerdo del equipo" según la auditoría (tarea 0.1 de fundaciones abierta). — [Auditoría Eje 7](docs/quality/auditoria-asistente.md)

### Inferences
- El rechazo de `Contracts` se argumentó contra un diseño en el que **el modelo genera llamadas a métodos** como sustituto completo del SQL. No se evaluó por escrito un esquema híbrido de tool-calling sobre endpoints, más allá del carril determinista sin LLM. La única pieza tipo "herramientas" es ese catálogo de intenciones, que reconoce por reglas y no por el LLM.
- Que la cobertura offline del catálogo sea 0/39 sugiere que las preguntas del dataset no coinciden con lo que los endpoints existentes resuelven de forma directa. Ojo: el corpus se escribió para medir traducción a SQL, como el propio README advierte.

### Gaps
- No hay documentación sobre MCP ni sobre function calling del LLM como alternativa: una búsqueda de "MCP", "tool use" y "function calling" en `openspec/changes/asistente-*` y `docs/architecture` no devolvió nada relevante.

## 5. Proveedores de modelo, modelo local elegido, resultados del evaluador por eje, latencia, VRAM, concurrencia y caché

### Takeaway
Un único puerto agnóstico (`IProveedorDeModelo`) tiene tres adaptadores: `simulado` (default), `anthropic` (default `claude-sonnet-5`) y `local` (cualquier servidor OpenAI-compatible: vLLM, llama-server o SGLang). Cassettes VCR graban y reproducen las respuestas de Anthropic. El modelo local elegido es Qwen3-8B: AWQ en vLLM para la RTX 5070 (estimado) y Q4_K_M en llama-server para la 3070 (medido). En la 3070, con el perfil optimizado, acierta 26/34 en capacidad, 12/15 en robustez, 10/11 en diálogo y 18/20 en social, con p50 de 2,4–2,9 s por turno y 7,4 GiB de VRAM. La línea de base de Claude está en torno a 30/32, 14/15, 11/11 y 20/20, aunque las cifras difieren entre el README y los archivos JSON.

### Cited Findings
- Proveedores: `simulado` ("Default de todos los ambientes. Determinista, sin red"), `anthropic` (requiere clave) y `local` (requiere `UrlDelProveedorLocal`). "Sumar uno nuevo... es una clase en `Infrastructure` y un brazo más del `switch`". — [README del módulo](backend/src/Modules.Asistente/README.md)
- Configuración de Anthropic:
  - Modelo default `claude-sonnet-5`, con esfuerzos generación=`medio`, redacción=`bajo` y reescritura=`bajo`.
  - "El esquema que el modelo maneja es chico —catorce tablas, poco más de cien columnas—... así que Opus no se paga". Haiku 4.5 queda descartado porque no acepta el parámetro de esfuerzo.
  - Separar los tres esfuerzos "bajó el p95 de 9,4 s a 6,7 s sin mover ninguno de los cuatro puntajes".
  - El prefijo viaja como bloque `system` cacheable.

  — [README del módulo](backend/src/Modules.Asistente/README.md); [proposal proveedor-anthropic](openspec/changes/asistente-proveedor-anthropic/proposal.md)
- Costo estimado con Claude: "del orden de un centavo de dólar por turno, contra dos y medio con Opus 5". — [tech-debt.md TD-008](docs/quality/tech-debt.md)
- Cassettes: grabación VCR del cuerpo crudo de Anthropic, sellados con modelo, fecha, hash del prefijo y hash del fixture. Hay 109 en `backend/tests/ArsDocendi.IntegrationTests/Cassettes`. Las líneas de base se congelaron "reproduciendo los 107 cassettes"; la discrepancia 107/109 no está explicada. — [README del módulo](backend/src/Modules.Asistente/README.md); [modelo-local.md §1](docs/architecture/modelo-local.md); [lineas-de-base/README.md](backend/eval/lineas-de-base/README.md)
- **Línea de base de Claude** (`claude-sonnet-5`, post-portal, 20 tablas), según la tabla del README: capacidad 31/32 (96,9 %), robustez 13/15 (86,7 %), diálogo 8/9 (88,9 %) y social 20/20. Los archivos JSON de esa misma línea de base dan otra cosa:
  - capacidad: 32 ítems, 22 `traduccion_correcta` + 8 `abstencion_correcta` (30 aciertos) + 2 `abstencion_sobrelo_factible`;
  - robustez: 14/15;
  - diálogo: 11/11 (10 traducciones + 1 abstención correcta);
  - social: 20/20.

  — [lineas-de-base/README.md](backend/eval/lineas-de-base/README.md); contradicho por [lineas-de-base/capacidad.json](backend/eval/lineas-de-base/capacidad.json) y los otros tres JSON
- **Local, medido el 2026-10-03** en una RTX 3070 con llama-server (build 11371), 1 slot de 16.384 tokens y KV `q8_0`. Perfil A sin optimizaciones; perfil B con optimizaciones.

  | Modelo y perfil | Capacidad (34) | Robustez (15) | Diálogo (11) | Social (20) | Falsas en capacidad | p50 | VRAM máx. |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | Qwen3-8B Q4_K_M, A | 24 | 11 | 9 | 20 | 8 | 5,1 s | 7.431 MiB |
  | **Qwen3-8B Q4_K_M, B** | **26** | **12** | **10** | **18** | **7** | **2,9 s** | 7.391 MiB |
  | XiYan-7B Q4, B | 14 | 7 | 7 | 17 | — | — | — |
  | Arctic-R1 7B Q4, B | 23 | 10 | 6 | 17 | 5 | — | — |
  | Qwen3-4B Q6_K, B | 23 | 12 | 9 | 17 | — | — | — |

  Qwen3-8B Q5_K_M "no entra". Con penalización 2,0 la capacidad normalizada de Qwen3-8B es 35,3 %. "La corrida es determinista" (3 repeticiones sin cambios). "La baja de social en el perfil B es un artefacto de la medición". — [modelo-local.md §3](docs/architecture/modelo-local.md)
- La comparación directa local vs Claude no está hecha sobre el mismo dataset: el de capacidad local tiene 34 ítems y la línea de base de Claude 32. Los especialistas "no se midieron con su plantilla nativa". — [modelo-local.md §3](docs/architecture/modelo-local.md); [lineas-de-base](backend/eval/lineas-de-base/capacidad.json)
- Expectativa de calidad: "En BIRD, un generalista de 7–9B queda 15–25 puntos debajo de Claude Sonnet 4.5"; "la única medición válida es el evaluador del repo". — [modelo-local.md §3](docs/architecture/modelo-local.md)
- Opciones medidas y dejadas apagadas:
  - `GlosarioEnElPrefijo`: 25/34 y social 17/20; empeora, no se promueve.
  - `RazonamientoEnSegundaGeneracion`: no suma aciertos y la corrida tarda 26 % más; p95 de 7,8 s → 9,6 s.

  Opción medida y adoptada: decodificación especulativa por n-gramas 6/24, con +16 % de tok/s (58,3 → 67,6), p50/p95 de 2,4 s / 6,6 s y sin VRAM extra. — [modelo-local.md §6, §8](docs/architecture/modelo-local.md)
- Ejemplos en el prefijo vs 4 por pregunta: 20 en el prefijo dan 26·12·10·18 contra 24·11·10·17 con 4 por pregunta; 28 en el prefijo dan 28·12·9·17. — [modelo-local.md §8](docs/architecture/modelo-local.md)
- **Servidor elegido para producción**: vLLM con `Qwen/Qwen3-8B-AWQ`, `--enable-prefix-caching`, `--kv-cache-dtype=fp8`, `--max-model-len=16384`, `--max-num-seqs=8`, `--gpu-memory-utilization=0.90` y thinking apagado. La alternativa es llama-server (`--parallel=4`, `--ctx-size=65536`, KV q8_0, `--cache-ram=0`). "Por qué vLLM y no llama-server": vLLM comparte el KV del prefijo de 12k entre requests y llama-server "guarda N copias". Ollama "Descartado" por un bug de VRAM en RTX 50 sobre Windows (#18581) y "menor control de slots y caché". — [compose.llm.yml](infra/compose/compose.llm.yml); [modelo-local.md §2](docs/architecture/modelo-local.md); [design proveedor-local D1](openspec/changes/asistente-proveedor-local/design.md)
- **Concurrencia**: `CompuertaDelModelo` y `ProveedorConCompuerta`. "La GPU nunca recibe más de `max-num-seqs`". La espera en cola no cuenta para el timeout ni para el breaker, y los turnos empezados tienen prioridad. Perfil 5070: `MaximoDeLlamadasConcurrentes`=8 y `EsperaMaximaEnColaSegundos`=45; perfil 3070: 2 (en la práctica 1 slot). "La compuerta es por proceso". — [design proveedor-local D4, D7](openspec/changes/asistente-proveedor-local/design.md); [.env.example](.env.example)
- Latencias estimadas en la 5070 (**[estimado]**, sin medir):

  | Carga | Latencia del turno |
  | --- | --- |
  | 1–2 en vuelo | ~3–5 s |
  | ~8 en vuelo | ~7–12 s (~35–45 tok/s por stream) |
  | Turno frío | +3–4 s de prefill |
  | 30 usuarios a la vez | encola; los últimos esperan "decenas de segundos" y a los 45 s reciben el texto de saturación |

  — [modelo-local.md §5](docs/architecture/modelo-local.md)
- Presupuesto del turno: 90 s en local ("debajo de los 100 s en que Cloudflare corta el origen") y 150 s por defecto. Timeout por llamada de 60 s. Breaker: 5 fallos y 30 s. — [design proveedor-local D7](openspec/changes/asistente-proveedor-local/design.md); [README del módulo](backend/src/Modules.Asistente/README.md)
- Cachés vigentes:
  - prefijo del proveedor (Anthropic cache control; prefix caching en vLLM/llama-server);
  - caché de SQL por (pregunta sin contexto, rol, día), opt-in, de 10 min en el perfil 3070;
  - prefijo del esquema cacheado en proceso por rol.

  Tampoco hay semantic cache de respuestas (ver §3). — [README del módulo](backend/src/Modules.Asistente/README.md); [.env.example](.env.example)
- Rama `infra/ollama`: despliega un Ollama compartido en "pc-prod (GPU + cliente Tailscale)" con `OLLAMA_MODELO=qwen3:4b`. Traefik solo admite `GET /api/version`, `GET /api/tags`, `POST /api/chat` y `POST /api/generate`; "El endpoint queda preparado para una integración posterior, no implementa el chatbot". — [ollama-compartido.md (origin/infra/ollama)](docs/operations/ollama-compartido.md)

### Inferences
- Hay tensión entre la rama `infra/ollama` (Ollama con qwen3:4b) y `modelo-local.md` (Ollama descartado; vLLM con Qwen3-8B-AWQ). Además, `ProveedorLocal` llama a `{Url}/chat/completions` (ruta OpenAI `/v1`), que las reglas de Traefik de esa rama no exponen: solo dejan pasar `/api/chat` y `/api/generate`. Sin cambios, el adaptador actual no podría usar ese endpoint (inferencia, no probada).
- Sobre las cifras locales: 26/34 en capacidad con 7 respuestas falsas significa que alrededor de 1 de cada 5 preguntas de capacidad recibe una afirmación incorrecta, contra prácticamente ninguna con Claude en su línea de base (2 abstenciones sobre lo factible, 0 traducciones incorrectas en el JSON de capacidad).

### Gaps
- No hay mediciones en una RTX 5070 ni con vLLM: todo lo de la 5070 está marcado **[estimado]**.
- No hay prueba de carga con concurrencia (el piloto c = 1, 4, 8, 12 de §7 está pendiente).
- No hay una corrida de Claude sobre el dataset vigente de 34 ítems de capacidad.

## 6. Supuestos de hardware y objetivos de concurrencia

### Takeaway
La producción prevista es una única NVIDIA GeForce RTX 5070 de 12 GB con un Intel Core i9 para atender de 2 a 30 usuarios. La 3070 de 8 GB es solo una PC de prueba para una persona. Nada de la 5070 está medido.

### Cited Findings
- "una NVIDIA GeForce RTX 5070 (12 GB GDDR7, ~672 GB/s, Blackwell de consumo `sm_120`, tensor cores FP8/FP4) con un Intel Core i9, atendiendo entre 2 y 30 usuarios". — [modelo-local.md (encabezado)](docs/architecture/modelo-local.md)
- RTX 3070: "8 GB GDDR6, ~448 GB/s, Ampere `sm_86`. No es un perfil de producción: es una sola persona probando". — [modelo-local.md §8](docs/architecture/modelo-local.md)
- VRAM en la 3070, medida el 2026-10-03 con el escritorio ocupando ~1,3 GB: 2 slots de 13.312 tokens "no entraron (falló al reservar 1.989 MiB de KV)". Con 1 slot de 16.384 la placa llegó a 7,3 GiB. Un modelo borrador Qwen3-0.6B no entra. — [modelo-local.md §8](docs/architecture/modelo-local.md)
- Presupuesto de VRAM en la 5070 (estimado):

  | Configuración | KV disponible | Capacidad |
  | --- | --- | --- |
  | vLLM AWQ (~5,9 GB) con KV FP8 | ~3,5–4 GiB | prefijo una vez ~0,85 GiB; "8 en vuelo usan ~2,5–3 GiB: entran con margen" |
  | llama-server | — | "8 slots de 16k serían ~9,6 GiB y no entran"; entran 4 |
  | Qwen3-14B-AWQ | — | "Inviable" |

  "La GPU no debe manejar el monitor". — [modelo-local.md §4](docs/architecture/modelo-local.md)
- 30 usuarios a un turno por minuto cada uno equivalen a "~0,5 turnos/s, dentro del agregado estimado (~250–350 tok/s de decode con 8 en vuelo)". — [modelo-local.md §5](docs/architecture/modelo-local.md)
- Producción en "Linux nativo (Ubuntu 24.04, driver NVIDIA ≥ 580 para CUDA 13)". La rama de Ollama documenta en cambio Debian 13 en pc-prod con una RTX 5070 detrás de Tailscale. — [modelo-local.md §2](docs/architecture/modelo-local.md); [ollama-compartido.md](docs/operations/ollama-compartido.md)
- Modelo de amenaza y uso: "unas treinta personas autenticadas". — [tech-debt.md TD-011](docs/quality/tech-debt.md)

### Inferences
- Con una sola GPU de consumo y 8 secuencias en vuelo como máximo, el sistema funciona más como "cola con buen p50" que como servicio con latencia garantizada bajo picos.

### Gaps
- No hay datos de distribución real de la demanda (picos simultáneos) ni del CPU o RAM del host.

## 7. Debilidades y modos de falla documentados

### Takeaway
Las debilidades principales son:
- respuestas falsas (SQL válido con lógica o literal incorrectos);
- abstenciones de más en preguntas contestables;
- sensibilidad del modelo chico a pequeños cambios de prompt (ejemplos, glosario);
- latencia y VRAM;
- un validador léxico en vez de AST;
- fugas de texto libre al proveedor por expresiones;
- riesgo de inyección indirecta;
- documentación y gobernanza desalineadas.

### Cited Findings
- "La más difícil de detectar" es "a valid query with a miscopied literal... zero rows from valid SQL is indistinguishable from 'there is none'"; "matters most for a small local model (Qwen3-8B), which copies literals less carefully than Claude". — [proposal recuperación de valores](openspec/changes/asistente-recuperacion-de-valores/proposal.md)
- Qwen3-8B (perfil B) da 7 "falsas en capacidad" sobre 34. XiYan "se abstiene de más" (6–11 contestables sin responder). Arctic-R1 Q5 responde 4 de 8 infactibles. — [modelo-local.md §3](docs/architecture/modelo-local.md)
- Fragilidad ante cambios del prompt:
  - "con ocho ejemplos más cambió la selección de 22 de los 34 ítems de capacidad y se perdieron tres aciertos";
  - pasar los ejemplos de materias a `unaccent ILIKE` hizo que «docentes de Bases de Datos» "pasó a buscar la habilidad";
  - el glosario empeoró capacidad y social (hipótesis: los sinónimos «cerrado/terminado» arrastraron preguntas al estado del pedido);
  - la especulación por n-gramas con valores por defecto cambió un veredicto.

  — [README del módulo](backend/src/Modules.Asistente/README.md); [modelo-local.md §6, §8](docs/architecture/modelo-local.md)
- Dialecto PostgreSQL: los especialistas (Arctic-R1, OmniSQL) "están entrenados sobre SQLite: pierden mucho en PostgreSQL". `ILIKE` "ignora mayúsculas pero NO tildes", lo que obliga a usar `public.unaccent` de los dos lados. — [modelo-local.md §3](docs/architecture/modelo-local.md); [InstruccionesDeGeneracion.cs](backend/src/Modules.Asistente/Infrastructure/Catalogo/InstruccionesDeGeneracion.cs)
- Truncado por techo de tokens: con razonamiento, el modelo "puede gastar el presupuesto pensando y cortar el JSON". Se mide como `truncado_en_generacion`. — [README del módulo](backend/src/Modules.Asistente/README.md)
- El prefijo no se invalida solo: "una migración que cambie el esquema exige reiniciar". Ya hubo corpus de cassettes irreproducible (TD-021, 120 cassettes) tras un cambio de esquema; se resolvió con una corrida financiada. — [README del módulo](backend/src/Modules.Asistente/README.md); [tech-debt.md TD-021](docs/quality/tech-debt.md)
- Comentarios del esquema que le mienten al modelo: los `CHECK` y comentarios dicen «Categoría 0» a «Categoría 6», pero el catálogo tiene de 1 a 6 (TD-025). — [tech-debt.md TD-025](docs/quality/tech-debt.md)
- Validador léxico ("lista negra léxica donde el estado del arte pide un AST"). TD-009: fuga por expresión sobre `sensible-texto`. Inyección indirecta por texto libre. Preámbulo del actor duplicado 4 veces y timeouts por defecto en 3 de 4 conexiones (estado al 2026-09-06; parte se resolvió después, por ejemplo `PreambuloDelActor`). — [Auditoría Ejes 2–3](docs/quality/auditoria-asistente.md)
- Bugs del ecosistema en `sm_120`: FlashInfer con KV FP8 produce texto incoherente (#41651). Prefix caching frágil en modelos híbridos (Qwen3.5-9B). Thinking que no se apaga (#35574, #37794). — [modelo-local.md §2–§3, §7](docs/architecture/modelo-local.md)
- Ítem que oscila en vivo (`dia-003-pivote-duro#1`) produce "regresiones falsas con lock por ítem". — [lineas-de-base/README.md](backend/eval/lineas-de-base/README.md)
- Gobernanza: el invariante #14 sigue sin acuerdo, hay 19 changes sin archivar y la definición sigue en `draft`. — [Auditoría Eje 7](docs/quality/auditoria-asistente.md)
- Un docente sin designación vigente queda invisible para roles no globales: "un jefe de cátedra deja de ver a su docente justo cuando lo necesita para renovarlo". — [design portal-por-ambito D2](openspec/changes/asistente-portal-por-ambito/design.md)

### Inferences
- Varias debilidades nacen de que el modelo escribe SQL libre sobre el esquema físico: literales, joins y dialecto. Son las que una capa semántica o tools parametrizadas evitarían por construcción, a cambio de menor cobertura.

### Gaps
- No hay telemetría de producción sobre la tasa real de respuestas falsas; solo el evaluador offline de 80 ítems.

## 8. Tamaño del dominio, catálogo de intenciones y tipo de preguntas

### Takeaway
El dominio es chico: 21 tablas concedidas en 3 schemas y unas 150 columnas clasificadas, con 20 ejemplos SQL, 24 términos de glosario y 5 intenciones deterministas. Las preguntas son consultas administrativas de gestión docente: plantel por materia, conteos de nombramientos, estado de pedidos de designación, períodos, habilidades y certificaciones del portal docente. El evaluador tiene 80 ítems.

### Cited Findings
- 21 tablas concedidas (8 identity, 7 designaciones, 6 portal) y 6 denegadas. El manifiesto de sensibilidad clasifica 151 columnas. — [manifiesto-privilegios.json](database/asistente/manifiesto-privilegios.json); [manifiesto-sensibilidad.json](database/asistente/manifiesto-sensibilidad.json)
- Hay cifras distintas en la documentación: el README del módulo dice "catorce tablas, poco más de cien columnas" (anterior a portal) y las líneas de base hablan de un prefijo de "20 tablas". — [README del módulo](backend/src/Modules.Asistente/README.md); [lineas-de-base/README.md](backend/eval/lineas-de-base/README.md)
- Intenciones: 5, con destinos lógicos `designaciones/pedidos-por-persona`, `pedidos-por-estado`, `pedidos-por-novedad`, `designaciones-por-materia` y `designaciones-por-cargo`. — [intenciones.json](backend/src/Modules.Asistente/Recursos/intenciones.json)
- Datasets de evaluación: capacidad 34 ítems, robustez 15, diálogo 5 diálogos (11 turnos) y social 20; en total 80 ítems puntuados, según `modelo-local.md`. — [backend/eval/datasets/](backend/eval/datasets/capacidad.json); [modelo-local.md §3](docs/architecture/modelo-local.md)
- Ejemplos de capacidad:
  - «¿Qué carreras dicta el Departamento?»
  - «¿En qué asignatura hay más gente nombrada?»
  - «¿Quiénes dictan Bases de Datos?»
  - «¿Cuántos nombramientos siguen abiertos?»
  - «¿Qué solicitudes puedo ver de mi carrera?»
  - «¿Cuántas solicitudes de baja se presentaron?»
  - «¿Qué solicitudes están marcadas como urgentes?»
  - «¿Qué docentes saben Kubernetes?»
  - «¿A qué docentes se les vence una certificación en 2026?»
  - Infactibles: «¿Cuál es el sueldo de cada profesor?», «¿En qué aula se dicta Sistemas Operativos?»
  - Ambiguas: «¿Quién dicta Análisis Matemático?» (existe en tres carreras), «¿Qué nombramiento tiene Gómez?»
  - Menciones: «¿En qué materias está designado #Suárez?»

  — [capacidad.json](backend/eval/datasets/capacidad.json)
- Robustez: errores de tipeo («carogs», «Bases de Dattos»), sin acentos, telegráficas («solicitudes urgentes») y coloquiales («che, ¿qué carreras hay...?»). — [robustez.json](backend/eval/datasets/robustez.json)
- Diálogo: seguimiento («¿y cuántos hay cerrados?»), aclaración, pivote y referencia al resultado («¿y quiénes están designados en esa materia?»). — [dialogo.json](backend/eval/datasets/dialogo.json)
- La tabla `designaciones.designaciones` es "la tabla del caso de uso central: cobertura de cátedra y composición del plantel. No existe ningún endpoint equivalente". — [manifiesto-privilegios.json](database/asistente/manifiesto-privilegios.json)
- El vocabulario "No se validó con el Departamento (ARS-65 sigue abierta)". Los casos de uso tampoco están validados con un usuario real (hueco 1 de la definición). — [README del módulo](backend/src/Modules.Asistente/README.md); [definición §8](docs/product/designs/asistente-conversacional-definicion.md)

### Inferences
- Con unas 21 tablas y preguntas recurrentes de pocas familias (plantel, nombramientos, pedidos, períodos, portal), el dominio es lo bastante chico como para que una capa semántica o un conjunto de 15–30 tools parametrizadas cubra una parte grande. Hoy no hay evidencia de tráfico real que lo confirme: la tabla dorada da 0/39 con las 5 intenciones actuales.

### Gaps
- No hay corpus de preguntas reales de usuarios: la evidencia es el dataset sintético del evaluador.

## 9. Endpoints REST existentes que podrían exponerse como tools/MCP, y si autorizan por usuario

### Takeaway
Fuera del asistente hay unos 60 endpoints, la mayoría de escritura. Los de lectura útiles para un tool-calling son pocos: listar y obtener pedidos de designación (por período activo), períodos, catálogos de designaciones, docentes de administración (búsqueda y filtro por materia), roles, usuarios, permisos, auditoría y el perfil propio del portal. Todos usan `[Authorize(Policy = <permiso>)]`, y los de pedidos y docentes además acotan por ámbito en código de aplicación. No existe endpoint para designaciones vigentes ni para consultar portales docentes ajenos, que el asistente sí resuelve vía SQL.

### Cited Findings
- `api/designaciones/pedidos`:
  - `GET` (listar por `periodoId`, por defecto el período activo) y `GET {id}`, con `DesignacionesVer`;
  - comandos `POST` (crear, enviar, reenviar, aceptar, rechazar, devolver, priorizar, despriorizar), `PUT` y `DELETE`, con políticas `DesignacionesGestionar`, `DesignacionesRevisar` o `DesignacionesVer`.

  — [PedidosController.cs](backend/src/Modules.Designaciones/Api/PedidosController.cs)
- El listado de pedidos acota por ámbito en el servicio: `servicio.ListarPorAmbitoAsync(id, ct)`. `ObtenerAsync` usa `ObtenerAutorizadoAsync`. — [ServicioPedidosApi.cs](backend/src/Modules.Designaciones/Services/ServicioPedidosApi.cs)
- Otros endpoints de designaciones:
  - `api/designaciones/periodos`: CRUD completo con `PeriodosAdministrar`, incluido el `GET`;
  - `api/designaciones/catalogos`: `GET` con `DesignacionesVer`;
  - `GET api/designaciones/periodos/{id}/lote.xlsx`.

  — [PeriodosController.cs](backend/src/Modules.Designaciones/Api/PeriodosController.cs); [CatalogosController.cs](backend/src/Modules.Designaciones/Api/CatalogosController.cs); [LoteController.cs](backend/src/Modules.Designaciones/Api/LoteController.cs)
- Administración (en el Host):
  - `api/administracion/docentes`: `GET` con búsqueda, `materiaId`, `rol` y `activo`; `GET {personaId}` y `GET catalogos`, con `DocentesVer`. Todos acotados por `ObtenerMateriasVisiblesAsync` (ámbito). Las escrituras usan `UsuariosAdministrar`.
  - `api/administracion/usuarios`, `roles`, `permisos`, `auditoria`, `catalogos` y `sistema/estado`, cada uno con su permiso.

  — [DocentesController.cs](backend/src/ArsDocendi.Host/Api/DocentesController.cs); `backend/src/ArsDocendi.Host/Api/*Controller.cs`
- Portal: `api/portal/perfil` y sus subrecursos (contacto, CV, habilidades, intereses, experiencia, educación, certificaciones, proyectos). Son solo `[Authorize]` y operan sobre **el perfil propio**; no hay endpoint REST para leer perfiles ajenos. — [PortalController.cs](backend/src/Modules.Portal/Api/PortalController.cs)
- Aulas y Tareas solo exponen `GET ping`. — [AulasController.cs](backend/src/Modules.Aulas/Api/AulasController.cs); [TareasController.cs](backend/src/Modules.Tareas/Api/TareasController.cs)
- `Contracts` existentes que expondrían consultas in-process:
  - `IDesignacionesQueries.UbicarPedidosAsync(textos)`;
  - `IAdministracionDesignaciones`;
  - `IPortalQueries.ObtenerPerfilAsync(personaId)`;
  - `IAulasQueries` y `ITareasQueries`.

  `Modules.Asistente.csproj` no referencia `Modules.Designaciones.Contracts`. — `backend/src/Modules.*.Contracts/`; [Modules.Asistente.csproj](backend/src/Modules.Asistente/Modules.Asistente.csproj)
- Los endpoints de docentes "acotan los datos por separado en el controller"; por eso el asistente exige alcance global para usar la conexión con PII. — [README del módulo](backend/src/Modules.Asistente/README.md)
- `docs/architecture/api-contracts.md` documenta los contratos HTTP: 44 líneas que mencionan rutas o endpoints. — [api-contracts.md](docs/architecture/api-contracts.md)

### Inferences
- Para una alternativa basada en tools o MCP sobre endpoints, la autorización por usuario existe (políticas por permiso más acotamiento por ámbito en servicios de pedidos y docentes), siempre que el tool se invoque con la identidad del usuario final y no con una cuenta de servicio.
- La cobertura funcional de lectura vía REST es mucho menor que la del SQL. Faltan:
  - designaciones vigentes (plantel, nombramientos abiertos o cerrados, horas);
  - agregaciones y conteos;
  - historial de pedidos con filtros arbitrarios;
  - portales docentes ajenos (habilidades, certificaciones, educación);
  - pedidos de períodos no activos sin conocer el id.

  Muchas preguntas del dataset (cap-003, cap-005, cap-006, cap-010, cap-022, cap-025–029) no tienen endpoint que las responda hoy, así que habría que crear endpoints o queries nuevos.
- `PeriodosController` exige `PeriodosAdministrar` incluso para el `GET`, por lo que un usuario común no podría usarlo como tool para «¿qué ciclos hay?», mientras que vía SQL sí lo ve (`designaciones.periodos` concedida sin RLS).

### Gaps
- No revisé en detalle cada servicio de administración para confirmar el acotamiento por ámbito más allá de docentes y pedidos.
- No verifiqué si los DTOs REST exponen PII (documento, teléfono) que un tool enviaría al LLM sin el enmascarado actual.
