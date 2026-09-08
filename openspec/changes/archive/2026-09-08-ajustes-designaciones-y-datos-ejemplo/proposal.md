## Why

El circuito de Designaciones mezcla horas solicitadas con valores históricos, limita indebidamente la dedicación y carece de una exportación consolidada por período. Los ambientes de prueba conservan datos entre despliegues y contienen ejemplos incompletos que dificultan verificar el circuito real.

## What Changes

- **BREAKING**: retirar `Sin novedad` de las nuevas altas y ediciones de pedidos. La continuidad docente se obtiene de las designaciones vigentes, sin generar trámites artificiales.
- **BREAKING**: reemplazar dedicaciones de texto por un catálogo en `designaciones`, con Categorías 1 a 6 de elección libre y referencias canónicas en pedidos, designaciones y administración de docentes. Categoría 0 deja de ser seleccionable.
- Corregir lectura, presentación y edición de horas de materia, investigación y externas, conservando por separado el snapshot histórico.
- Mostrar historial con fecha y hora en orden ascendente, y usar siempre el número del pedido como identificación visible.
- Agrupar las pantallas de Designaciones bajo el sector lateral DESIGNACIONES, sin enlace padre redundante.
- Exportar Excel desde Finalizados para Decanato, Secretaría Académica y Administrativo: pedidos `en_lote` del período configurado en Períodos de Designación y una hoja de designaciones resultantes que incluya continuidades.
- Mantener el subrayado de las pestañas durante hover, con verde claro, y agregar filtro Estado al tablero.
- Reiniciar completamente la base de staging y de cada PR en cada despliegue efectivo, antes de migraciones y datos sintéticos.
- Completar ejemplos con cargas horarias positivas, inicio del trámite y autor Jefe de Cátedra asociado a la materia y al docente; conservar cobertura de estados y roles.

## Capabilities

### New Capabilities

- `exportacion-lote-designaciones`: Excel por período, autorización, pedidos finalizados y continuidad de designaciones.
- `navegacion-designaciones`: sector lateral DESIGNACIONES y acceso a sus pantallas por rol.

### Modified Capabilities

- `pedidos-designacion`: novedades admitidas, dedicación libre mediante catálogo, horas editables separadas del snapshot e identificación visible por número.
- `persistencia-designaciones`: catálogo de dedicaciones, snapshot, historial y continuidad sin pedido.
- `tablero-revision-tabla`: filtro Estado y hover de pestañas.
- `pipeline-deploy-ci`: reconstrucción de bases descartables en cada despliegue.
- `datos-ejemplo-no-productivos`: cargas, autores, asociaciones e historiales completos y reproducibles.

## Impact

- Backend: `Modules.Designaciones`, sus contratos de administración y consumidores en la administración de docentes del Host. Frontend: formularios y API de Designaciones, administración de docentes y shell. Infraestructura: `spin-up.sh`, scripts de base y seed; workflows existentes continúan llamando al mismo punto de entrada.
- Migraciones nuevas para catálogo/FK y cargas complementarias del estado vigente; cambios coordinados de DTOs de catálogos, pedidos y administración; nuevo endpoint de descarga. No se modifica la normativa de aprobación ni el grafo entre módulos. Se mantienen contratos puros y lectura de identidad mediante `IConsultasIdentity`.
- Las decisiones funcionales provienen de esta solicitud y su aclaración sobre período y categorías; no se les atribuye una fuente reglamentaria inexistente. Las BR vigentes mantienen sus identificadores y tests.
- Actualizar contratos HTTP, modelo de datos, documentación del dominio, diseño UX y runbook en el diff de implementación.
- Rollback: backup previo a migrar bases persistentes; migraciones aditivas y compatibilidad histórica sin recategorizar datos silenciosamente. Ante incompatibilidad, restaurar backup y versión conjunta de backend/frontend. En staging/PR, reconstruir desde migraciones y seed de la versión elegida; el reinicio no recupera datos de sesiones anteriores. Producción queda excluida del reinicio.
