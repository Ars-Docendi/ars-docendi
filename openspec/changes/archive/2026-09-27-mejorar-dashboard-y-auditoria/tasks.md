## 1. Especificación UX

- [x] 1.1 Crear `docs/product/designs/administracion-sistema-design-spec.md` desde el template del repositorio; definir jerarquía, variantes de acciones, columnas de auditoría y estados responsivos, y verificar que respete `design-principles.md` y los componentes/tokens compartidos.

## 2. Contrato y consulta de auditoría

- [x] 2.1 Extender el DTO para exponer nombre legible del actor, etiqueta de módulo y resumen del evento, conservando clave de fila/request ID en el detalle; verificar el contrato serializado y mantener compatibilidad del endpoint GET.
- [x] 2.2 Agregar joins izquierdos de `changed_by` a `identity.users` y `users.persona_id` a `identity.personas`; verificar actor vinculado, fallback a `display_name`, actor no identificado y ausencia de UPN/correo/PII adicional.
- [x] 2.3 Implementar etiquetas de acción/módulo y resumen basado en metadata y snapshots seguros; verificar INSERT/UPDATE/DELETE, schema conocido/desconocido, valores aprobados y que un UPDATE no se presente como eliminación física.
- [x] 2.4 Hacer que la consulta admita búsqueda por nombre visible del actor, preserve los filtros existentes y aplique filtros antes de paginar; verificar orden estable, conteo y ausencia de duplicados con joins.
- [x] 2.5 Agregar pruebas backend para joins, fallbacks, búsqueda por actor, traducción de módulo/acción y redacción; ejecutar el subconjunto de pruebas de auditoría.

## 3. Interfaz

- [x] 3.1 Reemplazar estilos ad hoc de acciones del dashboard por el `Button` compartido y tokens del design system; usar variante secundaria para Actualizar/Reintentar y verificar focus, disabled, loading y estados parciales.
- [ ] 3.2 Presentar el dashboard con jerarquía clara de componentes y estados independientes; verificar desktop, viewport estrecho y estados de carga/error/desconocido. La media query y el desplazamiento horizontal están implementados; los estados tienen pruebas de componente. Falta inspección visual real porque el navegador bloqueó la URL local interna.
- [x] 3.3 Reorganizar la auditoría para mostrar Fecha, Usuario, Acción, Módulo y Cambio a primera vista; conservar detalle con clave/request ID y valores aprobados; verificar fallback de actor y acciones legibles.
- [x] 3.4 Sustituir el filtro de actor por búsqueda en nombre visible sin exponer UUID como requisito de uso; verificar integración con filtros, paginación y estado vacío.
- [x] 3.5 Agregar pruebas frontend para variante y comportamiento de acciones, tabla resumida, etiquetas y redacción del detalle; ejecutar los tests de la feature.

## 4. Documentación y verificación integrada

- [x] 4.1 Actualizar el contrato API y la documentación de arquitectura para el DTO, los joins, el mapeo de módulos y los límites de privacidad; verificar consistencia con la implementación.
- [x] 4.2 Ejecutar build y pruebas backend/frontend, lint, formato, `git diff --check` y `openspec validate mejorar-dashboard-y-auditoria --strict`; registrar resultados y preservar cambios ajenos.
