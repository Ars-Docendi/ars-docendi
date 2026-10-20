# Design

## Context

Ver `proposal.md` para motivación y `evidence/auditoria.md` para evidencia y límites. El punto de partida es `f10e0a5`; las correcciones usan el working tree actual sin incorporar ni revertir cambios locales ajenos.

- SQL embebido y `[RecursosMigracionSql]` son fuente de DDL; EF registra aplicación por contexto. Se preserva el baseline protegido y los incrementales ya publicados. Los mappings usan `ExcludeFromMigrations`.
- `ServicioAlmacenamientoArchivos` lee estado antes del stream, escribe una misma clave y confirma una copia analizada; no hay concurrencia/versionado de objeto. Portal elimina directamente archivos sustituidos. Los consumidores se enlazan por `ArsDocendi.Storage.Contracts`.
- `MaterializadorDesignaciones` cierra mañana un alta de hoy y abre su reemplazo hoy. Pedido tiene `xmin`, Designacion no; la administración y el circuito escriben sobre el mismo agregado desde caminos diferentes.
- Tareas usa catálogo numérico y referencias lógicas a usuarios. Copia proyecto al crear hijas, pero editar la raíz no actualiza descendientes. Portal incluye cinco colecciones hermanas sin splitting y múltiples servicios materializan directorios completos.
- `audit.log_change` registra snapshots completos y una columna de identidad; `row_history` desempata solo por fecha. El repositorio administrativo sí desempata por ID y redacta la respuesta.
- `infra/scripts/provision-db.sh` crea la base con el usuario app como owner. No se puede prometer protección de snapshots legados frente a ese owner por una simple vista/REVOKE. Separar identidades de migración/runtime es otro alcance no aprobado aquí.

**Decisiones de producto aprobadas:** actualizar la misma vigencia si comenzó en la fecha efectiva del Cambio, y mantener referencias intermodulares lógicas con coordinación/protección/diagnóstico. No se cambia la precisión DATE ni se hace obligatoria una FK a planes.

## Goals / Non-Goals

**Goals:**

- Hacer que las invariantes se sostengan bajo solicitudes concurrentes y fallos entre BD/objetos, y que los estados inválidos por SQL tengan garantías específicas cuando se acordó reforzar DDL.
- Mantener una implementación concreta dentro de los proyectos actuales, contratos puros y DAG; convertir cada riesgo concurrente en una reproducción determinística antes de repararlo.
- Entregar upgrades con datos y mediciones reproducibles, no un rebuild de staging ni umbrales absolutos de tiempo.

**Non-Goals:**

- No aplicar este propose, desplegar, push, reset, fusionar identidades, borrar PII histórica ni asignar plazos normativos.
- No nuevo motor declarativo de migraciones, detector global de drift, microservicio, caché global, event bus, scheduler o biblioteca nueva.
- No restaurar paginación visible, rediseñar la UI, cambiar normativa de aprobación o introducir FKs intermodulares generalizadas.
- No prometer aislamiento frente al owner/DBA, SQL administrativo arbitrario o escrituras que deliberadamente omiten el protocolo de referencias lógicas.

## Decisions

### D1. Una versión cargada se publica una sola vez y no se sobreescribe (A01)

Usar una reserva atómica persistida para la sesión/intento antes de aceptar el cuerpo. Cada intento tiene clave/versionado inmutable generado por servidor; un segundo PUT del intento utilizado no puede escribir la clave publicada. Persistir identificación del intento, clave real y token de estado; validar el tamaño esperado y analizar exactamente sus bytes antes de activar disponibilidad.

La transición de reserva a revisión/publicación hace compare-and-swap de estado/versión. No mantener una transacción ni bloqueo PostgreSQL durante transferencia o antivirus. Un upload/review tiene vencimiento recuperable; limpieza no debe borrar intentos activos. Un proceso interrumpido deja indisponibilidad explícita, no una validación reutilizable por bytes nuevos. La respuesta HTTP y la UI conservan los estados públicos existentes; los estados/reservas internos adicionales requieren SQL incremental y compatibilidad de deploy.

