## Context

La propuesta está en `proposal.md` y el contrato observable actualizado, en `specs/administracion-sistema/spec.md`. `RepositorioAuditoria` pagina `audit.change_log` y `ServicioAuditoria` redacta snapshots mediante una lista de campos seguros; el DTO actual sólo contiene el UUID del actor, acción técnica, schema/tabla y claves de campos. `IdentityDbContext` modela `identity.users` y `identity.personas`: `changed_by` referencia opcionalmente una cuenta y `users.persona_id` puede vincularla a una persona. La UI muestra esos valores técnicos y define sus propios estilos en `sistema.css`.

La identidad y auditoría son infraestructura transversal permitida en `ArsDocendi.Shared`. Los datos históricos provienen de `audit.change_log`; la tabla no requiere escrituras ni nuevos objetos de base de datos.

## Goals / Non-Goals

**Goals:**

- Hacer reconocibles a primera vista el actor, la acción, el módulo y el cambio auditado.
- Mejorar la jerarquía visual del dashboard y mantener visibles los estados independientes.
- Reutilizar componentes y tokens del design system, con estados accesibles y responsivos.
- Preservar permisos, paginación, filtros, orden estable y la política de redacción.

**Non-Goals:**

- Cambiar retención, triggers, schema, permisos o autorización.
- Leer las filas actuales de tablas de negocio para reconstruir eventos históricos.
- Exponer snapshots crudos, UPN, correo, `client_ip` o valores no clasificados.
- Agregar métricas históricas, alertas o soporte de auditoría a schemas que hoy no registran eventos.

## Decisions

1. **Resolver el nombre del actor con joins opcionales.** Proyectar eventos mediante `LEFT JOIN audit.change_log.changed_by → identity.users.id` y, cuando `persona_id` exista, `LEFT JOIN identity.personas`. Usar `Apellido, Nombre` como etiqueta preferida y `users.display_name` como fallback; si no se resuelve una cuenta, presentar `Actor no identificado`. No seleccionar UPN, correo, documento, CUIL ni otros campos personales. La relación de usuario/persona es a lo sumo uno a uno, por lo que la unión no debe multiplicar eventos.

2. **Derivar el módulo del schema, no de una tabla nueva.** Mantener un mapeo de presentación versionado en la aplicación para `identity`, `designaciones` y `portal`; para otro schema, mostrar un fallback neutral basado en su nombre. No crear catálogo persistido de módulos ni inferir eventos de Aulas/Tareas: sólo se muestran fuentes que efectivamente registran en `audit.change_log`.

3. **Construir un resumen desde el evento histórico y con redacción por defecto.** Traducir `INSERT`, `UPDATE` y `DELETE` a etiquetas en español; presentar el objeto y los campos modificados con etiquetas legibles y valores anteriores/nuevos sólo si pasan la lista segura existente. El resumen se calcula desde metadata, `changed_columns` y snapshots sanitizados, sin consultar la fila vigente: ésta podría haber cambiado o haber sido eliminada. Un `UPDATE` de baja lógica sigue siendo una actualización salvo que exista una regla de dominio explícita y verificable para etiquetarlo de otra forma.

4. **Mantener filtros y paginación coherentes con las etiquetas.** Aplicar filtros y join de actor en la consulta antes de paginar; permitir búsqueda por el nombre visible del actor, conservar el filtrado por schema/tabla y mantener `changed_at DESC, id DESC`. El conteo debe usar la misma condición de filtros, sin duplicar eventos.

5. **Reutilizar el design system en ambas pantallas.** Crear una especificación UX para Administración del Sistema según `docs/product/designs/_design-spec-template.md`. Mantener `PageHeader`, usar el `Button` compartido y presentar **Actualizar**/**Reintentar** como acciones secundarias; reservar `primary` para una acción primaria real. Usar tokens y componentes compartidos en lugar de colores/radios hardcodeados. El dashboard puede organizar los componentes como resúmenes compactos de estado, sin gráficas decorativas, conservando estados disponibles, no disponibles, desconocidos, carga y error.

6. **Contrato aditivo y sin migración de datos.** Extender el DTO de auditoría con etiqueta del actor, módulo y resumen; actualizar el contrato documentado y el consumidor frontend en el mismo cambio. No se modifica el DDL ni la autorización. Los detalles técnicos de fila y `request_id` quedan disponibles en el detalle secundario.

## Risks / Trade-offs

- [El nombre visible del actor es dato personal] → Exponer sólo el nombre necesario a quienes ya poseen `auditoria.ver`; no enviar UPN, correo, documento ni snapshots no filtrados.
- [Una búsqueda textual de actores podría ampliar el costo de consulta] → Limitarla al join indexado por `changed_by`, aplicar longitud máxima y timeout existente, y probarla con paginación.
- [Un evento histórico puede no corresponder al estado actual de su fila] → Derivar el resumen de los snapshots del propio log y no de joins a las tablas auditadas.
- [Un schema futuro puede no tener etiqueta de producto] → Mantener fallback explícito al nombre de schema sin fallar la consulta.

## Migration Plan

1. No requiere migración SQL ni cambio de permisos. Publicar DTO y UI compatibles dentro del mismo despliegue.
2. Verificar la lectura de eventos con actor vinculado, cuenta sin persona, actor nulo y schema desconocido; verificar que los filtros precedan a la paginación y que no haya duplicación.
3. Validar dashboard y auditoría con pruebas accesibles de carga, error, estado parcial, filtros y redacción.
4. Rollback: volver al backend y frontend previos; no hay datos persistidos nuevos que revertir.
