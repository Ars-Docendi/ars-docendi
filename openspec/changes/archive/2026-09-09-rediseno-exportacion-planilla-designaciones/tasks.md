## 1. Plantilla y frontera de dependencias

- [x] 1.1 Incorporar `docs/business-rules/planilla_designaciones.xlsx` como recurso embebido de `Modules.Designaciones` y verificar con el build que el archivo OOXML queda incluido en el assembly.
- [x] 1.2 Agregar la referencia de `Modules.Designaciones` a `Modules.Portal.Contracts`, resolver `IPortalQueries` por DI sin acceder a internals de Portal y verificar `dotnet build backend/ArsDocendi.slnx` junto con el guard de arquitectura.

## 2. Consulta y proyección del lote

- [x] 2.1 Extender la lectura consistente del lote para obtener el período anterior por `ImpactoDesde` e ID, y verificar con casos con y sin período anterior que no se use el texto estático de la plantilla.
- [x] 2.2 Agregar fixtures de exportación para continuidad, Cambio, Alta y Baja aprobados, múltiples materias y pedidos de otros estados/períodos; verificar que las filas se ordenen de forma estable y no se duplique persona/materia.
- [x] 2.3 Implementar la proyección de `PROPUESTA COMPLETA`, usando snapshot para el cargo/dedicación anterior y designaciones vigentes para la propuesta, y verificar que una Baja no reabra ni cree una designación.
- [x] 2.4 Implementar las proyecciones separadas de `ALTAS` y `BAJAS`, verificando que sólo incluyan pedidos `en_lote` del período activo y que las bajas conserven tipo, detalle y justificación como motivo.
- [x] 2.5 Completar datos de persona desde Identity y correo desde `IPortalQueries` sólo para Altas, verificando fecha de nacimiento, CUIL, teléfono, correo ausente y celdas sin fuente equivalente vacías.

## 3. Generación del XLSX institucional

- [x] 3.1 Reemplazar el escritor de dos hojas por la plantilla de tres hojas, conservando estilos, anchos, márgenes y encabezados combinados, y verificar las partes OOXML y los nombres `PROPUESTA COMPLETA`, `ALTAS` y `BAJAS`.
- [x] 3.2 Parametrizar los encabezados con el período activo y el anterior en la columna D de la primera hoja, incluyendo el caso sin período anterior, y verificar que no queden referencias fijas del modelo.
- [x] 3.3 Escribir filas dinámicas replicando el estilo de datos cuando se supere la cantidad preformateada, actualizar las dimensiones y verificar que no se exporten filas placeholder ni se trunque información.
- [x] 3.4 Mantener celdas textuales literales y escapadas, incluyendo CUIL, correo y textos con apariencia de fórmula, y conservar fechas y horas como números tipados con los formatos del modelo; verificar ceros iniciales, valores desconocidos y fechas.
- [x] 3.5 Actualizar `ExportacionLoteTests.cs` para verificar el libro completo, sus tres hojas, encabezados, contenido por novedad, tipos de celda, ausencia de UUIDs y ausencia de mutaciones en pedidos/designaciones.

## 4. Documentación y arquitectura

- [x] 4.1 Actualizar `docs/architecture/api-contracts-designaciones.md` con la respuesta XLSX de tres hojas, sus filtros de contenido y las fuentes de datos, y verificar coherencia con la spec delta.
- [x] 4.2 Actualizar `docs/architecture/domains/designaciones.md` y `docs/architecture/dependency-graph.md` con el período anterior, la proyección institucional y el edge `Modules.Designaciones -> Modules.Portal.Contracts`, verificando que el DAG no tenga ciclos.
- [x] 4.3 Actualizar la documentación de la exportación vigente y el mapeo de tests sin agregar cambios de schema ni modificar la design spec de UX, y verificar que no queden referencias a las dos hojas anteriores.

## 5. Verificación integrada

- [x] 5.1 Ejecutar `dotnet test backend/ArsDocendi.slnx` y verificar autorización, período activo, lectura consistente, continuidad, Altas, Bajas, Cambios y datos faltantes.
- [x] 5.2 Ejecutar `pnpm format:check`, `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build` y `pnpm exec openspec validate --all --strict`; registrar cualquier check bloqueado por el entorno.
- [x] 5.3 Verificar que el XLSX generado abre en un lector de hojas de cálculo disponible y que el rollback a la versión anterior restaura el libro de dos hojas sin migración de datos.