Confirmaciones de la misma versión confirmada son idempotentes. Confirmación y borrado comparten protocolo y estado; si un borrado ya ganó la reserva, un resultado tardío del antivirus no puede publicar. Downloads usan exclusivamente la clave/versionada publicada; no se utiliza una ETag como SHA-256 ni se presupone soporte de versioning/conditional PUT de SeaweedFS sin comprobarlo. La garantía preferida es impedir PUTs sobre la clave publicada mediante el protocolo servidor.

**Alternativas:** solo `xmin` o mutex en memoria no coordinan múltiples procesos ni el objeto remoto; rehash de cada descarga detecta tarde y aumenta I/O; ETags/versioning dependientes del proveedor solo son aceptables con prueba real de cobertura.

### D2. Protección de referencias sin registro redundante ni dependencia circular (A02/A09)

Mantener las referencias existentes en los schemas consumidores como autoridad. Introducir en `ArsDocendi.Storage.Contracts` un contrato puro de consulta de referencias por archivo; Portal y Designaciones implementan consultas selectivas locales. Host registra esas implementaciones en DI. Storage consume interfaces, nunca internals de esos módulos. El conjunto requerido es explícito y se valida en composición; ausencia de un consumidor o error de lectura falla cerrado.

Asociar, desvincular y reservar borrado toman una exclusión transaccional por archivo mediante PostgreSQL, en orden estable de IDs si intervienen varios. La disponibilidad se revalida dentro de la misma operación protegida que persiste la asociación. El borrado bajo exclusión consulta todos los consumidores; con referencias devuelve 409 seguro. Sin referencias reserva indisponibilidad en BD antes de borrar bytes, y finaliza idempotentemente. Un fallo remoto deja estado reintentable no descargable; limpieza usa exactamente el mismo protocolo.

Las mutaciones de Portal y Designaciones separan desvinculación de eliminación: reemplazar el CV libera solo esa referencia; si otro consumidor aún lo necesita, conserva el objeto. El archivo permanece descargable por el pedido autorizado aunque ya no lo sea por el perfil. Sin referencias puede entrar al flujo de eliminación permitido; nunca llamar a DELETE incondicional como parte del reemplazo.

Usar coordinación secuencial sobre una única base PostgreSQL y la frontera transaccional existente, sin MSDTC/transacción distribuida. Para varias instancias de DbContext, reutilizar conexión/transacción de la operación mediante registración local del composition root y enlistamiento implementado por cada módulo; no exponer DbContexts de otro módulo en Contracts. Debe existir una prueba real de rollback multicontexto y de conexión/transacción compartida, no asumir que `TransactionScope` con conexiones simultáneas no promociona.

Para usuario/persona, mantener desactivación como operación ordinaria y no exponer borrado físico sin protección. Validar destino/elegibilidad dentro de la mutación y coordinar cambios administrativos incompatibles mediante bloqueo de la entidad canónica. Las consultas de referencias necesarias para mantenimiento/diagnóstico se implementan en cada módulo y Host las compone por contratos. El diagnóstico detecta escritores/importaciones fuera del protocolo; no se agrega una FK por cada UUID.

**Alternativas:** contador/tabla central de referencias agrega otro estado que puede divergir; consultar sin bloqueo no cierra asociación-vs-delete; Storage → módulos internos crearía fronteras incorrectas; FK sola no protege soft-delete ni SeaweedFS.

### D3. Vigencia intradía, numeración y completitud (A03/A04/A08/A11)

