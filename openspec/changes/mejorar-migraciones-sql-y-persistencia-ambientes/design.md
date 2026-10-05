# Design

## Context

Ver `proposal.md` para motivación y alcance. La revisión parte de `d267144`, que incorpora `develop` a `feature/seed-data`. El árbol estaba limpio antes de crear este cambio.

El Host registra Identity, Storage, Designaciones, Aulas, Portal y Tareas y aplica cada migrador secuencialmente. El SQL se embebe en assemblies. Identity tiene su tabla de historial en su schema; otros contextos tienen configuraciones distintas que se deben inventariar antes del squash.

`spin-up.sh` todavía ejecuta `down -v`, `drop-db.sh`, `purge-storage.sh` y seed sobre staging/PR. El workflow de preview genera una credencial SeaweedFS por ejecución y permite cancelación en curso. Los deploys prod/staging ya separan build hosted de despliegue en principal/secundaria. `backup-storage.sh` ya respalda PostgreSQL y objetos, calcula checksums y transmite bytes por stdio; `restore-storage.sh` sólo admite ambientes descartables y exige bucket vacío.

La spec de plataforma ya eliminó reaper y separó PostgreSQL/SeaweedFS por host. No se modifican esas decisiones. `rediseno-modelo-academico` sigue activo: su modelo final está en el código, pero no todas sus delta specs se sincronizaron.

## Goals / Non-Goals

**Goals:**

- Mejorar el mecanismo existente con contratos observables, cambios pequeños y sin nuevas dependencias de gestión de schema.
- Consolidar una instalación final legible manteniendo SQL como única fuente del DDL y EF como historial de aplicación.
- Preservar datos y objetos entre despliegues de cada ambiente mientras éste exista.
- Tener preview, recovery y fallas cerradas compatibles con runners efímeros y Docker sin paths compartidos garantizados.

**Non-Goals:**

- Diff declarativo, generación automática de ALTER desde CREATE, repeatables arbitrarios o reemplazo de EF.
- Upgrade de instancias del historial alpha anterior, reseteos de servidores o acciones sobre bases reales durante la propuesta.
- Separación de usuarios/privilegios, cambios de API HTTP o ampliación general de pruebas de upgrades entre releases.
- Reaper, replicación, failover, cambio de hostname/scopes o rediseño de adjuntos/antivirus.
- Rehearsal de backups en cada deploy; la restauración se prueba de forma aislada como verificación del procedimiento.

## Decisions

### D1. Baseline final por contexto y SQL pequeño por responsabilidad

Crear un baseline EF para Identity/Audit, otro para Storage, otro para Designaciones y otro para Portal. Dividir su SQL por responsabilidad cuando sea necesario para mantener archivos legibles; no comprimirlo en un monolito ni concatenar operaciones obsoletas. Aulas/Tareas no reciben una migración vacía sólo por simetría.

Orden explícito: Identity/Audit (users, infraestructura audit, resto identity, attach diferido), Storage, Designaciones y Portal; Aulas/Tareas mantienen su integración. Las referencias a `storage.archivos` y a identidad deben existir antes de sus consumidores. La consolidación conserva funciones, triggers, secuencias, constraints, índices, FKs, permisos y catálogos del estado actual, incluido materia/carrera directos y planes informativos. No conserva backfills que sólo tenían sentido para datos alpha antiguos.

Comparar la instalación antigua y la nueva en bases desechables durante la implementación, excluyendo historia EF y nombres técnicos deliberadamente cambiados. Es una prueba del squash, no una matriz permanente de upgrades históricos. Documentar cambios deliberados; no eliminar constraints o campos por considerarlos legacy sin revisar sus consumidores.

Rechazar en preflight identificadores aplicados desconocidos, historial discontinuo y schemas gestionados preexistentes sin historial reconocido. No deducir que una base es vacía sólo porque no existe una tabla EF. Conservar la distribución de tablas de historial salvo necesidad verificada, y dar identidad de contexto a cada operación para evitar confundir histories compartidos.

Alternativa descartada: marcar el baseline aplicado sobre una base anterior. Oculta divergencias y contradice el corte para instancias nuevas.

### D2. Una asociación explícita entre migración y recursos

Cada migración declara una lista ordenada de rutas SQL en metadata reutilizable por su Up, el inventario y los checks. No duplicar el DDL ni mantener listas distintas para ejecución y preview. `RecursosSql` sigue leyendo assemblies en runtime.

El scaffolder recibe contexto y nombre, valida tokens/colisiones y genera SQL + wrapper con identidad EF reconocida y asociación de recursos. En dry-run no escribe; no conecta a bases ni aplica migraciones. Su documentación muestra el flujo completo y advierte que las ediciones posteriores requieren migración nueva.

