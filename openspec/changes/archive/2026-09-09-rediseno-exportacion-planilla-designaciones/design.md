## Context

La ruta `GET /api/designaciones/periodos/{id}/lote.xlsx` ya valida el ámbito,
lee el período activo y genera un XLSX mediante `ZipArchive` y XML escrito a
mano. Hoy produce `Pedidos finalizados` y `Designaciones resultantes`; el
archivo de referencia provisto en `docs/business-rules/` (`planilla_designaciones.xlsx`) es un
XLSX OOXML con tres hojas, estilos, anchos y encabezados combinados.

Los cargos y dedicaciones anteriores de un cambio o una baja están disponibles
en `Pedido.Snapshot`. Las continuidades y el estado posterior a una aprobación
se representan en `Designacion` vigente. Identity ya expone CUIL, fecha de
nacimiento y teléfono en `Persona`; el correo está en el perfil de Portal y
`IPortalQueries` es su frontera pública existente.

## Goals / Non-Goals

**Goals:**

- Entregar el modelo institucional como XLSX válido, con sus tres hojas y
  formato visual conservado.
- Proyectar altas, bajas, cambios, continuidades y designaciones administrativas
  sin modificar el estado de negocio.
- Usar el período activo y el anterior de forma determinista en los encabezados.
- Mantener tipado de fechas/números, textos literales, privacidad y la lectura
  consistente ya exigida por la exportación.

**Non-Goals:**

- No cambiar la ruta, permisos, filtros de la UI ni el contrato HTTP de descarga.
- No agregar tablas, columnas de base ni nuevos campos de dominio.
- No completar datos que el sistema no posee: departamento, cargo/dedicación
  externa y la pregunta de dedicación completa quedarán vacíos.
- No crear un motor genérico de reportes ni incorporar una biblioteca de Excel.
- No exportar UUIDs, adjuntos, credenciales ni información de otros ámbitos.

## Decisions

### 1. Usar el XLSX provisto como plantilla

El archivo de referencia se incorporará como recurso embebido de
`Modules.Designaciones`, dentro de una ubicación de recursos del proyecto. El
escritor abrirá una copia con `ZipArchive` y reemplazará sólo el workbook y las
tres hojas de datos, conservando del modelo los estilos, tema, anchos,
encabezados combinados, márgenes y relaciones.

Las filas se construirán a partir de las filas de datos del modelo, replicando
sus atributos de estilo cuando haya más filas que las preformateadas. No se
mantendrán filas placeholder sin datos. Los nombres de las pestañas serán los
basales del modelo (`PROPUESTA COMPLETA`, `ALTAS` y `BAJAS`); los períodos
dinámicos vivirán en los encabezados para no depender del límite de 31
caracteres de Excel.

Alternativa descartada: extender el XML minimalista actual con estilos propios.
Reduciría el código inmediato, pero no conservaría fielmente el modelo que el
usuario pidió.

### 2. Resolver períodos dentro de la lectura consistente

El repositorio devolverá junto con el período solicitado el período anterior,
seleccionado como el registro con mayor `ImpactoDesde` estrictamente menor al
del período activo, con desempate estable por ID. La consulta ocurrirá dentro
de la misma transacción `RepeatableRead` que pedidos y designaciones.

La proyección recibirá ambos períodos. El nombre configurado se conservará tal
cual para los encabezados; si no hay período anterior se emitirá sólo el texto
base de la columna D de la primera hoja, sin arrastrar el valor del archivo
modelo.

### 3. Proyectar las tres hojas desde el estado materializado

La consulta de lote conservará los pedidos del período y las designaciones
vigentes. El servicio derivará las filas así:

```text
designaciones vigentes --------------------+
                                           +--> PROPUESTA COMPLETA
pedidos Baja en_lote ----------------------+       D = anterior
                                                   E = propuesta vigente

pedidos Alta en_lote ---------------------> ALTAS
pedidos Baja en_lote ---------------------> BAJAS
```

- `PROPUESTA COMPLETA` tendrá una fila por persona y materia vigente, baja
  aprobada o Alta aprobada aún no materializada. Para una continuidad o carga
  administrativa, D y E repetirán el valor vigente. Para un Cambio, D usará el
  snapshot y E la designación vigente materializada. Para un Alta, D quedará
  vacío y E usará la designación vigente o, si todavía no existe, el
  cargo/dedicación solicitado. Para una Baja, D usará el snapshot y E quedará
  vacío.
