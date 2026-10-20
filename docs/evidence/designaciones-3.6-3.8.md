# Designaciones — evidencia 3.6–3.8

Change: `corregir-integridad-y-rendimiento-post-auditoria`.
Referencia auditada: `f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b`.
Ensayos sobre PostgreSQL 18 de Testcontainers, bases independientes y seed
`sintetico.sql`; no se usaron bases compartidas ni SGA/PII real.
El checklist se deja al parent: este documento no marca tareas como terminadas.

## Resultados comprobados

Los contadores se extrajeron del XML TRX y se contrastaron con sus entradas
`UnitTestResult`; no se suman ejecuciones distintas como una sola suite.

| Ejecución                                                                                              | Resultado                                                                   | Archivo bajo `.artifacts/apply-post-auditoria/designaciones-final/` |
| ------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Backend completo de la DLL final                                                                       | 386/386 PASS; 0 fallos/omitidas; 386 resultados enumerados                  | `results/backend-orden-locks-final.trx`                             |
| Subgrupo Designaciones + administración docentes/usuarios + Microsoft + mapper dentro de esa ejecución | 154/154 PASS                                                                | `results/backend-orden-locks-final.trx`                             |
| TDD y mapper con orden de locks corregido                                                              | 37/37 PASS; 0 fallos/omitidas                                               | `results/orden-locks-green.trx`                                     |
| Frontend Designaciones + remote adapters                                                               | 212/212 PASS; 0 fallos/pendientes                                           | `frontend-designaciones.json`                                       |
| Build Release de la solución, salida aislada                                                           | 0 errores; 8 advertencias xUnit1051 preexistentes en NumeracionPedidosTests | `orden-locks-build-green.log`                                       |
| Host compilado `--validar-recursos`                                                                    | `compatible=true`, recursos verificados                                     | `recursos.log`                                                      |

Además, OpenSpec estricto del change y `git diff --check` del alcance pasan.
Se comprobaron las 18 referencias locales de los documentos tocados sin destinos
faltantes; el mapping apunta a 11 archivos de tests existentes.

Los TRX anteriores se conservan como evidencia de la evolución, no como sustituto
de la compilación final. La ejecución completa de 386 tests y los 37 focalizados
acreditan el último guard de orden de locks; capturan SQL/cardinalidades en `StdOut`.
Las cuatro intercalaciones de CoordinacionAdministracionTests pasan en la DLL final.
La DLL final está bajo
`bin/ArsDocendi.IntegrationTests/release/ArsDocendi.IntegrationTests.dll`.
SHA-256 final:
`c79bddc006e9d5bb4dce39ab928b91022ef1438381e3af2bdeb44337cc636337`.

La suite completa incluye 8 casos de `ExportacionLoteTests`, 11 de
`IngresoMicrosoftTests` y los 3 de `ArquitecturaIdentityTests`: todos PASS.
La aprobación intradía preserva el arreglo anterior del parent: el materializador
retorna la trazabilidad técnica anterior/nueva y ServicioPedidos la registra.

## RED causal → GREEN

Los TRX siguientes están en `results/` del mismo directorio aislado.

| Comportamiento                                                   | RED observado                                                                                           | GREEN / cobertura                                                                                      |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Administración vs envío/snapshot y aprobación                    | `coordinacion-red.trx`: 2/2 fallos; envío no esperaba guard, aprobación reutilizaba xmin anterior       | `coordinacion-green.trx`: 2/2 PASS; ambos también PASS en la suite completa                            |
| Aprobación vs administración previamente observada               | `coordinacion-reversa-red.trx`: 1 fallo, no lanzaba conflicto y pisaba valores                          | `coordinacion-selectivas-green.trx`: caso PASS; horas/origen/snapshot/evento de aprobación conservados |
| Grilla mantiene información de fecha/actor sin historia completa | `listado-red.trx`: fallo por 8 resúmenes vacíos, no por preparación                                     | `listado-green.trx`: PASS; 8 filas, hasta 4 eventos y detalle de 201 eventos                           |
| Relectura administrativa por persona                             | `selectivas-red.trx`: 1 fallo por SQL final sin persona_id; prueba adicional de crecimiento ya PASS     | `coordinacion-selectivas-green.trx`: PASS y SQL acotado confirmado                                     |
| Nuevas referencias/persona y actor desactivado tras resolver     | `destinos-red.trx`: 3 fallos; FK cruda en persona inexistente y prohibición posterior a commit en actor | `destinos-green.trx`: 3/3 PASS; conflicto seguro antes de evento/snapshot                              |
| Creación/edición tras prevalidación de actor                     | `pedidos-destinos-red.trx`: 2 fallos; respuesta prohibida posterior a mutación ya confirmada            | `pedidos-destinos-green.trx`: 2/2 PASS; no pedido ni evento parcial                                    |
| Administración de cuenta y circuito del mismo actor              | `orden-locks-red.trx`: `40P01` real por orden invertido                                                 | `orden-locks-green.trx`: PASS; cuenta/persona primero, agregado después, snapshot de 32 horas          |