CI comprueba recursos presentes y embebidos, rutas/IDs únicos y SQL sin consumidor. Las excepciones documentadas (por ejemplo soporte de bootstrap) deben ser explícitas, no globs permisivos. Comparar archivos protegidos con una revisión Git confiable y fija; registrar en este corte el inventario consolidado como nuevo punto de partida. Sólo se retira el historial alpha como parte de este cambio revisado; no queda un flag genérico para saltar la protección.

La protección de CI impide reescribir historia versionada, pero no es un detector completo de drift manual en la base. No afirmar esa garantía.

### D3. CLI de consulta y preview, sin nuevas dependencias entre módulos

Proponer `--estado-migraciones` para salida JSON estable y `--script-migraciones <directorio>` para archivos SQL por contexto más un manifiesto ordenado. Son interfaces nuevas; no comandos ya disponibles. Extender el contrato transversal con DTOs puros; cada implementación mantiene acceso exclusivo a su DbContext. El Host sólo coordina.

Estado incluye contexto, IDs disponibles/aplicados/pendientes, identidad segura del destino y compatibilidad. No imprimir connection strings, usuario, password o secrets de objetos. Errores incompatibles devuelven exit no cero y no escriben.

Usar el generador EF del intervalo aplicado→último de cada contexto; no simular una ejecución ni concatenar todos los recursos. Conservar los archivos/manifiesto separados y su orden global; no unir scripts idempotentes con tablas de historial distintas sin validación. El preview va ligado a SHA y estado observado y no es garantía de ausencia de cambios manuales.

Generar estado y preview dentro del lock de despliegue. La aplicación efectiva sigue usando `--migrate` y verifica de nuevo compatibilidad/pendientes; no ejecutar un preview viejo si el estado cambió. La consulta no crea histories/schemas ni abre listener; contra una base vacía muestra pendientes. El proceso falla ante modos incompatibles o ruta de salida insegura.

### D4. Persistencia también de SeaweedFS y seed único

En deploy ordinario retirar `down -v`, drop y purge. `provision-db.sh`/`provision-storage.sh` siguen siendo idempotentes. Conservar bucket y objetos aunque se actualice la política o rote una credencial de PR. Evitar invalidar prematuramente al backend antiguo: si la credencial cambia, hacer la rotación durante mantenimiento y actualizar el backend conjuntamente.

Inicializar seed únicamente cuando la base de ese ambiente fue creada por el proceso o existe una inicialización fallida registrada de manera verificable. Insertar marcador de éxito en la misma transacción del dataset; una marca incompleta o una base poblada sin marca no autoriza sobrescritura. Un cambio de versión del dataset no dispara reseed automático.

No basta con comprobar que exista `public.seed_metadata`: registrar dataset e inicialización completada. Reusar la transacción/advisory lock existentes del seed y transmitir SQL por stdin, sin confiar en bind mounts del runner. El setup local conserva protección first-run y exclusividad de datos SGA en dev; no leer ni versionar ese archivo privado.

Teardown explícito/al cierre conserva destrucción del preview y su aislamiento. Un reset manual autorizado sigue separado del deploy y no admite prod. No crear reaper.

### D5. Despliegue incremental y sección crítica completa

Secuencia para todos los ambientes:

1. Validar destino/inputs, obtener imágenes inmutables y adquirir lock por ambiente.
2. Aprovisionar sólo recursos faltantes, consultar estado compatible y generar preview.
3. Si hay pendientes, detener el backend del ambiente y cualquier escritor conocido. Para una base existente crear backup completo verificable antes del primer cambio. Para una base nueva registrar que no había estado previo, sin fabricar backup.
4. Aplicar migraciones en orden. No asumir una transacción que abarque todos los contextos.
5. Inicializar seed no-prod si corresponde; nunca seed en prod.
6. Actualizar servicios y verificar SHA, pings y una comprobación de conectividad DB sólo lectura; ausencia de pendientes después del deploy.
7. Registrar éxito ligado a SHA/historial/backup y ejecutar retención de backups exitosos.

Sin pendientes se omiten parada por migración y backup pre-migración; igualmente se verifica la aplicación nueva. Si falla backup o migración, no publicar la nueva versión. Si un smoke falla, detener el backend nuevo, conservar backup/diagnóstico y no reiniciar ciegamente la versión vieja sobre un schema posiblemente actualizado. No publicar comentario de éxito ni recibo exitoso.

Usar `cancel-in-progress: false` para la fase crítica de PR. Compartir exclusión con teardown/manuales en el host destino; comprobar que el PR siga abierto antes de desplegar. No afirmar FIFO de GitHub ni que todas las ejecuciones encoladas se conservarán: el objetivo es no interrumpir ni intercalar operaciones. Mantener principal/secundaria, gates, SHA pinning y permisos mínimos existentes.

### D6. Backup conjunto existente, destino persistente y recuperación explícita

