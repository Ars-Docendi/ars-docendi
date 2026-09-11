## Context

Ver la motivación y el alcance en [proposal.md](proposal.md). La exploración confirmó:

- `api/pedidosApi.ts` sustituye las tres horas solicitadas por las del snapshot. `pedidosApi.test.ts` espera actualmente esa sustitución. `PedidoForm.tsx` reutiliza esos campos al editar.
- `ArmarSnapshotAsync` toma horas complementarias del pedido, no de la designación vigente. El catálogo frontend devuelve esas horas actuales como cero; la entidad vigente no las almacena.
- `SeccionDesignacionSolicitada.tsx` y `pedidoValidacion.ts` aplican la restricción de mejora; el backend admite las siete categorías de texto. El documento UX conserva la decisión anterior, ahora reemplazada por elección libre entre 1 y 6.
- La API ya ordena el historial por fecha; `detalleAdapters.ts` omite la hora. El encabezado del detalle usa `pedido.id`.
- `NAV_BY_ROLE` ya soporta sectores. `TablaRevision` usa Tabs, aunque la spec del tablero conserva texto viejo sobre secciones junto con escenarios posteriores de pestañas. Este cambio agrega filtro y hover sin reabrir el rediseño del tablero.
- `spin-up.sh` conserva bases existentes y levanta el servicio antes de migrar. `drop-db.sh` ya protege producción. El seed carece de eventos iniciales para varios pedidos y sólo asigna un jefe a una materia.
- Hay cambios locales previos en Sidebar y DetallePedidoPage que deben preservarse.

## Goals / Non-Goals

**Goals:** preservar historia y valores solicitados, centralizar la integridad de categorías, descargar un lote consistente y reconstruir ambientes descartables de forma reproducible.

**Non-Goals:** un ABM de dedicaciones, cambiar la cadena de aprobación, integrar Guaraní, agregar estados de exportación o un motor de reportes. La exportación es una consulta del estado persistido, no vuelve a aprobar ni materializar pedidos. El reinicio alcanza sólo despliegues efectivos de staging/PR; no fuerza despliegues para cambios documentales.

## Decisions

### 1. Catálogo con seis categorías seleccionables y compatibilidad histórica

Crear `designaciones.dedicaciones` siguiendo cargos: UUID, código único 1–6, nombre, orden y activo; adjuntar auditoría. Agregar FK `dedicacion_id` en designaciones y `dedicacion_solicitada_id` en pedidos. La API de catálogos entrega objetos y las mutaciones usan UUID; ningún cliente puede crear categorías con texto libre. Se eliminan los filtros y validaciones de mejora en todos los consumidores.

Migrar exactamente los textos Categoría 1 a 6. Los textos anteriores fuera de esa escala, incluida Categoría 0, se conservan sólo como legado de lectura en las columnas anteriores y snapshots; no se recategorizan, no se insertan como séptima opción. Nuevas Altas/Cambios exigen FK válida y activa. Una Baja o una continuidad puede conservar la dedicación histórica sin seleccionar otra. Las nuevas escrituras administrativas también validan el catálogo; un registro legado sin modificación de dedicación debe poder conservar su valor.

La integridad de nuevas selecciones se impone en backend y base, distinguiendo filas legadas de nuevas mediante SQL versionado, sin permitir que una actualización modifique el texto legado ni use ese camino para ingresar valores arbitrarios. El adaptador público de administración en `Modules.Designaciones.Contracts/Administracion` y sus consumidores Host/frontend se actualizan en conjunto. Los DTOs de lectura conservan nombre histórico además de ID opcional. No agregar interfaz interna con una implementación ni ABM.

Alternativa descartada: ampliar la lista de strings; no satisface el catálogo persistido y deja divergencias entre pantallas.

### 2. Separar snapshot y solicitud en todo el recorrido

Los campos `horas`, `horasInvestigacion` y `horasExternas` del modelo editable provienen siempre de los campos del pedido. Un objeto histórico independiente alimenta el panel de valores anteriores. El snapshot nunca inicializa los campos solicitados ni se modifica al guardar/reenviar. Un snapshot vacío de Alta indica ausencia de designación previa; no convierte las horas pedidas en cero.

