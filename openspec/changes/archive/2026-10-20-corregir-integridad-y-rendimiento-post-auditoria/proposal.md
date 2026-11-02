# Proposal

## Why

La auditoría del esquema construido por `feature/seed-data` en `f10e0a5` encontró catorce hallazgos de integridad/funcionamiento (A01–A14) y cuatro áreas de rendimiento (P01–P04), incluyendo colisiones reales de numeración, solapamiento intradía, consultas cartesianas y riesgos de publicación/borrado de archivos. Este cambio cierra esas brechas con garantías comprobables y upgrades incrementales, sin interpretar la instalación exitosa ni los tests existentes como prueba de integridad completa.

## What Changes

- Vincular cada confirmación/antivirus con una versión de objeto inmutable y resolver cargas, confirmaciones, eliminaciones y limpieza concurrentes. Un PUT tardío no podrá reemplazar bytes publicados.
- Coordinar las referencias lógicas a archivos con las mutaciones de Designaciones/Portal: eliminar o reemplazar una asociación no borrará bytes todavía referenciados por otro consumidor.
- Corregir numeración sin truncar ni resetear secuencias; conservar números históricos y gaps legítimos.
- Aplicar la decisión del usuario para cambios intradía: actualizar la misma vigencia iniciada ese día, conservando ID, fecha inicial, snapshot y trazabilidad de los valores y orígenes anteriores/nuevos. Para vigencias previas, mantener cierre/apertura atómicos y EXCLUDE.
- Exigir cargo y horas positivas para Alta/Cambio antes de enviar, reenviar o aprobar, sin inventar valores para pedidos existentes ni ampliar la admisión actual de borradores.
- Propagar atómicamente cambios de proyecto de una raíz a todos sus descendientes, validar proyectos elegibles también al crear hijas y reforzar ausencia de ciclos, igualdad de proyecto —incluido NULL— y solución obligatoria de Resuelta.
- Añadir la FK simple de `designaciones.materia_id` a `identity.materias`, UNIQUE de UPN normalizado y nombres normalizados de roles activos; impedir cambios de ámbito con asignaciones vivas y resolver conflictos concurrentes sin errores 500 genéricos.
- Conservar, por decisión del usuario, referencias intermodulares deliberadamente lógicas: validación coordinada, protección de borrado físico y diagnóstico explícito de huérfanos; no agregar FKs indiscriminadas ni una FK materia–plan/carrera informativa.
- Incorporar control concurrente de agregados y tests de intercalación reales. **BREAKING:** las mutaciones de entidades existentes de Tareas/Proyectos y secciones Portal afectadas exigirán la versión observada; los clientes deberán actualizar DTOs/adapters y tratar 409 sin reintentar ciegamente. Las creaciones y operaciones verdaderamente idempotentes no exigirán una versión de una fila inexistente.
- Mejorar identidad de auditoría para PK compuestas y orden estable, minimizar valores personales en eventos nuevos y definir tratamiento restringido de eventos legados sin purgarlos ni alterar el historial automáticamente.
- Eliminar expansión cartesiana del perfil y lecturas globales/N+1 innecesarias; agregar índices por perfil y medir índices candidatos con SQL real, cardinalidad, buffers y costo de escritura. Mantener grillas completas sin paginación visible y la búsqueda acotada de candidatos.
- Convertir hallazgos en tests persistentes y evidencia reproducible. Actualizar únicamente contradicciones de specs directamente relacionadas con estos controles; no restaurar modelos académicos, autenticación o roles obsoletos.

## Capabilities

### New Capabilities

- `integridad-referencias-logicas`: validación y protección coordinada de referencias intermodulares sin FK, diagnóstico de solo lectura y rechazo seguro ante datos incoherentes.

### Modified Capabilities

- `almacenamiento-adjuntos`: bytes confirmados inmutables, retención según referencias y coordinación concurrente de su ciclo de vida.
- `persistencia-designaciones`: numeración no truncante, FK simple de materia, actualización intradía trazable y control concurrente de vigencias; reconciliar la referencia académica obsoleta con materia/carrera canónicas actuales.
- `pedidos-designacion`: completitud previa al circuito y lecturas de listado livianas, preservando autorización, snapshots y listas completas.
- `tareas-api`: jerarquía/proyecto coherentes, elegibilidad de hijas, transiciones y mutaciones versionadas, relaciones concurrentes idempotentes y consultas acotadas.
- `portal-docente-api`: lecturas sin multiplicación cartesiana, mutaciones independientes/versionadas y creación concurrente de perfiles/términos.
- `persistencia-identity`: unicidad normalizada, coherencia permanente de scopes y lecturas selectivas; reconciliar escenarios incompatibles con roles de sistema inmutables y scopes actuales.
- `editar-rol`: ámbito protegido cuando tiene asignaciones vivas y unicidad concurrente de nombres activos, conservando código estable y metadatos de sistema inmutables.
- `administracion-sistema`: identidad precisa/legada de eventos, desempate y minimización/restricción de snapshots; preservar consulta administrativa de solo lectura.
- `migraciones-sql-versionado`: upgrades aditivos con datos, detección de incompatibilidades sin reparación destructiva, inventario real y medición de índices.

## Impact

- **Backend:** Host, Shared (solo Identity/Audit), Storage, Designaciones, Portal y Tareas. Aulas conserva sus contratos/ping y no recibe tablas artificiales. Se modificarán servicios, repositorios, mappings, DTOs y tests relacionados.
- **Contratos/consumidores:** ampliar `ArsDocendi.Storage.Contracts` para coordinación del archivo y consultas de referencias implementadas por cada módulo; ampliar lecturas de `IConsultasIdentity` por ID/lote y búsqueda acotada; actualizar consumidores Portal/Designaciones/Tareas/Host y adapters/tests frontend de versiones/conflictos. Contracts no contendrá lógica ni I/O.
- **DAG:** Host compone implementaciones locales a través de contratos; Storage no dependerá de los internals de Portal/Designaciones. Shared no adquiere I/O de otros schemas. No se prevé un nuevo proyecto/biblioteca ni servicio externo.
- **Datos:** nuevas migraciones SQL+wrapper, índices/constraints y metadata aditiva para concurrencia/auditoría/objetos. No editar baselines publicados ni rehashar el manifiesto para ocultar cambios. Las referencias lógicas existentes permanecen salvo la FK simple de materia expresamente incluida.
- **Compatibilidad:** deploy coordinado backend/frontend por versiones y nuevos 409; no prometer compatibilidad del backend antiguo una vez activadas restricciones, formatos o estados nuevos. Endpoints existentes conservan nombres, permisos, aislamiento por ambiente y protección de PII.
- **Operación/rollback:** preflight de datos en solo lectura, backup verificable de BD+objetos con escritores detenidos y ensayos de upgrade/no-op en destinos desechables. Fallar cerrado ante huérfanos, duplicados normalizados o jerarquías inválidas; saneamiento explícito aprobado, nunca auto-delete/merge/reseed. Preferir roll-forward; recuperación solo en destino aislado con release compatible, sin `Down` destructivo ni restores automáticos.
- **Normativa:** no se inventan nuevas obligaciones reglamentarias ni plazos legales de retención. Las decisiones técnicas/intradía aprobadas se documentan como decisiones de producto; se mantienen las BR existentes y su mapping a tests.
- **Límite:** la auditoría reconstruyó la rama y no inspeccionó catálogos desplegados. El cambio incluye diagnóstico antes del upgrade, no presupone datos reales limpios. Este propose autoriza planificación solamente.