Reusar el formato PostgreSQL+objetos/checksums de `backup-storage.sh`, endureciendo publicación completa/atómica e integridad antes de aceptar éxito. Parar escritores durante el snapshot conjunto evita inconsistencias entre metadata y bytes; el backup no garantiza recuperar escrituras posteriores.

Persistir en volumen Docker dedicado por ambiente en el host que ejecuta la base, no en workspace efímero, imagen, repo o artefacto de PR. Transmitir contenido por stdio y adaptar las interfaces actuales para el volumen; no asumir que paths SSH/runner existen en el daemon. Un fallo de snapshot deja un registro incompleto, nunca un backup marcado recuperable.

Variables nuevas no secretas: `BACKUP_VOLUME_PREFIX` (default `arsdocendi-backups`) y `BACKUP_RETENTION_DAYS` (default 7, entero positivo). Definirlas como vars en `prod`, `staging` y `pr-preview` si se requiere override. Conservar backups de despliegues fallidos hasta eliminación explícita; la retención sólo elimina backups completos de despliegues exitosos vencidos del mismo ambiente. Un teardown no elimina implícitamente los backups ni duplica datos en otro host.

Conservar credenciales actuales y su scope; no renombrar `SEAWEEDFS_ROOT_*` a `MINIO_ROOT_*`. La protección de prod continúa en principal, separado del runner efímero secundario. Respaldos no se exponen públicamente; sólo usuarios/servicios administrativos autorizados acceden al volumen. No introducir secretos nuevos ni inventario local de aplicación.

Mantener `restore-storage.sh` sin restore directo de prod. Documentar recuperación productiva controlada: validar backup, restaurar en base/bucket nuevos y aislados dentro de principal, verificar metadata/bytes y versión compatible, y sólo con aprobación operar el corte de destino. No utilizar Proxmox como destino de datos productivos ni sobreescribir una base que aceptó nuevas escrituras. La prueba automatizada usa datos sintéticos y destinos desechables; no requiere restaurar producción.

### D7. Mantener alcance de pruebas proporcional

Adaptar fixtures y consumidores de IDs alpha eliminados. Retirar sólo ensayos de transiciones que dejan de existir, preservando verificaciones vigentes de constraints, permisos, auditoría, adjuntos y reglas de dominio. Añadir pruebas de los nuevos contratos/errores y de conservación en redeploy. No ampliar una matriz de upgrade desde releases históricas ni separar privilegios. Estas exclusiones no justifican dejar sin tests el código nuevo.

## Risks / Trade-offs

- [Instancia antigua no declarada] → preflight rechaza sin writes; no reset ni baseline stamp automáticos.
- [Squash omite una función/constraint/reference data] → comparación del estado final en bases aisladas y suite existente antes del corte.
- [Seed pisa cambios] → inicialización autorizada y marcador transaccional, no reseed por versión.
- [DB y objetos divergen] → parar escritores y reutilizar backup conjunto, no dump PostgreSQL aislado como recuperación completa de adjuntos.
- [Falla después de confirmar un contexto] → conservar backup y mantenimiento; recuperación explícita, no rollback global prometido.
- [Rebase de PR elimina una migración ya aplicada] → rechazar estado incompatible; agregar migración nueva o reset manual autorizado, nunca reset en deploy.
- [Preview obsoleto] → capturarlo bajo lock y revalidar estado antes de aplicar.
- [Backups de fallo crecen] → inventario visible y limpieza manual documentada, no borrado automático de la única recuperación.
- [Credencial PR rota antes de mantenimiento] → coordinar provisionamiento/rotación con backend, comprobar descarga de adjuntos tras redeploy.
- [Specs activas del modelo académico] → incorporar su resultado actual sin archivar/sincronizar el otro cambio dentro de éste.

## Migration Plan

1. Inventariar el estado post-develop, histories y dependencias; capturar equivalencia antigua→baseline en destinos de prueba.
2. Implementar baseline, scaffolding/integridad y CLI, con docs/tests de cada fase.
3. Integrar backup, seed único, conservación DB/objetos y sección crítica sin alterar topología/scopes.
4. Validar instalación nueva, no-op, preview, redeploy con datos/adjuntos, fallas y restore aislado. Probar imagen real en PostgreSQL 18 y controles de workflows.
5. Desplegar el baseline únicamente a instancias nuevas confirmadas. Si hay un historial anterior, abortar y pedir decisión específica; este cambio no lo convierte.
6. Registrar el inventario baseline como inmutable y habilitar migraciones incrementales futuras.

Rollback antes de datos persistentes: volver al artefacto anterior y reconstruir sólo destinos vacíos/autorizados. Después de datos persistentes: recuperar estado consistente desde backup en destino aislado y seleccionar release compatible con aprobación; nunca ejecutar Down destructivos como recuperación automática.
