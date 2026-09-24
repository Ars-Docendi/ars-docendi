## Context

Ver `proposal.md` para la motivación y `specs/administracion-sistema/spec.md` para el contrato observable. El backend ya expone pings HTTP para los cuatro módulos; son smoke tests y no verifican PostgreSQL. `ArsDocendi.Shared` ya modela `audit.change_log` en `IdentityDbContext` como lectura, con `changed_by`, `changed_at`, snapshots JSON, columnas cambiadas y `request_id`. El DDL también contiene `client_ip`, pero no forma parte del modelo de lectura actual. El catálogo de permisos es cerrado y la sesión, sidebar y guards del frontend se basan en permisos efectivos.

## Goals / Non-Goals

**Goals:**

- Componer estados actuales de los pings existentes y una comprobación acotada de PostgreSQL.
- Exponer una consulta de auditoría filtrada y paginada, protegida por autorización backend y frontend.
- Limitar la exposición de datos personales y secretos almacenados en snapshots.
- Conceder permisos iniciales a `sys_admin` sin acoplar la navegación al nombre del rol.

**Non-Goals:**

- Historial de disponibilidad, alertas, SLA, gráficas de latencia/error, métricas de pool o estado de procesos de infraestructura externos.
- Cambios de retención, particionado, estructura o triggers de `audit.change_log`.
- Exportación de auditoría, operaciones de edición/eliminación o exposición de IP de cliente.

## Decisions

1. **Permisos dedicados.** Agregar `sistema.estado.ver` y `auditoria.ver` al catálogo cerrado, registrarlos en las políticas de autorización y usarlos en sidebar, rutas y API. La migración SQL versionada insertará los permisos y otorgará ambos explícitamente al rol de sistema `sys_admin`. No se dependerá del seed inicial que asignó el catálogo existente completo: ese seed no vuelve a ejecutarse al actualizar una instalación. Alternativa descartada: reutilizar `usuarios.ver` o `roles.ver`, que concedería acceso por una responsabilidad distinta.

2. **Dashboard compuesto por comprobaciones existentes y una prueba de DB.** Consultar los cuatro pings existentes en paralelo y obtener el estado de PostgreSQL mediante una consulta mínima, acotada por timeout, ejecutada en el backend. Mostrar estados independientes, hora y duración de la prueba de DB; no agregar almacenamiento histórico ni inventar uptime o tasas de error sin instrumentación. La respuesta de DB será protegida por `sistema.estado.ver` y sólo expondrá estado y duración, nunca la excepción cruda ni configuración de conexión. Alternativa descartada: mostrar un único indicador verde basado sólo en que la página cargó, que no detectaría fallas parciales.

3. **Lectura de auditoría por API con límites.** Usar una consulta de solo lectura sobre la entidad `RegistroCambio`, sin tracking, con filtros parametrizados, página acotada y orden determinista por `changed_at DESC, id DESC`. Filtros temporales y por metadatos se traducirán a una consulta SQL acotada; el tamaño máximo de página tendrá un límite explícito. Alternativa descartada: cargar todos los eventos al navegador, lo que escala mal sobre un log append-only.

4. **Redacción por lista segura.** La respuesta listará metadatos y columnas cambiadas. El detalle sólo serializará valores previos/nuevos de campos explícitamente aprobados; PII y secretos se enmascararán y todo campo no clasificado se ocultará por defecto. No se mapeará ni expondrá `client_ip`. No se enviarán los JSON completos de `old_row` o `new_row` al cliente. Esto reduce exposición accidental, aunque exige pruebas con filas de identidad y otras tablas auditadas.

5. **Estados remotos claros y ninguna escritura.** El frontend usará el cliente HTTP y el patrón de consultas existente; cada pantalla diferenciará carga, resultado vacío, error recuperable y éxito. Los endpoints de auditoría serán GET y no ofrecerán operaciones de mutación. No se agregará un mecanismo de retención ni se alterará el historial existente.

6. **Contratos y límites arquitectónicos.** Mantener el acceso a `audit` dentro de la infraestructura permitida de identidad/auditoría; ubicar orquestación y endpoints en la superficie administrativa del Host, conservando el flujo Controller → Service → Repository. No se agregará I/O fuera de `identity`/`audit` en `ArsDocendi.Shared`, ni se crearán dependencias entre módulos de negocio.

## Risks / Trade-offs

- [Una clave nueva de snapshot puede contener PII no reconocida] → Ocultar valores no clasificados por defecto, probar las claves sensibles presentes en el modelo de datos y no serializar snapshots crudos.
- [Pings exitosos no garantizan que todas las operaciones de negocio funcionen] → Etiquetarlos como smoke/estado HTTP de módulo y presentar separadamente la comprobación de DB; no denominarlos diagnóstico exhaustivo.
- [Una consulta de auditoría amplia puede cargar PostgreSQL] → Aplicar filtros parametrizados, límite de página, orden con desempate por ID, timeout y pruebas de volumen razonable.
- [Despliegues existentes no recibirían permisos nuevos sólo por el seed inicial] → Incluir inserción idempotente y membresía explícita de `sys_admin` en SQL versionado, y verificar actualización desde una base ya migrada.

## Migration Plan

1. Desplegar el change con migración SQL aditiva de los permisos y su asignación inicial a `sys_admin`; ejecutar el mecanismo habitual `--migrate` antes de habilitar la nueva UI.
2. Desplegar backend y frontend. Verificar las dos políticas, pings de módulos, comprobación de DB, filtros, paginación y redacción de snapshots.
3. Rollback de aplicación: volver a la versión anterior y dejar los permisos aditivos sin uso. No borrar permisos ni membresías automáticamente durante rollback, porque podrían haber sido asignados a otros roles; la limpieza, si alguna vez se requiere, deberá verificarse y aprobarse aparte.