- La función de número conserva `nextval` una vez por asignación y padding mínimo, con longitud de salida al menos la del correlativo. SQL incremental `CREATE OR REPLACE` sin tocar secuencia/historia.
- Para Cambio, obtener/proteger la vigente y su versión antes de materializar. Si inicio = fecha efectiva, actualizar ID existente con cargo/dedicación/tres horas y `origen_pedido_id` nuevo; mantener `vigente_desde`. Registrar anterior/nuevo incluyendo ambos orígenes en audit e historial del trámite, y mantener snapshot inicial. Para inicio anterior, cierre `[inicio,fecha)` y nueva apertura en fecha, con EXCLUDE preservado. Si inicio es posterior, rechazar conflicto de vigencia; no retroceder fechas silenciosamente.
- Mantener la fecha efectiva usada actualmente por el circuito, calculada una vez por operación; este cambio no decide sustituirla por rango del período ni zona nueva. En tests inyectar reloj/fecha determinística. La semántica vigente de consultas sigue siendo `vigente_hasta IS NULL`; no introducir vigencia temporal nueva sin otro cambio. La Baja intradía conserva su cierre mínimo actual mientras no se apruebe otra política.
- Alta/Cambio necesitan cargo/dedicación elegibles y horas > 0 antes de envío, reenvío y aprobación. Guardado no amplía admisión actual. Añadir CHECK condicional para estados fuera de borrador donde corresponda, evaluando legados sin completar ceros ni columnas faltantes. API ofrece errores por campo y preserva BR/documentos/legajo; adaptar formularios sin inventar reglas nuevas.
- Añadir token `xmin` a Designacion y proteger agregado persona–materia dentro de ambas rutas. El guard del usuario de administración no sustituye esa coordinación. Snapshot al enviar y materialización comparten estado protegido; una versión obsoleta devuelve 409 o produce una revalidación explícita antes del commit, nunca retry ciego.

**Alternativas:** quitar EXCLUDE pierde historia; intervalos vacíos pierden semántica; posponer todos los cambios a mañana contradice la decisión aprobada; timestamps cambian más modelo/contratos de lo necesario.

### D4. Integridad Identity con garantías de BD y contratos de error (A07/A10/A12)

- UNIQUE funcional de `lower(btrim(upn))`, misma expresión en alta/edición/login. Preflight de equivalencias; conservar IDs/vínculos/roles sin fusión. No inventar UPN ni activar cuentas.
- UNIQUE funcional parcial de `lower(btrim(name)) WHERE is_active` para roles activos. Preflight incluye sistema/personalizados. Traducción específica de 23505 por constraint, no conversión indiscriminada de cualquier error a duplicado.
- Guard en servicio y BD sobre cambios de ámbito personalizados con asignaciones vivas. Bloquear el rol de modo compatible entre asignación y cambio; la lectura/validación de scope debe participar del bloqueo. Una revocación auténtica de una asignación incoherente legada puede cerrar su actividad sin exigir inventar nuevo ámbito; no permite nuevos grants incoherentes. Asignaciones revocadas conservan su contexto histórico.
- Proteger metadata de roles de sistema conforme al SQL y `editar-rol`; corregir el escenario viejo que permitía renombrarlos. Corregir el escenario genérico de scope materia que exigía carrera erróneamente. Docente conserva materia+carrera; Jefe/personalizado de materia conservan materia sin carrera.
- Extender `IConsultasIdentity` solo con lecturas selectivas por cuenta, lote y búsqueda. No trasladar a Shared lectura de schemas de negocio ni metadata Storage.

**Alternativas:** normalizar solo en C# deja otros escritores y carreras desprotegidos; bloqueo/`xmin` por rol individual no resuelve unicidad entre roles distintos; revocar automáticamente todas las membresías al editar ámbito es una decisión destructiva no aprobada.

### D5. Jerarquía Tareas y guards versionados (A05/A06/A11/A12/A13)