Para que las horas complementarias actuales sean reales, agregar columnas nullable no negativas a la designación vigente, propagarlas por consultas/contratos y materializarlas con Alta/Cambio. Recuperar valores de pedidos de origen cuando existan; conservar NULL cuando el dato anterior sea desconocido, sin inventar ceros. El snapshot de nuevos envíos lee los valores vigentes. Los snapshots ya guardados se conservan por su valor histórico, aun si el código anterior los obtuvo incorrectamente.

El cambio es local a la persistencia existente de designaciones; no introduce un nuevo agregado de horas a nivel docente. Mantener una materia por pedido y no sumar indiscriminadamente horas de otras materias. Las horas de materia deben ser positivas cuando son solicitadas; investigación/externas pueden ser cero en uso real. Los ejemplos de este cambio usarán valores positivos para evidenciar diferencias.

### 3. Continuidad sin trámite y tratamiento de registros anteriores

Retirar `Sin novedad` del catálogo, default del formulario, validadores y nuevas mutaciones. La selección inicial del tipo será vacía y obligatoria. Eliminar la vía de aprobación de nuevos pedidos de ese tipo. Registros históricos se siguen leyendo con su texto original; un borrador o devuelto anterior debe cambiar a una novedad admitida antes de continuar, y cualquier envío/aceptación de un registro legado todavía en circuito se rechaza sin mutación. No borrar historial de producción. El seed nuevo no contiene ese tipo de pedido.

La continuidad se obtiene por persona y materia, no simplemente por persona: una modificación sobre una materia no borra sus otras designaciones. Pedidos rechazados, cancelados o pendientes no alteran los valores vigentes.

### 4. Exportación de consulta por período configurado

Agregar `GET /api/designaciones/periodos/{periodoId}/lote.xlsx` bajo Controller → Service → Repository. La UI toma el período activo configurado en Períodos de Designación y lo muestra junto al botón. Backend verifica que el identificador exista y siga activo; si cambió, responde conflicto para refrescar el período. Sin período activo el botón queda deshabilitado con explicación. Los filtros de nombre, estado, carrera y período de la tabla no recortan el lote: el botón explicita el período configurado que exportará.

Autorizar exclusivamente roles de Decanato, Secretaría y Administrativo según identidad persistida y ámbito departamental. Descargar no confiere permisos de aprobar. Nunca confiar en filas o roles enviados por frontend. Leer en una misma transacción consistente pedidos, período y designaciones para evitar un Excel mezclado con una aprobación concurrente.

Dos hojas, como se propuso durante la exploración:

| Hoja                      | Contenido                                                                                                                                                                                                                                                                                                                                                       |
| ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Pedidos finalizados       | Todos los pedidos del ámbito con `periodo_id` igual al período configurado y estado `en_lote`: número, período, docente, legajo, carrera, materia, novedad, cargo/dedicación solicitados, horas de materia/investigación/externas, solicitante, inicio del circuito y aprobación final.                                                                         |
| Designaciones resultantes | Una fila por persona/materia vigente del ámbito al descargar: período destino y rango de impacto configurados, docente, legajo, carrera, materia, cargo, dedicación, las tres horas y número del pedido del período que originó el cambio cuando corresponda; de lo contrario, continuidad. Las bajas constan en la primera hoja y no se reabren en la segunda. |

La materialización ya ocurre al aceptar en Decanato: leer el estado resultante evita aplicar el cambio dos veces. El período es el destino del lote, no una reconstrucción retrospectiva ni una fecha calculada desde el reloj. Registrar fecha/hora de generación y período en el archivo. Exportar sin pedidos aprobados sigue siendo válido: primera hoja sólo con encabezados y segunda con continuidades. No marcar pedidos como exportados ni crear número ficticio para una continuidad.

Generar XLSX real con dos hojas, números y fechas tipados y cadenas como celdas de texto (incluyendo cadenas que comienzan con `=`, `+`, `-` o `@`). No renombrar CSV como Excel. No hay dependencia de Excel instalada: para este archivo tabular fijo usar XML y ZIP de la biblioteca estándar, sin construir una abstracción genérica; validar estructura y apertura en un lector real durante implementación. Si su complejidad excede el escritor concreto de dos hojas, justificar una biblioteca dedicada en el mismo diff antes de incorporarla.

### 5. Presentación y navegación