- La materia y sus horas se tomarán de la designación vigente; en un Alta aún
  no materializada, del pedido; y en una Baja, del pedido/snapshot. La
  observación usará la justificación cuando exista y, en bajas, conservará
  también el tipo y detalle de baja.
- Los pedidos Alta y Baja se separarán por novedad sólo cuando estén en
  `en_lote` y pertenezcan al período activo. El número de fila del modelo será
  secuencial y los datos se ordenarán por persona, materia y número de pedido
  para que descargas repetidas sean estables.
- Cargo y dedicación se presentarán en una única celda con el formato textual
  del modelo, conservando cada componente histórico y dejando vacío el que no
  exista. CUIL se tomará de `Persona.Cuil`, sin usar el documento como reemplazo.
- En `ALTAS`, fecha de nacimiento y teléfono vendrán de Identity. El correo se
  resolverá con `IPortalQueries.ObtenerPerfilAsync` sólo para las personas de
  esas filas y se copiará únicamente el campo `Contacto.Mail`.
- Las columnas del modelo para departamento, cargo/dedicación departamental,
  horas destinadas al ingreso y dedicación completa se emitirán vacías porque
  no tienen una fuente persistida equivalente. No se reinterpretará
  `HorasExternas` como horas destinadas al ingreso.

Alternativa descartada: reutilizar `Designaciones resultantes` como una cuarta
hoja o conservar `Pedidos finalizados`. El modelo institucional reemplaza esa
organización; los pedidos siguen siendo la fuente para construir ALTAS/BAJAS y
para la trazabilidad, no una hoja técnica adicional.

### 4. Incorporar Portal sólo por su contrato público

`Modules.Designaciones` agregará una referencia a
`Modules.Portal.Contracts` e inyectará `IPortalQueries`. No se accederá al
`PortalDbContext`, a entidades internas ni a la API HTTP de Portal. El Host ya
compone ambos módulos, por lo que el nuevo edge
`Modules.Designaciones -> Modules.Portal.Contracts` mantiene el DAG.

Se usará la consulta existente por persona para evitar ampliar el contrato con
un método bulk que sólo tendría un consumidor. La cantidad esperable de altas
es acotada; si el volumen real vuelve costosa la consulta secuencial, el
siguiente paso será agregar una consulta batch explícita al contrato.

### 5. Mantener seguridad y tipos del XLSX

Las celdas textuales se escribirán como texto literal y escapado, incluyendo
CUIL, correo, motivos y nombres que comiencen con caracteres de fórmula. Las
fechas y horas se escribirán como números serializados con los estilos
numéricos del modelo; los números de horas conservarán tipo numérico. Las
pruebas inspeccionarán las partes OOXML, las hojas, los encabezados, los estilos
de celda y los valores, además de abrir el resultado con un lector compatible
cuando el entorno lo permita.

## Risks / Trade-offs

- **El archivo modelo cambia de ubicación o formato** -> verificar su firma
  OOXML, incorporarlo como recurso versionado y agregar un test de partes
  mínimas/hojas esperadas.
- **Hay más filas que las preformateadas** -> clonar la fila de datos del modelo
  y actualizar la dimensión de cada hoja; no truncar información.
- **Correo ausente o perfil inexistente** -> dejar la celda vacía y no hacer
  fallar toda la exportación.
- **Una consulta de Portal por alta** -> mantener el uso mínimo del contrato;
  incorporar batch sólo si una medición demuestra que el volumen lo requiere.
- **Período anterior inexistente o con fechas atípicas** -> ordenar por
  `ImpactoDesde` e ID y eliminar el texto estático del modelo cuando no haya
  candidato.
- **Cambio o baja sin snapshot histórico** -> conservar la celda anterior
  vacía; nunca inferirla de otra materia o de una fila no relacionada.

## Migration Plan

1. Incorporar el recurso XLSX y actualizar el escritor concreto para producir
   las tres hojas, sin modificar schema ni datos.
2. Agregar la referencia a `Modules.Portal.Contracts`, registrar/validar la
   resolución de `IPortalQueries` y actualizar `dependency-graph.md` y el
   dominio de Designaciones.
3. Extender los tests de exportación con períodos anterior/activo, Alta, Baja,
   Cambio, continuidad, múltiples materias, datos faltantes y correo de Portal.
4. Actualizar `api-contracts-designaciones.md` y la spec vigente para describir
   las tres hojas y las fuentes de datos.
5. Desplegar backend y recurso como una misma versión. El rollback es volver a
   la versión anterior del backend, que conserva el endpoint y genera el libro
   anterior; no requiere migración ni reversión de datos.