- Cambiar proyecto de raíz actualiza por recorrido recursivo la raíz y todos sus descendientes dentro de una transacción. Solo propaga el campo heredado y sus versiones/eventos, no campos propios. El escritor autorizado de raíz no recibe autoridad sobre otros atributos de hijas creadas por otros usuarios.
- Igualdad de proyecto incluye NULL. **Corrección del alcance de auditoría:** una hija con proyecto NULL no es inválida si su padre también carece de proyecto; root sin proyecto está permitida por `tareas/spec.md`. El probe de ciclo sigue demostrando una brecha real.
- Bloqueo por raíz/componente, orden de IDs consistente y validación de ancestros. Trigger diferible al commit para igualdad de proyecto permite el update coordinado sin rechazar estados internos temporales. Garantía de aciclicidad toma la misma exclusión sobre el componente; un simple recorrido sin coordinación no basta ante dos cambios de padre concurrentes. La API no incorpora edición de padre/reparenting como feature nueva.
- Hijas y raíces verifican proyecto efectivo y `admite_tareas` dentro de transacción protegida frente a cierre. Cambiar estado del proyecto debe coordinar con esa lectura; no hardcodear finalizado/cancelado.
- Token `xmin` en Tarea/Proyecto, versión en respuestas y DTOs de mutaciones; validar antes de guardar y mapear 409 con código existente `concurrency-conflict`. Comentarios son append independiente pero deben revalidar visibilidad/actor bajo guard protegido; relaciones crean/reutilizan el par canónico después de autorización. UPSERT o traducción/relectura precisa resuelve 23505 sin duplicar eventos.
- Reintroducir protección SQL de Resuelta mediante trigger que resuelve `codigo` desde el catálogo numérico; preservar FK de estado y no fijar ID. Proteger cambios de códigos/comportamiento de catálogo que invaliden datos; no ampliar UI de edición de catálogos.
- Listado resuelve solo IDs visibles por lote y relaciones donde alguno de los extremos pertenece a ese conjunto. Candidatos se filtran/ordenan en SQL antes del límite; mantener todas las palabras, sin tildes/case y la jerarquía vigente.

**Alternativas:** impedir todo traslado con hijas evita el bug pero limita el flujo elegido; cascada en memoria fuera de transacción falla ante profundidad/concurrencia; CHECK con subconsulta a catálogo no es una garantía SQL admisible.

### D6. Portal: acceso acotado, versión por sección y altas incidentales idempotentes (A11/A12/P01/P02/P03)

Separar consultas de lectura completa y mutación de ítem. Lectura completa usa split/proyecciones controladas; una escritura obtiene solo identidad, sección y referencias pertinentes. Si la lectura completa necesita coherencia entre colecciones, usar una transacción de snapshot compatible y acotada; documentar round trips adicionales sin mantenerla durante I/O remoto.

Índices incrementales `(perfil_id)` en experiencias, educaciones, certificaciones y proyectos, revisando catálogo para evitar equivalentes. Mantener IDs/constraints actuales. Resolver metadata de archivos por batch mediante Storage.Contracts; resolver persona por cuenta concreta, no `ListarUsuarios`.

UPSERT/relectura del perfil único por persona para creaciones incidentales y del término normalizado para sugerencias; transacción de reemplazo de tags conserva listas independientes. Colisión real de ediciones sobre la misma sección no se trata como idempotencia: `xmin`, versión y 409. Añadir versión de sección a DTOs de lectura/mutación existentes; secciones nuevas permiten creación sin versión de fila inexistente. Tests deben cubrir dos primeras secciones distintas y dos escritores del mismo contacto.

**Alternativas:** cachear todo el directorio oculta crecimiento/consistencia; splitting por sí solo no elimina N+1 ni scans por perfil; un índice presente en el snapshot EF no implica que el DDL lo haya creado.

### D7. Auditoría precisa sin reescribir ni purgar legado (A14/P04)

Añadir metadata de clave completa/formato de manera aditiva. Extender `audit.attach` para declarar columnas de PK compuesta manteniendo llamadas simples compatibles. Eventos nuevos conservan clave completa determinística para rol_permisos y docente_habilidades. Consultas precisas usan clave completa; legado puede reconstruirse al leer solo si sus snapshots contienen todos los componentes y la operación no es ambigua. Si no, se declara agrupación legacy; no hay backfill que invente componentes ni rewrite de eventos.

`row_history` selecciona una vez cada evento pertinente, con orden `(changed_at,id)` y actor/fecha de la misma fila. Preservar semántica de la firma actual para claves simples y dar superficie explícita para identidad compuesta, sin presentar filtros de grupo como historia de una relación exacta.

