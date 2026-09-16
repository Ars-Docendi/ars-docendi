## 1. Contrato y regresiones backend

- [x] 1.1 Agregar pruebas de regresión para el PUT de usuarios que cubran versión obsoleta `409 concurrency-conflict`, unicidad, membresía repetida, atomicidad y el caso `Docente + Materia A` junto con `Jefe de Cátedra + Materia B`; verificar que las pruebas nuevas fallen antes de cambiar la implementación.
- [x] 1.2 Agregar pruebas de contrato para que la consulta devuelva `roles` resumidos sin duplicados y `membresias` completas con IDs de rol, materia y carrera; verificar que Gustavo Ruiz conserve tres ámbitos y un solo rol resumido.
- [x] 1.3 Cambiar los DTOs y la documentación de `administracion-identidad-api` para separar resumen, membresías y payload de escritura; verificar compilación del backend y correspondencia con `docs/architecture/api-contracts-administracion.md`.

## 2. Persistencia y reglas de membresías

- [x] 2.1 Ajustar `ServicioUsuarios` y sus mapeos para conservar IDs canónicos, resumir roles por `rolId` y reemplazar membresías de forma atómica; verificar las regresiones de roles mixtos, duplicados y rollback ante error.
- [x] 2.2 Clasificar y mapear de forma estable conflictos de concurrencia, UPN, documento, legajo, validación y ámbito; verificar respuestas Problem Details con `type` y status esperados sin exponer excepciones internas.
- [x] 2.3 Revisar la autorización que consume membresías y probar que un `jefe_catedra` sólo habilite acciones dentro de su materia o carrera; verificar una acción permitida y otra denegada fuera de ámbito.
- [x] 2.4 Cambiar alta y edición de Docentes para recibir filas explícitas `rol + materia` y eliminar la combinación implícita de listas independientes; verificar con una prueba que roles distintos no se propaguen a materias ajenas.

## 3. Relación Usuarios-Docentes

- [x] 3.1 Agregar una prueba de integración o contrato para el indicador docente, `personaId`, cantidad de materias, vínculo de cuenta y docentes sin cuenta; verificar que el perfil se derive de membresías o designaciones sin persistir `es_docente`.
- [x] 3.2 Implementar la composición backend y los endpoints/acciones de navegación entre cuenta y ficha docente respetando `IConsultasIdentity` y `Modules.Designaciones.Contracts`; verificar que abrir el vínculo no cree registros ni acceda a implementaciones internas de otro módulo.

## 4. Frontend de Usuarios y Docentes

- [x] 4.1 Agregar pruebas del adaptador de usuarios que preserven `roles` resumidos, todas las `membresias` y sus IDs al editar; verificar que el caso de Gustavo no genere tres envíos idénticos.
- [x] 4.2 Actualizar modelos, consultas, payloads y manejo de `problem.type` para consumir el nuevo contrato y mantener abierto el modal ante conflictos de concurrencia; verificar errores accionables para `409` y `422`.
- [x] 4.3 Reemplazar los checkboxes/listas globales de roles por un editor de filas `rol + ámbito` en Usuarios y Docentes, con validación de duplicados y compatibilidad; verificar los escenarios de roles distintos por materia, edición atómica y campos obligatorios.
- [x] 4.4 Actualizar tablas y filtros de Usuarios y Docentes para mostrar roles únicos, resumen de ámbitos, indicador de perfil docente, estado de cuenta y enlaces cruzados; verificar docentes con cuenta, sin cuenta y filtros combinados.
- [x] 4.5 Sustituir los cuatro SVG del shell por iconos semánticamente distinguibles usando el helper existente, conservar etiquetas/tooltip accesibles y verificar legibilidad en sidebar expandido y colapsado.

## 5. Documentación y verificación final

- [x] 5.1 Actualizar `docs/architecture/api-contracts-administracion.md`, el design spec administrativo y cualquier documentación de datos afectada con el contrato de `roles`/`membresias`, navegación y errores; verificar que no queden referencias al payload global ambiguo.
- [x] 5.2 Ejecutar la verificación proporcional completa: `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check` y `pnpm exec openspec validate --all --strict`; registrar cualquier check bloqueado por el entorno.

> Verificación: `dotnet test backend/ArsDocendi.slnx` no pudo ejecutar tests mediante Microsoft.Testing.Platform en este entorno (IPC/named pipe y luego descubrimiento 0); el runner xUnit directo ejecutó el assembly completo: 138/138. El resto de los checks indicados pasó.
