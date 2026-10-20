# Regresiones de integración y referencias — continuación del apply

## Alcance y resultados reales

Estos checks son regresiones focalizadas de un candidato intermedio. No sustituyen el gate backend consolidado ni el ensayo final de instalación/upgrade. Ninguno autoriza publicación, saneamiento o cambios de datos compartidos.

- `integracion-correcciones.trx`: **24 ejecutadas, 22 aprobadas, 2 fallidas, 0 omitidas**. Las dos fallidas fueron reutilizaciones del contexto Portal tras desenlistar: la cadena recuperada de una conexión Npgsql abierta ya no incluía contraseña. No se trata de una contraseña ausente en la configuración inicial ni se relajó la autenticación.
- `revision-2.trx`: **53 ejecutadas, 51 aprobadas, 2 fallidas, 0 omitidas**, con los mismos dos fallos de restauración. Esta ejecución también verificó las correcciones de relaciones concurrentes, selección acotada y trazabilidad intradía.
- `conexion-restaurada.trx`: **3 ejecutadas, 3 aprobadas, 0 fallidas/omitidas**. Reemplazo de CV, archivo compartido con pedido y rollback multicontexto Designaciones. Los participantes preservan un clon de la conexión antes del enlistamiento y restauran una conexión propia al terminar; no reconstruyen desde una cadena que pueda omitir credenciales.
- `destinos-red.trx`: **1 ejecutada, 1 fallida, 0 omitidas**, causal en `Alta_http_protege_responsable_hasta_commit_y_conserva_historia_inactiva`. Sin el guard consumidor, la desactivación termina mientras el INSERT HTTP sigue detenido en una barrera PostgreSQL.
- `destinos-green.trx`: **19 ejecutadas, 19 aprobadas, 0 fallidas/omitidas**: suite HTTP Tareas y destinos lógicos Identity. Con el guard, la segunda sesión espera realmente un lock antes de liberar el INSERT; después del commit la desactivación conserva identidad/nombre e historial al leer el detalle.
- `backend-regresion-intermedia.trx`: **377 ejecutadas, 375 aprobadas, 2 fallidas, 0 omitidas**, duración informada 8 min 9 s. Únicos fallos: los dos casos de `Administracion_y_circuito_revalidan_vigencia_bajo_exclusion_compartida`. Snapshot no esperó la protección administrativa; aprobación recibió `DbUpdateConcurrencyException` al actualizar una vigencia ya modificada. Se remitieron ambos al responsable de Designaciones. La DLL fue compilada durante su desarrollo y no certifica el estado de su implementación posterior; la tarea 3.6 y el gate consolidado permanecen abiertos.

## Implementación comprobada

- Tareas resuelve nombres mediante IDs visibles/históricos y consulta relaciones que tocan esos IDs. Los candidatos filtran roles admitidos y roles de jerarquía superior antes del límite de la consulta Identity; no cargan un directorio global.
- Relaciones canónicas usan exclusión transaccional, raíces e IDs ordenados, `INSERT ... ON CONFLICT DO NOTHING` y borrado idempotente. Ambos extremos se autorizan bajo la protección.
- Nuevas referencias de responsable/creador en altas y reasignaciones de Tareas se protegen como lote ordenado dentro de la transacción local. Proyectos protege el responsable nuevo y comentarios protege el autor nuevo. No se agregaron FKs intermodulares indiscriminadas ni escrituras de negocio en Identity.
- Cambio intradía registra datos técnicos anteriores/nuevos y origen en el historial del pedido, conservando el snapshot y confirmando materialización e historial en la misma transacción.

## Evidencia reproducible

TRX y logs se guardaron en `.artifacts/apply-post-auditoria/` (artefactos locales ignorados). Los tests y esta síntesis permanecen en archivos versionables. Se ejecutó `dotnet build backend/ArsDocendi.slnx -c Release --disable-build-servers -m:1 --artifacts-path <salida aislada dentro del repo>` y `dotnet vstest <DLL release> --TestAdapterPath:<salida> --TestCaseFilter:<clase/caso> --logger:trx --ResultsDirectory:<evidencia>`.

Salidas utilizadas: `correcciones`, `revision-2`, `revision-3`, `destinos-red` y `destinos-green`. El build registró advertencias xUnit1051 existentes en NumeracionPedidosTests; no son una ejecución fallida ni una prueba omitida.

## Entregas delegadas verificadas por el coordinador

