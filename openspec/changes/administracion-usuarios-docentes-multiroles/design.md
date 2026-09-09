## Contexto

`identity.user_roles` ya persiste una asignación por usuario, rol y ámbito, y su restricción de unicidad evita duplicados exactos. El problema está en la traducción hacia la API y la UI: el contrato actual expone esas asignaciones como `roles`, la tabla las renderiza como nombres repetidos y el formulario vuelve a construir asignaciones usando etiquetas visibles. Además, el alta y edición de Docentes reciben roles y materias separados y pueden generar el producto cartesiano entre ambos.

El cambio cruza identidad, administración de Docentes y navegación del frontend. Debe conservar la separación de módulos: la identidad se consulta mediante `IConsultasIdentity`, y los datos de designaciones sólo se consumen mediante `Modules.Designaciones.Contracts`.

## Objetivos y no objetivos

### Objetivos

- Representar por separado el resumen de roles y las membresías completas `rol + ámbito`.
- Hacer atómica la edición de identidad y membresías, con errores distinguibles para concurrencia, unicidad, validación y ámbito.
- Permitir que una misma persona tenga roles docentes diferentes en materias diferentes.
- Hacer visible y navegable la relación entre una cuenta y su perfil docente, incluyendo docentes sin cuenta.
- Reutilizar la iconografía SVG existente y mejorar su semántica y legibilidad sin agregar dependencias.

### No objetivos

- No crear un booleano persistido `es_docente` ni duplicar la relación usuario-persona.
- No cambiar el modelo de designaciones académicas ni resolver en este cambio una regla normativa que todavía no tenga fuente institucional confirmada.
- No crear una capa de componentes o contratos genéricos para un solo consumidor.

## Decisiones

### Contrato de membresías

El DTO de lectura de usuario tendrá dos colecciones explícitas:

- `roles`: resumen único por `rolId`, con código y nombre para tablas, filtros y badges.
- `membresias`: todas las asignaciones activas, con `rolId`, código, nombre, ámbito, `materiaId` y `carreraId`.

El DTO de escritura reemplazará la colección ambigua de roles por `membresias`, cuyos elementos conservarán únicamente IDs canónicos (`rolId`, `materiaId`, `carreraId`). La UI podrá mostrar nombres, pero no los usará para identificar ni reconstruir una asignación. El cambio se despliega coordinadamente con backend y frontend; no se mantendrá un alias ambiguo después de migrar los consumidores existentes.

### Persistencia y consistencia

Se reutilizará `identity.user_roles` y su restricción existente. El servicio validará referencias, compatibilidad de ámbito y duplicados antes de reemplazar las membresías. La actualización de persona, cuenta y membresías se confirmará dentro de la transacción existente; ante cualquier error se conservará el estado anterior.

La versión seguirá siendo la de concurrencia optimista ya mapeada por EF/Npgsql. Una versión obsoleta responderá `409` con `concurrency-conflict`. Los conflictos de unicidad conservarán su código específico; las reglas de ámbito y duplicación de membresías responderán `422` con `identity-role-scope-conflict`. El frontend mostrará el mensaje según `problem.type`, no según el texto de una excepción.

### Roles docentes por materia

Las operaciones de Usuarios y Docentes aceptarán filas completas de membresía. Cada fila relacionará exactamente un rol con un ámbito, por ejemplo `Docente + Materia A` o `Jefe de Cátedra + Materia B`. Se elimina la combinación implícita de listas independientes de roles y materias. El backend volverá a validar la compatibilidad aunque el selector frontend la anticipe.

### Relación Usuario-Docente

El perfil docente se calculará a partir de membresías docentes activas y designaciones vigentes, y expondrá los identificadores necesarios para navegar entre ambas pantallas. No se agregará estado derivado persistido. Usuarios será la superficie de cuenta y membresías; Docentes seguirá siendo la superficie de persona, designaciones y estado de cuenta. Ambas mostrarán un vínculo explícito, sin crear registros al navegar.

La composición que requiera designaciones se hará desde la frontera pública de administración/contratos. Ningún módulo referenciará implementaciones internas de otro módulo.

### UI e iconografía

Se conservará el helper SVG inline existente y el estilo visual común del shell. La semántica será: personas agrupadas para Usuarios, educación para Docentes, protección/rol para Roles y protección validada para Membresía de Roles. Los enlaces conservarán texto o nombre accesible; los SVG serán decorativos cuando el enlace ya tenga etiqueta.

### Documentación y pruebas

La implementación actualizará el contrato de API, el design spec administrativo y las pruebas de regresión de backend/frontend. La coexistencia de roles por materia sólo se registrará como `BR-<modulo>-NNN` cuando se confirme su fuente normativa; hasta entonces queda como requisito funcional de esta propuesta.

## Riesgos y mitigaciones

- **Cambio de forma del DTO de usuario:** actualizar consumidores y contrato en el mismo cambio, y desplegar backend/frontend como conjunto compatible.
- **Formulario abierto con datos obsoletos:** enviar `version`, mantener el modal abierto ante `concurrency-conflict` y exigir recarga antes de reintentar.
- **Diferencia entre rol y designación:** calcular el indicador docente con ambas fuentes y conservar `TieneCuenta` para docentes sin usuario.
- **Regresión del alcance de autorización:** probar una acción dentro y fuera de la materia de cada membresía; el rol no se tratará como global.
- **Pérdida accidental de ámbitos al editar:** exigir la lista completa de membresías y validar que un error no confirme ningún dato parcial.

## Plan de migración

1. Agregar primero las pruebas de contrato, concurrencia, duplicados y roles mixtos.
2. Cambiar los DTOs y servicios backend, actualizar la documentación de API y ejecutar las pruebas de administración.
3. Actualizar adaptadores, formularios y tablas del frontend para usar `membresias` e IDs canónicos; agregar filtros, enlaces e iconos.
4. Verificar formato, specs y builds. No se requiere migración SQL ni modificación de datos existentes: las tres asignaciones de Gustavo Ruiz deben permanecer como tres ámbitos legítimos.
5. Si fuera necesario revertir, volver a la pareja de versiones anterior sin borrar filas de `identity.user_roles`.

## Preguntas abiertas

No hay decisiones bloqueantes para implementar la propuesta. La única confirmación posterior es la fuente normativa necesaria para convertir la regla de coexistencia de roles en una regla `BR-<modulo>-NNN`.
