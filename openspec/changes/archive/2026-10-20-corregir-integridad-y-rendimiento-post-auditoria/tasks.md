# Tasks

Este checklist corresponde a implementación futura, no autoriza apply desde propose. Cada bug se trabaja verticalmente RED→GREEN: observar el fallo causal, aplicar el fix mínimo, ejecutar su regresión y documentar evidencia. Usar fixtures sintéticas, Testcontainers y proveedor real donde intervienen bytes; ninguna prueba modifica bases compartidas. No marcar por intención, tests sin discovery, mocks del mecanismo entero o mediciones no ejecutadas.

## 1. Evidencia durable y diagnóstico de referencias

- [x] 1.1 Convertir los probes de `evidence/auditoria.md` en fixtures/pruebas persistentes con SHA anterior y clasificación SQL/API/carrera; verificar que reproduzcan numeración, EXCLUDE, huérfanos, UPN, scope, solución y claves parciales sin leer PII ni exigir archivos ignorados de `.artifacts`.
- [x] 1.2 Implementar RED→GREEN los contratos locales de lectura y el modo propuesto `--diagnosticar-integridad`; verificar base limpia, huérfanos, duplicados normalizados, scopes y jerarquías inválidas, error de permisos y salida segura no exitosa. Comparar tablas/histories/auditoría antes/después para demostrar solo lectura.
- [x] 1.3 Implementar RED→GREEN validación coordinada de destinos lógicos y protección de mantenimiento/borrado físico para usuario/persona, conservando desactivación; verificar dos sesiones destino-vs-referencia, historial de inactivos, referencias Portal/Tareas y ausencia de FKs indiscriminadas o escrituras de módulos a Identity.
- [x] 1.4 Documentar diagnóstico, alcance frente a escritores externos y saneamiento manual aprobado en `database/README.md`, `docs/architecture/data-model.md` y runbook de migraciones; verificar comandos nuevos contra fixtures y tests de arquitectura del DAG/Contracts.

## 2. Storage: objeto inmutable y retención por referencias

- [x] 2.1 Crear la reproducción RED sincronizada de PUT B detenido tras el control inicial, confirmar A y completar B; corregir reserva/intento/clave publicada inmutable con SQL+wrapper incremental y verificar GREEN con PostgreSQL y SeaweedFS reales comparando bytes, MIME, tamaño y hash de descarga.
- [x] 2.2 Implementar RED→GREEN confirmación y eliminación concurrentes, publicación CAS y doble confirmación idempotente; verificar que un resultado tardío de antivirus no resucite disponibilidad ni duplique publicación, y que un error esperado no se traduzca a 500 genérico.
- [x] 2.3 Implementar RED→GREEN recuperación de uploads/reviews interrumpidos, vencimiento y limpieza de temporales; inyectar fallos entre reserva/subida/escaneo/publicación y verificar reintentos seguros sin bloqueo DB durante stream/antivirus ni borrado de intento activo.
- [x] 2.4 Crear contratos puros de consultas de referencias y composición explícita de consumidores; implementar RED→GREEN asociaciones/borrado/limpieza con exclusión transaccional por archivo. Verificar consumidor faltante/fallido, asociación-vs-delete, orden de múltiples IDs y rollback multicontexto real sin MSDTC.
- [x] 2.5 Corregir RED→GREEN reemplazo/eliminación de CV y documentos para desvincular antes de evaluar retención; verificar un mismo archivo utilizado por Portal y pedido, descarga desde pedido tras reemplazo de perfil y 409 al DELETE directo mientras siga referenciado.
- [x] 2.6 Implementar RED→GREEN reserva de indisponibilidad y eliminación remota reintentable cuando no quedan referencias; verificar fallo del proveedor, reejecución y conservación de archivos/bytes ajenos. Actualizar contratos, `docs/architecture/api-contracts.md`, dominio Storage y runbook, y comprobar tests de consumidores/frontend y grafo.

## 3. Designaciones: número, vigencias y completitud

- [x] 3.1 Corregir RED→GREEN numeración mediante nueva migración de función; verificar correlativos 1000/9999/10000/10001, concurrencia, gaps tras rollback y conservación de secuencia/números previos con protección de bytes históricos.
- [x] 3.2 Corregir RED→GREEN Cambio intradía actualizando la vigencia existente; verificar mismo ID/inicio, cargo/dedicación/tres horas/origen nuevos, ausencia de `23P01`, snapshot inmutable y auditoría de valores/orígenes anterior/nuevo con fecha determinística.
- [x] 3.3 Verificar RED→GREEN Cambio sobre vigencia anterior, rechazo de inicio futuro incompatible y reintento idempotente; probar rollback ante fallo de actualización/apertura y conservar EXCLUDE, semántica NULL de vigente y comportamiento vigente de Baja intradía.
- [x] 3.4 Implementar RED→GREEN completitud de Alta/Cambio antes de envío, reenvío y aprobación, y CHECK incremental cuando corresponda; verificar cargo/horas NULL/cero, legado en revisión, Baja válida, documentación/legajo/BR actuales y errores por campo con feedback de frontend sin eventos parciales.
- [x] 3.5 Añadir RED→GREEN FK simple de materia con preflight de huérfanos; verificar inserción/update inválidos, borrado de materia usada solo por designaciones y upgrade limpio, sin FK informativa materia–plan/carrera ni auto-reasignación de datos.
- [x] 3.6 Añadir RED→GREEN control `xmin`/coordinación persona–materia compartido por administración y circuito; reproducir dos sesiones administración-vs-aprobación/snapshot, comprobar conflicto 409 o revalidación protegida y ausencia de pérdida de horas/origen/eventos.
- [x] 3.7 Separar RED→GREEN consultas de listado/detalle y lectura posterior por persona; verificar todas las filas visibles, acciones/permisos y detalle completo, capturar cardinalidad con historia creciente sin transportar historias completas en la grilla.
- [x] 3.8 Actualizar dominio, data-model, api-contracts y mapping de BR/tests de Designaciones; verificar referencias de documentación y regresiones de snapshot/exportación/identidad canónica, sin inventar normativa ni restaurar creación de cuentas por login.