- Historial: ordenar por instante ascendente en API, conservar orden en UI y representar `dd/MM/yyyy HH:mm` en `America/Argentina/Buenos_Aires` con Intl. El orden no depende de la cadena formateada; empates mantienen un criterio estable por ID.
- Número: encabezados, títulos, modales, mensajes y nombres de descarga usan `numero`. Los UUID siguen en claves técnicas y rutas, nunca como etiqueta visible del pedido.
- Sidebar: modificar grupos de `NAV_BY_ROLE` para ofrecer DESIGNACIONES con Mis pedidos, Revisión y Períodos según permisos actuales. Mantener navegación accesible también contraída; quitar el padre redundante sin introducir rutas nuevas.
- Filtro Estado: reutilizar FiltrosLista, con Todos y estados presentes en revisión; aplicar antes de contar pestañas. No confundir estado con área de devolución ni prioridad.
- Hover: override acotado a `.adoc-revision` con los tokens verdes existentes, preservando borde inferior y foco por teclado. Inspeccionar estilos computados para reproducir la desaparición reportada; no editar node_modules ni alterar todas las Tabs del producto.

### 6. Reconstrucción no productiva y seed verificable

Reutilizar validaciones de ambiente y `drop-db.sh` dentro de `spin-up.sh`. Validar parámetros y disponer de las imágenes antes de comenzar la operación destructiva. Para staging/PR: detener el backend anterior, eliminar sólo su base, aprovisionar, ejecutar migraciones en contenedor transitorio, sembrar, y finalmente iniciar los servicios. Ante falla intermedia, no publicar la app con schema/seed parcial. Serializar ejecuciones por ambiente durante la sección de reinicio, incluso con cancelaciones/reintentos del workflow de PR. Producción conserva su base y nunca ejecuta el reset ni el seed sintético.

Estado cero significa base nueva más catálogos y ejemplos sintéticos, sin modificaciones dejadas por sesiones anteriores. La reconstrucción incluye numeración, idempotencia, auditoría, identidades y metadata de la base descartable.

El seed tendrá seis categorías; cargas positivas y distintas entre snapshot y solicitud; evento crear de todos los pedidos y enviar de todos los que salieron del borrador; cadena completa hasta cada estado. La fecha de creación inicia el borrador y el primer envío inicia la revisión (semántica existente de Inicio). No agregar un campo fecha_inicio redundante. Los autores tendrán rol de Jefe sobre la materia, y el docente estará asociado a ella; en Alta es una incorporación propuesta, no una designación vigente previa artificial. Sembrar jefes/ámbitos adicionales cuando las materias lo requieran. Mantener UUIDs estables, datos inventados, cobertura de roles/estados e idempotencia del seed aislado.

## Risks / Trade-offs

- Datos legados fuera de catálogo → lectura fiel y migración exacta; exigir categoría válida cuando se cambie esa dedicación.
- Horas históricas desconocidas → NULL visible como dato ausente, nunca inventado; completar fixtures con valores reales sintéticos.
- Descarga de todo el ámbito pese a filtros visuales → mostrar explícitamente el período del lote y comprobar autorización en servidor.
- XLSX generado con herramientas estándar → test de partes, relaciones, tipos y contenido más apertura manual; mantener formato simple.
- Reset interrumpido → servicio detenido hasta completar migración/seed; reejecución reconstruye el ambiente, con exclusión mutua por ambiente.
- Specs/documentos anteriores contradictorios → deltas sustituyen explícitamente Sin novedad y categorías; actualizar diseño UX y referencias tocadas sin rediseñar funcionalidades ajenas.

## Migration Plan

1. Incorporar migraciones aditivas de catálogo, FK y horas, con auditoría y preservación de legados. Probar desde base vacía y desde schema anterior con categorías 0/1/6, snapshots y pedidos Sin novedad históricos.
2. Actualizar todos los consumidores de contratos, validaciones, formularios y materialización junto con documentación. No desplegar backend/frontend incompatibles por separado.
3. Actualizar seed y secuencia de despliegue; verificar dos ejecuciones sobre el mismo ambiente descartable con una fila extra entre ambas y un intento prohibido sobre prod.
4. Validar recorrido de creación, devolución, edición, aprobación y Excel en staging reconstruido.
5. Para bases persistentes, respaldar antes de migrar. Rollback mediante versión conjunta anterior y restauración del backup si hubo escrituras incompatibles. Para staging/PR, reconstruir con la versión anterior; los datos previos al reset son deliberadamente descartables.