La regresión posterior de Designaciones/administración/ingreso terminó con **138/138 aprobadas, 0 omitidas**, comprobadas en `.artifacts/apply-post-auditoria/designaciones-final/results/regresion-intermedia.trx`. Incluye los dos casos antes fallidos de coordinación (snapshot y aprobación), ahora `Passed`. Es una ejecución focal posterior: no transforma retrospectivamente el resultado 375/377 ni sustituye repetir la suite backend completa sobre el candidato consolidado.

Se leyeron los contadores TRX reales, no solo los resúmenes de los agentes: Identity **75/75** en `.artifacts/identity/identity-regresiones-green.trx`; upgrade adicional de nombres **2/2** en `.artifacts/identity/identity-upgrade-nombres-green.trx`; Auditoría focal **23/23** en `TestResults/auditoria-completa.trx`; Storage aislado **33/33** en `TestResults/storage-suite-final-aislada.trx`. Son ejecuciones distintas, no un total de una sola suite. Se inspeccionaron también los RED causales de UPN, scope y nombre de rol.

Con esa evidencia se marcaron **4.1, 4.2, 4.3, 7.1, 7.2 y 7.3**; `openspec instructions apply` confirmó **10/47 completas y 37 pendientes**. Las tareas de adopción consumidora/documentación global, Storage completo y gates finales no se cierran por esos resultados focales. La interrupción por cuota de Designaciones/Tareas/Portal no convierte su código persistido en una entrega certificada.

## Referencias lógicas Identity — tarea 1.3

Se recompiló `ArsDocendi.IntegrationTests` Release desde el árbol actual (exit 0; 10 advertencias de analizadores xUnit en `NumeracionPedidosTests.cs` y `ConcurrenciaSeccionesTests.cs`, sin errores) y se ejecutó VSTest con adapter explícito. El TRX `.artifacts/apply-post-auditoria/destinos-logicos-task-1-3/destinos-logicos-task-1-3.trx` fue leído por XML: **15 ejecutadas, 15 aprobadas, 0 fallidas, 0 omitidas**.

La cobertura cruzada comprobada incluye:

- Dos sesiones PostgreSQL en altas HTTP de **tarea y proyecto** contra desactivación del responsable: la desactivación espera el lock real mientras la inserción está detenida; la referencia confirma primero y luego la identidad inactiva sigue resolviéndose en el listado/historial.
- `DestinosLogicosIdentityTests`: orden de carrera desactivación-vs-referencia, persona-vs-mantenimiento, rollback del consumidor, rechazo de destino inactivo y DELETE físico fail-closed.
- Portal crea un perfil usando una persona Identity existente; el DELETE físico directo falla con `identity_destino_borrado_protegido` y las filas de persona y perfil permanecen.
- Consulta del catálogo real de `pg_constraint`: cero FKs desde schemas `portal`/`tareas` hacia `identity`; pruebas de arquitectura verifican que los módulos no escriben entidades protegidas de Identity ni acceden internals ajenos.
- Designaciones conserva regresiones de persona inexistente y actor desactivado, sin confirmar vigencia, snapshot ni evento parciales.

Las pruebas usan PostgreSQL desechable/Testcontainers; después de la ejecución no quedaron contenedores, volúmenes ni redes etiquetados por Testcontainers. No se consultaron ni modificaron bases compartidas.

## Gates pendientes

La delegación original de Tareas/Proyectos (`deleg_02590798`) terminó por HTTP 429 del proveedor. Ese fallo de cuota no constituye un fallo de tests ni una entrega completa. El coordinador retomó el código persistido, corrigió relaciones concurrentes/lecturas selectivas y ejecutó las regresiones consignadas arriba; la sección 5 permanece pendiente de reconciliar contra todos sus criterios y el gate consolidado.

La coordinación completa administración–aprobación/snapshot y la separación de consultas Designaciones ya tienen evidencia del responsable y lectura directa de los TRX: `docs/evidence/designaciones-3.6-3.8.md` registra RED→GREEN causal para guard compartido, revalidación/xmin y grilla/detalle; el TRX consolidado `designaciones-final/results/backend-orden-locks-final.trx` contiene 386/386 PASS (cero fallos/omitidas), junto con pruebas focales de locks 37/37, coordinación 2/2 y lecturas selectivas 5/5. Con esa evidencia se cerraron 3.6–3.8; la tarea transversal 1.3 quedó verificada separadamente en la sección anterior. Esto no implica cierre del release. Portal/Tareas, Storage global y gates finales continúan pendientes hasta reconciliar cada criterio y resultado propio.