## 4. Identity: normalización, scopes y lectura selectiva

- [x] 4.1 Implementar RED→GREEN UNIQUE de UPN normalizado y consulta de ingreso con la misma expresión; verificar case/espacios por API y SQL, bloqueo de duplicados legados sin fusión y upgrade que conserva ID/persona/roles/OID/TID.
- [x] 4.2 Implementar RED→GREEN guard de ámbito en servicio/BD y bloqueo compartido con asignaciones; verificar cambio con membresías vivas rechazado, solo revocadas permitido, carrera grant-vs-scope, revocación segura de legado incoherente y roles de sistema inmutables.
- [x] 4.3 Implementar RED→GREEN UNIQUE de nombre normalizado de rol activo y traducción específica de 23505; verificar dos renombres simultáneos de roles diferentes, nombre propio, desactivados, código estable y respuesta 409 consumible por formulario.
- [x] 4.4 Extender RED→GREEN `IConsultasIdentity` para cuenta, IDs/lotes y candidatos filtrados antes de materializar/límite; verificar selección multi-palabra/case/tildes/jerarquía, usuarios históricos inactivos y conteo de consultas sin directorio global o N+1.
- [x] 4.5 Actualizar docs de Identity, contratos/consumidores y formulario de rol con conflictos; verificar tests de ingreso/membresías/frontend y coherencia con los deltas que eliminan escenarios obsoletos de renombre de sistema y carrera obligatoria para rol genérico de materia.

## 5. Tareas: jerarquía coherente y guard de estado protegido

- [x] 5.1 Implementar RED→GREEN cambio de proyecto de raíz propagado a todos los descendientes y validación diferible; verificar profundidad mayor a dos, P→Q, P→NULL, NULL heredado válido, preservación de responsables/estado/avance y rollback del conjunto.
- [x] 5.2 Implementar RED→GREEN validación de proyecto efectivo de hijas coordinada con cambio de estado; verificar proyecto cerrado, cierre-vs-creación con dos sesiones, banderas de catálogo y edición permitida al conservar proyecto actual cerrado.
- [x] 5.3 Añadir RED→GREEN versión de Tarea/Proyecto y guards transaccionales; reproducir cancelación-vs-En curso, reasignación-vs-mutación y dos ediciones, verificar versión ausente/obsoleta y 409 sin historial parcial. Actualizar DTOs/adapters/cache y UX de conflicto sin retry ciego.
- [x] 5.4 Resolver RED→GREEN relaciones canónicas concurrentes con autorización y resultado idempotente; verificar dos solicitudes del mismo par, remove ausente y ocultación 404 fuera de ámbito, sin 500 por PK ni duplicación.
- [x] 5.5 Añadir RED→GREEN garantías SQL de ciclos, proyecto heredado y Resuelta con solución usando catálogo numérico; verificar SQL directo, dos modificaciones de jerarquía concurrentes, IDs no fijados y cambios de catálogo que invalidarían filas. No incorporar API de reparenting ni obligatoriedad de proyecto en raíces.
- [x] 5.6 Acotar RED→GREEN directorio/relaciones a IDs visibles y búsqueda previa al límite; verificar datasets con visibles constantes y datos ajenos crecientes, contenido/orden/permisos iguales y ausencia de cargas globales.
- [x] 5.7 Actualizar docs de Tareas/Proyectos, data-model, api-contracts y tests HTTP/frontend/BR aplicables; verificar catálogos, numeración, pings y tablas de negocio enganchadas a auditoría sin repetir texto sensible en nuevos snapshots.

## 6. Portal y rendimiento transversal

