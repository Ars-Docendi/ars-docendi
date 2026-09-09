## 1. Catálogo y persistencia

- [x] 1.1 Crear migración versionada de dedicaciones 1–6 y FK en pedidos/designaciones con auditoría; verificar en DesignacionesPersistenciaTests referencias inválidas, unicidad y migración desde base vacía y schema anterior.
- [x] 1.2 Implementar preservación de valores legados sin séptima opción ni recategorización automática y restricciones de nuevas escrituras; verificar Categoría 0 histórica legible, selección nueva rechazada y columnas legadas no utilizables para insertar texto arbitrario.
- [x] 1.3 Propagar IDs de dedicación por catálogos, DTOs de pedidos, contratos de administración y consumidores Host/frontend; verificar CatalogosDesignacionesTests y edición de docentes con las seis opciones y rechazo de referencias inválidas/inactivas.
- [x] 1.4 Agregar horas de investigación y externas a designaciones vigentes, recuperar valores de pedidos de origen cuando existan y conservar NULL desconocidos; verificar materialización Alta/Cambio, consultas y toma de snapshots con valores distintos de la solicitud.

## 2. Pedidos y corrección de horas

- [x] 2.1 Reemplazar el test que exige sustituir horas por snapshot en pedidosApi.test.ts por regresiones de Alta enviada y Cambio devuelto, y comprobar que fallen antes de corregir el mapeo; deben diferenciar valores solicitados e históricos para las tres cargas.
- [x] 2.2 Separar snapshot y datos editables en types, pedidosApi, PedidoForm y paneles de detalle; verificar guardar, recargar, editar devuelto y reenviar sin perder horas ni modificar el snapshot con los tests de API/formulario existentes extendidos.
- [x] 2.3 Retirar Sin novedad de nuevas solicitudes, defaults y avance del circuito en frontend/backend y persistencia; verificar rechazo directo por API, lectura fiel de legados y continuidad sin crear pedidos. Mantener las BR de adjuntos, legajo, ámbito e idempotencia mediante PedidosApiTests/PedidosHttpTests.
- [x] 2.4 Eliminar restricción de mejora de dedicación en formulario y validación compartida; ajustar PedidoForm.test.ts y pedidoValidacion.test.ts para aceptar Categoría 1, 2 y 6 partiendo de 2, y confirmar el mismo comportamiento por HTTP.

## 3. Historial, identificación y tablero

- [x] 3.1 Agregar regresión mínima de historial con eventos del mismo día fuera de orden; corregir formato fecha/hora y orden ascendente estable entre API y detalleAdapters, verificando las horas locales y el snapshot sin cambios.
- [x] 3.2 Sustituir UUIDs visibles por numero en detalle, edición, modales y mensajes; buscar todos los usos antes de cambiar y verificar con un pedido cuyo UUID y número sean distintos que el DOM muestre sólo el número como identificación.
- [x] 3.3 Reorganizar NAV_BY_ROLE bajo DESIGNACIONES y quitar el enlace padre redundante; verificar Sidebar.test.tsx para cada rol y navegación contraída/teclado, preservando los cambios locales previos en Sidebar.tsx.
- [x] 3.4 Incorporar Estado a FiltrosLista y aplicarFiltros; verificar combinación de filtros, devueltos de distintas áreas, contadores, restablecimiento y vacío con filtrosTablero.test.ts y TablaRevision.test.tsx.
- [x] 3.5 Reproducir hover en el navegador inspeccionando estilos computados, aplicar el ajuste verde claro acotado a revisión y comprobar subrayado activo y foco por teclado; dejar evidencia visual de antes/después sin editar la biblioteca instalada.

## 4. Exportación del lote

- [x] 4.1 Agregar consulta y endpoint del lote por período activo con autorización de los tres roles y ámbito; verificar respuestas sin autenticación, rol no autorizado, período inexistente/inactivo y cambio concurrente de período mediante tests HTTP.
- [x] 4.2 Resolver ambos conjuntos en una lectura consistente: pedidos en_lote del período y designaciones vigentes con continuidad; verificar Alta, Baja, Cambio, docente sin pedido, docente con varias materias, otros estados/períodos y ausencia de mutaciones o duplicados.
- [x] 4.3 Generar el XLSX de dos hojas definido en design.md; verificar partes/relaciones del libro, columnas, fechas y números tipados, legajos con ceros, textos con caracteres de fórmula y valores desconocidos mediante un check de contenido y apertura en un lector de hojas de cálculo.
- [x] 4.4 Incorporar Exportar a la derecha de las pestañas sólo en Finalizados y roles habilitados, con período explícito, progreso, error/reintento y ausencia de período; verificar que filtros de tabla no recorten el archivo y que se pueda descargar una hoja de continuidades sin pedidos aprobados.

## 5. Datos sintéticos y despliegues

- [x] 5.1 Actualizar sintetico.sql con seis categorías, cargas positivas y diferentes entre snapshot/solicitud, continuidad sin pedido y novedades admitidas; extender SeedSinteticoTests para comprobar cobertura, valores y segunda ejecución idempotente.
- [x] 5.2 Completar jefes y ámbitos por materia, asociaciones docentes y eventos crear/enviar/transiciones hasta cada estado; verificar en SeedSinteticoTests que todo autor sea Jefe de su materia y todo pedido en revisión tenga Inicio, con Altas sin designación previa artificial.
- [x] 5.3 Reutilizar drop-db.sh dentro de spin-up.sh para reset exclusivo de staging/PR y ordenar detención, recreación, migraciones, seed y puesta en servicio; verificar con un check ejecutable de orquestación que prod jamás alcanza reset/seed y una falla detenga la publicación.
- [x] 5.4 Serializar la reconstrucción del mismo ambiente manteniendo gates de PR y aislamiento entre ambientes; verificar ejecuciones concurrentes y reintento tras interrupción con el check de scripts y configuración de workflows.
- [x] 5.5 Verificar en bases locales descartables dos despliegues consecutivos con una fila añadida entre ambos; confirmar que desaparezca, que numeración/metadata se reinicien y que una base vecina permanezca intacta. Registrar comandos y resultado sin operar sobre producción.

## 6. Documentación y verificación integrada

- [x] 6.1 Actualizar api-contracts-designaciones.md, contratos de administración afectados, data-model.md, diagramas de datos y domains/designaciones.md con catálogos, horas, endpoint y continuidad; comprobar coherencia con DTOs/DDL y que dependency-graph.md refleje las fronteras conservadas.
- [x] 6.2 Actualizar rediseno-designaciones-exploracion.md y el design spec aplicable con elección libre 1–6, ausencia de Sin novedad, separación de horas, historial, número, sidebar, Estado y Exportar; eliminar contradicciones de las decisiones reemplazadas y conservar trazabilidad a esta solicitud sin inventar citas normativas.
- [x] 6.3 Actualizar runbook de infra con estado cero, orden de reconstrucción, fallos y rollback; verificar que documente los comandos reales y la exclusión de producción.
- [x] 6.4 Ejecutar dotnet test backend/ArsDocendi.slnx, pnpm --filter frontend test:run, lint y build, pnpm format:check y pnpm exec openspec validate --all --strict; registrar cualquier bloqueo del entorno y comprobar que los cambios locales ajenos se preservaron.
- [x] 6.5 Realizar recorrido integrado con seed nuevo: Jefe crea/envía, Coordinador devuelve, propietario corrige las tres horas/reenvía, revisores aprueban y rol habilitado exporta; verificar número, hora/orden, categoría libre y ambas hojas del Excel sin duplicar continuidad.