Minimización en captura SQL de nuevos snapshots mediante clasificaciones explícitas por tabla/campo: IDs técnicos y valores necesarios de vigencias/orígenes se conservan; PII, secretos, texto documental no clasificado, buckets/claves/URI y contenido de comentarios no se duplican. `changed_columns` sigue describiendo qué cambió. Evitar pérdida de trazabilidad intradía por aplicar una supresión indiscriminada.

No purgar historia previa: mantener redacción fail-closed de API, clasificación/documentación de acceso directo/backups y procedimiento manual de tratamiento solo si se autoriza una política institucional. El runtime/DBA owner actual puede leer datos históricos; declarar esa limitación. No incluir separación de privilegios, nuevos secretos o cifrado de todos los backups como cambios implícitos de este propose.

Medir listado y COUNT por separado; evaluar B-tree de fecha/ID y filtros predominantes frente a BRIN e índices de historia existentes. El resultado aceptable de un candidato sin beneficio es descarte motivado con evidencia, no un índice obligatorio.

**Alternativas:** cambiar `row_pk` de todos los eventos pierde compatibilidad/inmutabilidad; redactar solo UI no minimiza persistencia futura; una vista no aísla frente al owner.

### D8. DDL incremental, datos legados y rendimiento medido (A09/P02/P04)

Añadir FK simple materia de Designacion, sin FK materia–plan/carrera. Diagnóstico operativo propuesto `--diagnosticar-integridad` en el CLI de Host: nuevo modo de solo lectura con salida segura de categorías/conteos/IDs técnicos, ambiente, release e historial. Cada módulo consulta su propio schema; Host compone contratos sin Shared → módulos. No es un comando existente ni un diff engine.

Preflight antes de writes/migración y comprobación dentro de las operaciones incrementales para evitar TOCTOU cuando un escritor no fue detenido. FKs/UNIQUE/CHECK/trigger se validan antes de éxito; variantes NOT VALID solo son fase intermedia explícita, nunca final aceptado. Para instalaciones nuevas, orden Identity → Storage → Designaciones → Aulas → Portal → Tareas; cada incremental mantiene el orden de su contexto. Evitar SQL de Storage que presuma que Portal/Designaciones ya existen en instalación limpia.

Cuatro índices de Portal están justificados por consulta medida; el resto son candidatos. Capturar SQL de EF, EXPLAIN ANALYZE/BUFFERS, filas y bytes/materialización, consultas emitidas, tamaño de índices y trabajo de inserción/update. Dataset base Portal 2.000/20.000 y perfil 3×3×3×2×4; datasets de Tareas mantienen visibles constantes y crecen datos ajenos; pedidos distinguen estado/ámbito y audit página/COUNT. Repeticiones warm/cold documentadas, sin prometer tiempos absolutos productivos. No restaurar paginación visible para alcanzar un benchmark.

## Risks / Trade-offs

- [Protocolo BD/objetos tiene estados recuperables] → pruebas con proveedor real, cortes entre reserva/subida/escaneo/confirmación/borrado y retries idempotentes; no éxito parcial.
- [Múltiples DbContexts/conexiones pueden promover transacción] → una conexión/transacción por operación, tests reales de enlistamiento y rollback; ningún requisito de MSDTC.
- [Locks de jerarquía/referencias aumentan contención] → granularidad por agregado/archivo, orden estable, timeout seguro y tests de dos sesiones; no bloquear DB durante stream/antivirus.
- [Versiones nuevas rompen clientes viejos] → cambio coordinado DTO/backend/frontend, errors consumibles, invalidación de caches y sin retry automático de una intención obsoleta.
- [Legados inválidos bloquean upgrade] → diagnóstico portable, salida segura y saneamiento manual aprobado antes de activar garantías. No inventar datos para pasar tests.
- [Split query puede observar secciones de momentos distintos] → snapshot acotado cuando el contrato exige lectura coherente, medición de round trips y separación de writes.
- [Minimizar snapshots reduce valores disponibles] → allowlist técnica explícita preserva cargo/horas/origen para trazabilidad, legacy sigue disponible con redacción; no prometer eliminación retrospectiva de PII.
- [Índices aumentan costo de escritura] → justificar cada uno y comparar cobertura parcial/GiST antes de añadirlo; registrar candidatos descartados.
- [Specs principales contienen inconsistencias antiguas] → deltas completos de requisitos tocados, preservar escenarios no afectados y explicitar reconciliación; no realizar una limpieza global de specs ni revertir políticas vigentes de seed/auth.

