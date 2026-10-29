# Design

## Context

- Todo evento del circuito pasa por `ServicioPedidos.RegistrarEnHistorial`, dentro de
  `UnidadDeTrabajo.EjecutarEnTransaccionAsync` (crear, editar, transiciones; el pase a `en_lote` es
  la aceptación de Decanato, que materializa designaciones en la misma transacción).
- `ServicioLoteDesignaciones.ExportarAsync` lee el período en una transacción `RepeatableRead` y
  usa `ExcepcionAplicacion(TipoErrorAplicacion.Conflicto, ...)` para los 409.
- `IDiagnosticoIntegridadModulo` ya compone hallazgos de los módulos (clase `"hallazgo"`) en
  `ArsDocendi.Migraciones/DiagnosticoIntegridad.cs`.
- La app se conecta con un único rol que también es dueño de los schemas: cualquier protección en
  base es saltable por ese rol si deshabilita triggers. Por eso la defensa real es la firma con
  clave fuera de la base más el ancla en el documento exportado; los triggers suben el costo y
  evitan errores, no son la garantía.
- `Pedido.Version` es `xmin` (rowversion): cambia en cada UPDATE y no sirve para el estado canónico.

## Goals / Non-Goals

**Goals:**

- Detectar con certeza toda alteración del circuito hecha fuera de la app, incluida la de alguien
  con acceso total a la base pero sin la clave.
- Que una alteración posterior a un lote exportado sea demostrable contra el documento aunque el
  atacante tenga base y clave.
- Costo mínimo: sin dependencias nuevas, sin cambios en Contracts ni frontend.

**Non-Goals:**

- Impedir a un superusuario modificar datos (imposible desde la propia base).
- Sellar `designaciones.designaciones` (estado vigente mutable por administración) ni otros módulos.
- UI de verificación, WORM o sellado de tiempo RFC 3161.

## Decisions

### D1. Cadena de hashes + HMAC + ancla, no blockchain distribuida

No hay partes desconfiadas que necesiten consenso; hay una institución y un atacante interno.
Lo útil de una blockchain es el encadenamiento (borrar o editar un eslabón rompe los siguientes),
y eso se logra con una tabla. _Alternativas_: blockchain/ledger externo (infraestructura y
dependencias desproporcionadas); `pgaudit`/logs del servidor (el mismo DBA los controla); firma
asimétrica Ed25519 (descartada por ahora: HMAC alcanza mientras el verificador es la propia app; se
puede migrar con `version_clave`).

### D2. Una cadena por período

Encadenar todos los pedidos del período en una sola secuencia detecta también el borrado o la
inserción de un pedido completo, que una cadena por pedido no ve. El costo es serializar los
sellados de un período: con un `pg_advisory_xact_lock` por período el bloqueo dura lo que la
transacción del evento, y el volumen (cientos de eventos por período) lo hace irrelevante.

### D3. La app calcula y firma; la base sólo encadena y protege

- **Payload canónico** (bytes UTF-8 de un JSON con orden de propiedades fijo y fechas en ISO-8601
  UTC con precisión de microsegundos, como las guarda PostgreSQL): `periodo_id, secuencia,
historial_id, pedido_id, accion, etapa, rol_id, actor_id, comentario, creado_en, hash_estado`.
- **`hash_estado`** = SHA-256 del estado canónico del pedido tras el evento: `estado, novedad,
persona_id, materia_id, carrera_id, cargo_solicitado_id, dedicacion_solicitada_id, horas,
horas_investigacion, horas_externas, justificacion, tipo_baja, tipo_baja_detalle,
etapa_retorno, propietario_actual, prioritario, snapshot` y los adjuntos ordenados por id
  (`id, tipo, archivo_id`). Se excluye `Version` (xmin).
- **`hash`** = SHA-256(`hash_anterior` ‖ payload); génesis: `hash_anterior` = SHA-256(`periodo_id`).
- **`firma`** = HMAC-SHA256(clave[`version_clave`], `hash`).
- La base (trigger `BEFORE INSERT`) toma el advisory lock del período y exige
  `secuencia = cabeza + 1` y `hash_anterior = hash de cabeza`. La app lee la cabeza **después** de
  tomar el mismo lock (`SELECT pg_advisory_xact_lock(...)`), así nunca choca con el trigger.