- [x] 6.1 Reproducir RED la consulta EF del perfil 3×3×3×2×4 y corregir su expansión; verificar GREEN con secciones completas, cardinalidad acotada y consistencia elegida, guardar SQL real/planes/round trips sin omitir datos para aparentar mejora.
- [x] 6.2 Crear índices incrementales por `perfil_id` de experiencias, educaciones, certificaciones y proyectos; verificar catálogo real sin equivalentes duplicados y comparar planes/buffers sobre 2.000 perfiles/20.000 experiencias, preservando filas y midiendo costo de writes.
- [x] 6.3 Implementar RED→GREEN versión por sección/mutación Portal y obtención de ítem acotada; verificar dos contactos concurrentes, edición de experiencia y reemplazo documental obsoleto sin pérdida de datos ni objetos. Actualizar DTOs/adapters/frontend para 409.
- [x] 6.4 Resolver RED→GREEN perfil incidental y término normalizado concurrentes con UPSERT/relectura; verificar dos primeras secciones diferentes, mismas secciones incompatibles, dos perfiles sugiriendo el término y habilidades/intereses independientes sin 500 por UNIQUE.
- [x] 6.5 Acotar RED→GREEN identidad del actor y metadata de archivos por lote en Portal; verificar número de consultas con documentos crecientes, otras secciones preservadas, autorización y datos institucionales de solo lectura.
- [x] 6.6 Medir candidatos de índices de pedidos/Tareas y FKs selectivas con datasets/documentación de filtros y orden; registrar antes/después, repeticiones, tamaño/costo de escritura y cobertura parcial/GiST. Crear solo incrementales justificados o entregar descarte motivado de cada candidato, manteniendo grillas sin paginación visible.
- [x] 6.7 Actualizar dominio Portal, API/data-model y reporte reproducible de rendimiento; verificar comandos de fixtures/planes y tests funcionales/frontend, sin SLA o factor de aceleración extrapolado de una ejecución.

## 7. Auditoría precisa y minimización

- [x] 7.1 Implementar RED→GREEN metadata de clave completa/formato y enganches incrementales para PK compuestas; verificar dos permisos del mismo rol y habilidad/interés del mismo perfil, filtros precisos y convivencia con claves simples/legadas sin rewrite de eventos.
- [x] 7.2 Corregir RED→GREEN `row_history` para seleccionar actor/fecha del mismo evento con desempate por ID; verificar múltiples cambios en una transacción, creación/modificación/eliminación y grupos legacy marcados, sin atribuciones inventadas.
- [x] 7.3 Implementar RED→GREEN allowlist de snapshots nuevos en captura SQL; verificar PII/secretos/valores no clasificados ausentes, columnas modificadas presentes y cargo/horas/orígenes intradía conservados. Mantener redacción fail-closed de API para eventos antiguos.
- [x] 7.4 Documentar clasificación/acceso de datos históricos y respaldos, limitación del runtime owner y procedimiento de tratamiento manual sujeto a autorización; verificar tests que upgrade no purga/rewrite de eventos ni pierde historial de trámites y ejemplos documentales sin PII real.
- [x] 7.5 Medir página y COUNT de auditoría separadamente, índices temporales/filtros contra BRIN y cobertura vigente; verificar orden estable, límites/timeouts/permisos y decisión justificada de crear o descartar candidatos. Actualizar runbook con evidencia real y costo de escritura.

## 8. Integración, upgrade y cierre verificable

- [x] 8.1 Ensayar instalación nueva y upgrade real desde `f10e0a5` con datos/objetos sintéticos, cuentas Microsoft vinculadas y auditoría legacy; verificar CLI estado/preview sin writes, runner/recursos compilados, FKs/triggers/índices reales, conservación de IDs/snapshots/bytes y no-op sin nueva auditoría.
- [x] 8.2 Ensayar bases incompatibles con duplicados/huérfanos/scopes/ciclos y fallos de proveedor/migración; verificar aborto antes de publicación, ausencia de saneamiento/reset automático, preservación de backups y posibilidad de recuperación aislada con versión compatible.
- [x] 8.3 Ejecutar validación de tooling e historia confiable: `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs` con base/corte confiables según README, build Release y `--validar-recursos`; verificar logs reales y bytes de SQL/wrappers protegidos intactos, sin rehash del manifiesto como bypass.
- [x] 8.4 Ejecutar suite backend con discovery efectivo y TRX, tests/lint/build frontend, formato del diff y OpenSpec estricto del change; verificar totales reales, pruebas nuevas de concurrencia/integridad/rendimiento y DAG. Registrar por separado cualquier fallo preexistente de validación global fuera de este alcance, sin declararlo corregido.
- [x] 8.5 Consolidar matriz de cobertura A01–A14/P01–P04 con test/resultado por fila y plan de release/rollback BD+objetos ya ensayado; verificar que cada escenario del delta tenga test o evidencia, ninguna tarea automatable quede sin ejecutar y las limitaciones legadas estén explícitas.

## Workflow follow-up

- Presentar este propose para revisión; iniciar apply únicamente ante una solicitud nueva del usuario.
- La revisión, sincronización/archivo del change y cualquier commit/push/despliegue se ejecutan solo dentro del alcance autorizado posteriormente, sin incluirlos como pruebas de corrección no realizadas.
- La activación en ambientes compartidos, el saneamiento de datos reales o el tratamiento de PII histórica requieren autorización operativa específica; no forman parte de los ensayos automáticos del checklist.
