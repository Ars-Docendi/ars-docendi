---
status: draft
owner: "Administración del sistema"
feature: "openspec/specs/administracion-sistema/spec.md"
last_updated: 2026-09-24
---

# Design spec: Administración del Sistema

## Resumen

La experiencia de Administración del Sistema permite al personal autorizado revisar la disponibilidad actual de los componentes y consultar eventos de auditoría. Prioriza diagnóstico rápido, trazabilidad comprensible, privacidad y consistencia con la interfaz institucional existente.

## Roles que ven esta surface

- [x] Administrativos

El acceso efectivo se controla por `sistema.estado.ver` y `auditoria.ver`; la especificación visual no altera esos permisos.

## Flujo principal

1. La persona abre el dashboard y distingue el estado de cada módulo y de PostgreSQL, incluidos resultados parciales.
2. Si hace falta, actualiza las comprobaciones mediante una acción secundaria; durante la consulta el control indica actividad y queda deshabilitado.
3. Desde la navegación autorizada abre los registros de auditoría, busca por rango, operación, módulo, tabla, nombre del actor o clave de fila y pagina los resultados.
4. Identifica cada evento por fecha, usuario, acción, módulo y resumen; expande el detalle para ver la clave, el request ID y únicamente valores aprobados por la política de redacción.

## Layout / IA

- Ambas pantallas reutilizan `PageHeader`, el contenedor y la escala tipográfica del shell institucional; no introducen un sistema visual paralelo.
- El dashboard presenta una nota breve que diferencia pings HTTP de la comprobación de PostgreSQL y una tabla compacta con componente, estado, duración y última comprobación. Cada comprobación es independiente; no usar un indicador global que oculte fallas parciales.
- El botón **Actualizar** y las acciones de reintento son secundarias (`Button` `secondary` o `ghost`). La búsqueda del panel de filtros es la acción primaria de esa consulta; las acciones de actualización de datos nunca usan énfasis primario.
- La auditoría prioriza en escritorio las columnas **Fecha**, **Usuario**, **Acción**, **Módulo** y **Cambio**. Objeto/tabla, clave de fila, request ID y detalle se presentan como contexto secundario sin ocultar que la consulta es de solo lectura.
- Los filtros se agrupan en un panel compacto con etiquetas persistentes. La búsqueda del actor acepta texto legible y no exige conocer un UUID.
- La acción **Buscar** es primaria dentro del formulario de consulta; **Actualizar**, **Reintentar**, **Limpiar** y paginación son secundarias o ghost según su énfasis.
- En pantallas estrechas, filtros fluyen a una columna y la tabla conserva encabezados y permite desplazamiento horizontal accesible; no se comprimen datos ni se ocultan silenciosamente campos esenciales.

## Estados a diseñar

| Estado            | Descripción                                                                                                | Cuándo se muestra                      |
| ----------------- | ---------------------------------------------------------------------------------------------------------- | -------------------------------------- |
| Loading           | Mensaje de estado anunciado; en refetch se conserva el contenido previo y se indica actividad en la acción | Carga inicial o actualización          |
| Empty             | Mensaje claro sin resultados para la consulta; paginación deshabilitada                                    | Auditoría sin eventos coincidentes     |
| Error             | Mensaje accionable sin excepción interna y acción secundaria para reintentar                               | Fallo de consulta                      |
| Success           | Tabla o estados independientes con hora, duración y metadatos comprensibles                                | Respuesta válida                       |
| Partial / unknown | Cada componente muestra disponible, no disponible o desconocido sin inferir salud de los demás             | Ping, DB o timestamp faltante/inválido |
| Awaiting approval | No aplica: estas pantallas son de consulta y no forman un flujo de aprobación                              | Nunca                                  |

## Decisiones de diseño

- Usar controles nativos o componentes compartidos con nombre accesible, foco visible, estados disabled/loading y áreas de interacción adecuadas.
- Mantener idioma español institucional y traducir las acciones de auditoría sin confundir `UPDATE` con una eliminación física.
- Mostrar nombre de persona cuando esté vinculado y, si no, el nombre visible de la cuenta; ante ausencia de cuenta, mostrar **Actor no identificado**.
- Presentar el módulo mediante una etiqueta legible para schemas conocidos y un fallback explícito para los demás.
- Construir el resumen desde la metadata histórica y campos redactados; no consultar la fila vigente ni enviar snapshots crudos, UPN, correo o IP.
- Reutilizar variables/tokens y los estilos del componente `Button`; no fijar colores o radios locales para controles compartidos.

## Anti-patterns a evitar (específicos de esta feature)

- Botón primario azul/redondeado para una acción de actualización rutinaria.
- Mostrar UUIDs, nombres de schemas o acciones SQL como jerarquía principal orientada al usuario.
- Indicador agregado que convierta un resultado parcial en estado global engañoso.
- Mostrar JSON de snapshots, datos personales, secretos, UPN/correo o IP.
- Resumir un `UPDATE` como una baja sin regla que lo sustente.
- Ocultar controles esenciales en móvil, quitar el foco visible o usar colores como único indicador de estado.

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- [`docs/product/designs/administracion-usuarios-docentes-design-spec.md`](administracion-usuarios-docentes-design-spec.md)
- [`docs/product/designs/administracion-roles-permisos-design-spec.md`](administracion-roles-permisos-design-spec.md)
- Spec funcional: [`openspec/specs/administracion-sistema/spec.md`](../../../openspec/specs/administracion-sistema/spec.md)

## Open questions de diseño

- No quedan decisiones de interacción pendientes para este alcance; las etiquetas de salida se mantienen en el mapeo del backend y el cliente normaliza las etiquetas conocidas al schema usado por el filtro, conservando la entrada desconocida para fallback.