- _Alternativa descartada_: calcular el hash en un trigger. Quien inserte por SQL obtendría un
  eslabón válido gratis; la firma sólo puede venir de quien tiene la clave.

### D4. El sellado empieza en `enviar`

Antes del envío el pedido es un borrador privado del Jefe de Cátedra, editable y eliminable. Sellar
desde `enviar` mantiene la eliminación de borradores sin excepciones en la cadena, y el sello de
`enviar` cubre el contenido completo vía `hash_estado`. `SelladorPedidos.SellarAsync` se llama
desde `RegistrarEnHistorialAsync` y omite los eventos con `etapa = 'borrador'`: no hay camino de
vuelta a borrador, así que la etapa del evento basta y no hace falta consultar sellos previos. El
verificador exige sello a todo evento fuera de borrador y a cualquier evento de borrador posterior
al primer sello del pedido; un pedido fuera de borrador sin ningún sello cuenta como divergente.

### D5. Protecciones en base (migración incremental)

`node scripts/migrations/scaffold.mjs Designaciones SellosIntegridadPedidos`:

- `designaciones.pedido_sellos (periodo_id, secuencia, historial_id UNIQUE, pedido_id,
hash_anterior, hash_estado, hash, firma, version_clave, created_at)`, PK `(periodo_id,
secuencia)`, FK a `periodos` y a `pedido_historial` (sin cascada). Checks de longitud 32 bytes.
- `designaciones.periodo_anclas (id, periodo_id, secuencia, hash, motivo, created_at)` con
  `motivo IN ('exportacion_lote', 'retroactivo')`.
- Función `designaciones.rechazar_modificacion()` en triggers `BEFORE UPDATE OR DELETE` de
  `pedido_sellos` y `periodo_anclas`, y `BEFORE UPDATE` de `pedido_historial` cuando la fila no
  es de borrador y cambia algún valor (el upsert idempotente del seed reescribe valores iguales).
- `BEFORE DELETE` en `pedido_historial`: rechaza salvo que el pedido padre exista y esté en
  `borrador`. La FK es `ON DELETE RESTRICT` y EF borra primero el historial cargado y después el
  pedido, así que la eliminación de borradores pasa. Un evento sellado tampoco puede borrarse
  llevando antes el pedido a `borrador`: la FK `pedido_sellos → pedido_historial` lo impide.
- `BEFORE DELETE` en `pedidos`: rechaza si `estado <> 'borrador'`. Pedidos e historial usan
  funciones separadas: PL/pgSQL no admite leer `OLD.estado` sobre una fila de historial.
- `audit.attach` sobre las dos tablas nuevas, como el resto del schema.
- Entidades `PedidoSello` y `PeriodoAncla`, mapeo en `DesignacionesDbContext` y snapshot EF
  actualizados a mano (sin `dotnet ef migrations add`).

### D6. Verificador único, tres consumidores

`VerificadorIntegridadPedidos.VerificarAsync(periodoId)` devuelve `ResultadoIntegridad(integro,
secuencia, hash, hallazgos[(categoria, id)])`. Recorre los sellos por secuencia recalculando
hash y firma, compara el historial sellable con los sellos, recalcula `hash_estado` de cada pedido
contra su último sello y comprueba que cada ancla coincide con el hash en su posición. Lo usan:

1. el endpoint `GET /api/designaciones/periodos/{id}/integridad` (`auditoria.ver`) en un
   `IntegridadController` propio: `PeriodosController` exige `periodos.administrar` a nivel de clase;
2. `ExportarAsync`, dentro de su transacción `RepeatableRead` (lectura consistente con el lote);
   si hay hallazgos lanza `Conflicto "lote-integridad-comprometida"` sin detalle; si no, inserta el
   ancla cuando la cabeza difiere de la última anclada y escribe `INTEGRIDAD · sello {n} · {hash}`
   en una celda nueva `M1` de la primera hoja, fuera de la tabla institucional. El frontend
   distingue este 409 del de período inactivo por el `type` del problema;