Los RED corresponden a comportamiento productivo ausente, no a errores de setup.
Las carreras ejecutan servicios/repositorios reales con dos sesiones; un observador
verifica `pg_stat_activity`, `wait_event` advisory/transactionid y `pg_blocking_pids` antes de
liberar la transacción ganadora. No se reemplazó el mecanismo por mocks ni por
un sleep como única prueba de intercalación.

## 3.6 — mecanismo y aceptación

- `Designacion.Version`/`Pedido.Version` conservan el mapping `xmin` existente.
- Identity se protege antes del advisory lock del agregado: usuarios ordenados,
  personas ordenadas y finalmente persona–materia. Esto evita invertir el orden
  de edición administrativa de cuenta/persona al actuar sobre la propia designación.
- Administración toma el guard transaccional de persona antes de validar/reemplazar
  el conjunto. El circuito toma persona y el par persona–materia antes de validar
  vigencia, congelar snapshot y materializar. Persona cubre altas aún inexistentes
  y reemplazos de varias materias sin phantoms entre caminos cooperantes.
- Las lecturas EF anteriores al guard no se reutilizan: el circuito desprende las
  observaciones sin cambios y relee bajo lock; administración compara en lote los
  xmin observados y falla si cambiaron. Una entidad ya mutada fuera del guard falla,
  no se descarta una intención silenciosamente.
- Snapshot al enviar, estado, designaciones e historial se confirman juntos;
  reenviar/aprobar conservan el snapshot inicial. El caso administración→envío
  observa las 32 horas confirmadas, no las 10 anteriores; administración→aprobación
  revalida y aplica las 16 solicitadas; el caso inverso rechaza la administración
  obsoleta y conserva 16 horas y origen del pedido aprobado.
- `DbUpdateConcurrencyException` mantiene el mapper HTTP `409 concurrency-conflict`,
  cubierto por los tests del mapper. La intercalación de servicios demuestra además
  la alternativa permitida de revalidación protegida real; no se agregó un retry ciego.

Tests: `CoordinacionAdministracionTests.cs`, `ConcurrenciaVigenciasTests.cs`,
`VigenciasPedidosTests.cs`, `VigenciasPedidosCasosTests.cs`.

## 3.7 — semántica y cardinalidad real

El repositorio del listado no incluye adjuntos/historia completa. Una consulta
adicional por lote trae, como máximo, primero, creación, primer envío y último
por pedido. Selecciona eventos reales con desempate `(created_at,id)` y excluye
comentarios. Así el cliente conserva actor de creación, fechas, última actualización,
filtros/orden y acciones; detalle/formulario siguen usando GET por ID con toda la
historia y documentación autorizada, no el resumen de la fila.

La prueba de crecimiento registra en el TRX final:

```text
Cardinalidad real: filas=8/8; eventos resumidos=22/22; detalle=2003; consultas resumen=1
Relectura posterior: filas=1
```

El primer dataset agrega envío y edición a la historia inicial; el segundo añade
2.000 ediciones al mismo pedido. No crecen filas ni eventos materializados del
resumen, y el detalle sí conserva los 2.003 eventos. El actor del último evento
se desactiva y conserva su etiqueta histórica en listado/detalle. No hay paginación
visible ni límite de filas; personas/actores se resuelven por IDs visibles en lote.
La respuesta administrativa aplica `persona_id` y `vigente_hasta IS NULL` en SQL.

SQL ejecutado, sin valores de parámetros ni PII:
[designaciones-consultas.sql](./designaciones-consultas.sql).
Los tests capturan comandos con DbCommandInterceptor y sus cardinalidades con
ITestOutputHelper; el SQL puede regenerarse desde los TRX, no exige artefactos ignorados.

Tests: `ListadoPedidosTests.cs`, `LecturasSelectivasDesignacionesTests.cs`.
No se extrapola SLA, aceleración productiva, costo de buffers o plan de ejecución
no medido en estos ensayos: esta evidencia acredita cardinalidad/semántica y SQL
selectivo, no un benchmark de latencia.