## Migration Plan

1. Congelar referencia anterior y ejecutar reproducción/diagnóstico en bases desechables. Verificar grafo, consumidores y mapping completo A01–A14/P01–P04. Incorporar solo evidencia sintética, sin PII.
2. Implementar verticalmente RED→GREEN por hallazgo, contratos, frontend y DDL incremental. Build/runner/resources verificados; no cambios al baseline ni al manifiesto histórico para ocultar drift.
3. Ensayar instalación nueva y upgrade desde `f10e0a5` con datos, archivos ya disponibles/referenciados, eventos legados, cuentas Microsoft vinculadas y referencias históricas inactivas. Metadata aditiva de uploads antiguos mantiene sus claves existentes para lectura, pero ninguna sesión anterior ambigua se publica sin protocolo verificable.
4. Ensayar upgrade incompatible y rollback de transacción: no datos parciales, no eventos fabricados, no fusión/purga. Confirmar no-op, preview sin writes, hashes/bytes de objetos e historial por contexto.
5. Preparar procedimiento de release: retirar escritores conocidos, backup verificable BD+objetos, diagnóstico, aplicar candidato e inspeccionar garantías y pings/descargas. Activación real requiere solicitud/autorización separada; este cambio no despliega servidores.
6. Ante fallo, detener candidato/publicación y conservar backup/evidencia. Preferir roll-forward. No arrancar backend viejo sobre estados/contratos nuevos sin ensayo compatible. Recuperar solo en destino aislado y mediante el procedimiento institucional aprobado; no Down que borre historia.

## Trazabilidad de aceptación

| Hallazgo | Decisión / capability                  | Tareas principales |
| -------- | -------------------------------------- | ------------------ |
| A01      | D1 / almacenamiento-adjuntos           | 2.1–2.3            |
| A02      | D2 / almacenamiento-adjuntos           | 2.4–2.6            |
| A03      | D3 / persistencia-designaciones        | 3.1                |
| A04      | D3 / persistencia-designaciones        | 3.2–3.3            |
| A05      | D5 / tareas-api                        | 5.1                |
| A06      | D5 / tareas-api                        | 5.2                |
| A07      | D4 / persistencia-identity, editar-rol | 4.2                |
| A08      | D3 / pedidos-designacion               | 3.4                |
| A09      | D2,D8 / integridad-referencias-logicas | 1.2–1.3,3.5        |
| A10      | D4 / persistencia-identity             | 4.1                |
| A11      | D3,D5,D6 / mutaciones versionadas      | 3.6,5.3,6.3        |
| A12      | D4,D5,D6 / unicidad concurrente        | 4.3,5.4,6.4        |
| A13      | D5 / tareas-api                        | 5.5                |
| A14      | D7 / administracion-sistema            | 7.1–7.4            |
| P01      | D6 / portal-docente-api                | 6.1                |
| P02      | D6,D8 / índices Portal                 | 6.2                |
| P03      | D4–D6 / consultas acotadas             | 3.7,4.4,5.6,6.5    |
| P04      | D7,D8 / índices medidos                | 6.6,7.5            |

## Open Questions

Solo se difieren detalles que no cambian contratos/alcance: IDs y nombres exactos de nuevas migraciones al aplicar, forma de los datasets adicionales de benchmark y duración de los ensayos según recursos disponibles. No queda pendiente la política intradía ni la elección de referencias lógicas. No se asigna un plazo de tratamiento de auditoría legada: sin autorización institucional no se ejecuta purga y esa limitación es explícita.