3. el lector `IDiagnosticoIntegridadModulo` de Designaciones, que emite
   `RegistroIntegridad("hallazgo", categoria, id, null, null)` por período.

Categorías: `sello-hash-invalido`, `sello-firma-invalida`, `sello-secuencia-rota`,
`historial-sin-sello`, `pedido-estado-divergente`, `ancla-no-reproducida`.

### D7. Clave y configuración

`Integridad:ClavesSellado` (mapa `versión → clave base64, ≥ 32 bytes`) e
`Integridad:VersionVigente`, desde variables de entorno/secretos del host. Validación al arrancar
con `ValidateOnStart`: en `Production` falla sin clave; fuera de producción, sin claves definidas,
`ClavesSellado` usa una clave fija de desarrollo (staging/PR tienen datos sintéticos). Un valor
vacío cuenta como ausente porque Compose interpola así una variable no definida. Prod recibe la
clave del secret `SELLADO_CLAVE_PROD` vía `Integridad__ClavesSellado__1`. La clave nunca se
loguea; los errores nombran la sección, no el valor.

### D8. Sellado retroactivo como comando del host

`--sellar-historial-existente`: nuevo `ModoMigracion.SellarHistorialExistente` en
`ArsDocendi.Migraciones/SolicitudMigracion.cs` (mantiene la regla de modo único). `Program.cs`
lo despacha resolviendo `SelladorPedidos` del scope (el Host ya referencia Designaciones), sin
pasar por `RunnerMigraciones` y sin interfaz nueva en Shared: no hay segunda implementación. Por
cada período, en una transacción con el lock del período, sella en orden `(creado_en, id)` los
eventos sin sello con `etapa <> 'borrador'` de pedidos que ya salieron de borrador, con el estado
actual del pedido, y registra un ancla `retroactivo` si selló algo. No corre en migraciones (la
base no tiene la clave) ni al arrancar. `spin-up.sh` y `scripts/setup.sh` lo ejecutan sólo después
de sembrar una base nueva; repetirlo en cada deploy legitimaría cambios hechos por SQL.

## Risks / Trade-offs

- [Un actor con base **y** clave reescribe la cadena completa] → Sólo detectable contra anclas
  externas: la exportación del lote imprime la cabeza. Se documenta separar custodia de la clave
  (infraestructura) del acceso a la base (DBA).
- [Superusuario deshabilita triggers] → La firma y la verificación siguen detectándolo; los
  triggers son defensa en profundidad.
- [Canonicalización frágil: un cambio de serialización invalida toda la cadena] → Serializador
  propio explícito (no el de la API), con un test que congela el hash de un evento conocido; un
  cambio de formato exige versión de payload nueva.
- [Precisión de fechas .NET vs PostgreSQL] → Truncar `CreadoEn` a microsegundos antes de persistir
  y hashear.
- [Datos sembrados o previos quedan como `historial-sin-sello` hasta correr el comando] → El
  diagnóstico lo muestra; README y runbook de deploy incluyen el paso.
- [Pérdida de la clave] → Sellos previos no verificables. La clave se respalda como cualquier
  secreto de producción; la rotación conserva versiones anteriores para verificar.
- [Serialización por período bajo carga] → Bloqueo acotado a la transacción del evento; volumen
  bajo.

## Migration Plan

1. Configurar `Integridad__ClavesSellado__1` e `Integridad__VersionVigente=1` en cada ambiente.
2. Desplegar (la migración incremental corre con el runner habitual).
3. Ejecutar `--sellar-historial-existente` una vez por ambiente (en dev, después del seed).
4. Verificar con el diagnóstico o el endpoint de integridad.

Rollback: desplegar la versión anterior de la app. La migración es aditiva; los triggers
append-only no interfieren con la versión anterior salvo que intente borrar un pedido enviado o
editar historial, cosas que hoy no hace. Retirar los triggers requiere una migración nueva.