## Guard Designaciones relacionado con 1.3

Creación valida persona resuelta y actor dentro de la transacción consumidora;
edición valida actor y persona nueva/cambiada; cada transición valida el actor de
su evento; administración valida la persona antes de confirmar la nueva vigencia.
En transacciones explícitas se usa `ProteccionDestinosIdentity.ValidarAsync` con
la conexión y DbTransaction de Designaciones. En `TransactionScope`, Npgsql no
expone ese DbTransaction local: la ruta administrativa invoca la misma función
`identity.validar_destinos_logicos` mediante la conexión enlistada abierta,
con idéntica traducción específica de error. No abre una conexión de validación
independiente, no agrega FKs ni escribe entidades de Identity.

Los tests de nueva vigencia cubren transacción explícita y ambiente. Los de
creación/edición detienen una lectura real del catálogo después de resolver el
actor, desactivan el usuario en otra sesión y continúan: el guard devuelve
`identity-destination-conflict` y conserva cantidad de pedidos/eventos y horas.
El test de envío detiene la lectura real del pedido posterior a resolver actor,
antes del guard, y conserva borrador/snapshot nulo/eventos previos. Las carreras
administración/circuito verifican separadamente bloqueos reales de advisory y del
usuario (`transactionid`). Los históricos no exigen actividad.

Tests: `DestinosLogicosDesignacionesTests.cs`, `DestinosPedidosTests.cs`.
Esto no cierra 1.3 por sí solo: la aceptación transversal de mantenimiento,
Portal/Tareas y referencias externas sigue siendo responsabilidad del parent.

## 3.8 — documentación y regresiones

Se actualizan dominio, data-model, contrato general/detallado y mapping de BR/tests.
Se elimina la derivación documental obsoleta de carrera desde materia y la afirmación
incorrecta de que el login crea/vincula por documento una cuenta por el Alta.
Las decisiones técnicas no se presentan como normativa nueva; las citas normativas
pendientes siguen pendientes. Snapshot/exportación/identidad canónica y login
sin provisión de usuarios conservan sus tests existentes; no se redujo su cobertura.

## Reproducción

Desde `/workspaces/ars-docendi`, usar rutas absolutas y workdir fijo. No utilizar
bin/obj compartidos ni aplicar migraciones en una base compartida.

```bash
dotnet build /workspaces/ars-docendi/backend/ArsDocendi.slnx -c Release -m:1 --disable-build-servers --artifacts-path /workspaces/ars-docendi/.artifacts/apply-post-auditoria/designaciones-final
dotnet vstest /workspaces/ars-docendi/.artifacts/apply-post-auditoria/designaciones-final/bin/ArsDocendi.IntegrationTests/release/ArsDocendi.IntegrationTests.dll --TestAdapterPath:/workspaces/ars-docendi/.artifacts/apply-post-auditoria/designaciones-final/bin/ArsDocendi.IntegrationTests/release --logger:'trx;LogFileName=reproduccion-completa.trx' --ResultsDirectory:/workspaces/ars-docendi/.artifacts/apply-post-auditoria/designaciones-final/results
pnpm --filter frontend test:run src/features/designaciones src/test/remoteAdapters.test.ts --reporter=json --outputFile=/workspaces/ars-docendi/.artifacts/apply-post-auditoria/designaciones-final/frontend-designaciones.json
openspec validate corregir-integridad-y-rendimiento-post-auditoria --strict
```

## Límites

- No se modificaron SQL/wrappers históricos, snapshots EF, Storage, Portal, Tareas
  ni ReferenciasArchivosDesignaciones. No hubo commits, push, deploy o cambios al checklist.
- El protocolo coordina los escritores de administración/circuito; no ofrece garantía
  de advisory lock frente a un DBA/SQL externo que lo omita. xmin/constraints conservan
  sus garantías específicas; no se promete aislamiento absoluto frente al owner.
- La administración mantiene su DTO actual: compara observaciones del contexto y
  revalida el conjunto dentro del guard, no incorpora un token nuevo entre requests.
- No se ensayó aquí un release real ni recuperación operativa de BD+objetos; esa
  aceptación y los checks globales de 8.x quedan al parent.
- La suite completa integrada verifica una captura del working tree en desarrollo;
  cambios posteriores de otros agentes requieren una nueva compilación/verificación.
