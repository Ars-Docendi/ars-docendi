## 1. Alineación de alcance y contratos

- [x] 1.1 Inventariar todas las fuentes de registros mock alcanzables desde `frontend/src` y clasificarlas como dato persistido, preferencia de sesión, constante presentacional o fixture exclusiva de test.
- [x] 1.2 Reconciliar los cambios activos `admin-docentes` y `roles-membresia` con este cambio, conservando su UI terminada pero eliminando de su alcance final los stores en memoria y las premisas frontend-only.
- [x] 1.3 Definir y documentar los DTOs, rutas, permisos, códigos Problem Details e idempotencia de `/api/administracion`, `/api/designaciones` y `/api/desarrollo` antes de implementar consumidores.
- [x] 1.4 Definir factories de query keys y convenciones de adapters HTTP por feature, incluyendo los estados carga, vacío, error y reintento.

## 2. Seguridad y soporte HTTP del backend

- [x] 2.1 Agregar pruebas de integración que fijen el formato Problem Details y el mapeo esperado para 400, 401, 403, 404, 409 y 422.
- [x] 2.2 Implementar el mapeo central de errores con `traceId`, códigos estables y errores por campo, usando logging estructurado sin exponer detalles sensibles.
- [x] 2.3 Agregar pruebas de arquitectura para asegurar Controller → Service → Repository, escritura exclusiva administrativa de `identity` y ausencia de referencias a internals entre módulos.
- [x] 2.4 Incorporar soporte y pruebas de `Idempotency-Key` para comandos de Designaciones que mutan transiciones, incluida la repetición concurrente.

## 3. Dataset sintético no productivo

- [x] 3.1 Crear una prueba automatizada que ejecute el seed sobre PostgreSQL migrado y verifique que producción y una fuente productiva son rechazadas antes de escribir.
- [x] 3.2 Definir UUIDs reservados y una matriz de fixtures que relacione personas, usuarios, roles, permisos, carreras, materias, cargos, designaciones, períodos y pedidos.
- [x] 3.3 Ampliar `sintetico.sql` con transacción, advisory lock, metadata versionada y upserts de catálogos, personas, cuentas, roles, permisos y ámbitos de `identity`.
- [x] 3.4 Incorporar upserts de cargos, períodos, designaciones vigentes, pedidos, adjuntos e historial de `designaciones`, respetando estados y reglas existentes.
- [x] 3.5 Agregar verificaciones automáticas de cobertura para todos los roles de sistema, ámbitos, estado activo/inactivo, persona sin cuenta y cada estado soportado de pedido.
- [x] 3.6 Ejecutar el seed dos veces y verificar ausencia de duplicados, restauración de las filas fixture, preservación de filas ajenas e integridad referencial.

## 4. API de administración de usuarios e identidad

- [x] 4.1 Agregar pruebas de repositorio para listar/detallar usuarios con persona, estado, roles y ámbitos sin tracking accidental ni consultas N+1.
- [x] 4.2 Implementar repositorios y servicios de consulta administrativa de usuarios y catálogos sobre `IdentityDbContext`.
- [x] 4.3 Agregar pruebas de servicio para alta y edición atómica, UPN/documento duplicados, ámbitos incompatibles y ausencia de escrituras parciales.
- [x] 4.4 Implementar alta y edición de persona, cuenta y asignaciones de rol mediante la superficie exclusiva de administración.
- [x] 4.5 Agregar pruebas para activar/desactivar usuarios, incluyendo denegación posterior de acceso y conflictos de concurrencia.
- [x] 4.6 Implementar activación/desactivación y publicar controllers de `/api/administracion/usuarios` con autorización por permisos.

## 5. API de roles y membresías de permisos

- [x] 5.1 Agregar pruebas de listado, creación y edición de roles, incluyendo copia one-shot de permisos desde un rol base y protección de roles de sistema.
- [x] 5.2 Implementar repositorio, servicio y endpoints de `/api/administracion/roles` sin permitir mutaciones protegidas.
- [x] 5.3 Agregar pruebas de reemplazo atómico de permisos con IDs inválidos, duplicados y concurrencia.
- [x] 5.4 Implementar consulta del catálogo de permisos y reemplazo de `/api/administracion/roles/{id}/permisos`.

## 6. API administrativa de docentes

- [x] 6.1 Definir en `Modules.Designaciones.Contracts` el contrato puro para consultar y reemplazar designaciones vigentes desde la superficie administrativa.
- [x] 6.2 Agregar pruebas cross-context de alta y edición docente para persona nueva o existente, rol docente, materias, cargos y horas, incluyendo rollback completo ante error.
- [x] 6.3 Implementar el comando público de Designaciones para mantener asignaciones vigentes sin exponer entidades ni internals del módulo.
- [x] 6.4 Implementar repositorio, servicio orquestador y endpoints `/api/administracion/docentes`, coordinando `identity` con el contrato público de Designaciones.
- [x] 6.5 Agregar pruebas de listado y filtros docentes con múltiples designaciones, personas sin cuenta y usuarios activos/inactivos.

## 7. API de períodos, catálogos y pedidos

- [x] 7.1 Agregar pruebas de servicio y endpoint para listar, crear, editar, activar, desactivar y eliminar períodos, cubriendo período activo único y referencias existentes.
- [x] 7.2 Implementar repositorio, servicio y controller de `/api/designaciones/periodos` con validación autoritativa y conflictos estables.
- [x] 7.3 Agregar pruebas de catálogos por actor para períodos activos, materias a cargo, personas elegibles y cargos activos.
- [x] 7.4 Implementar `/api/designaciones/catalogos` usando IDs canónicos y ámbitos resueltos desde `identity`.
- [x] 7.5 Agregar pruebas HTTP para crear, obtener, editar, enviar, reenviar y eliminar pedidos, incluyendo numeración, historial, autorización y atomicidad.
- [x] 7.6 Completar servicios/repositorios faltantes y publicar los endpoints de ciclo de vida de `/api/designaciones/pedidos`.

## 8. API del circuito de revisión

- [x] 8.1 Agregar pruebas HTTP de listados y detalle filtrados por materia, carrera y alcance global, incluyendo intentos de falsificar rol o ámbito desde el cliente.
- [x] 8.2 Implementar consultas de pedidos por actor y detalle autorizado sin aceptar `ActorContexto` como autoridad del request.
- [x] 8.3 Agregar pruebas de aceptar, rechazar, devolver, reenviar, priorizar y despriorizar para cada rol, etapa y condición de comentario.
- [x] 8.4 Publicar comandos de revisión que resuelvan `ICurrentUser`, ejecuten la máquina de estados y confirmen pedido e historial en una única transacción.
- [x] 8.5 Verificar materialización de la designación al completar la cadena y el comportamiento idempotente ante solicitudes repetidas.

## 9. Sesión basada en identidades sembradas para desarrollo

- [x] 9.1 Agregar pruebas de integración del handler dev para usuario activo, rol permitido, rol inventado, usuario inactivo y usuario no perteneciente al dataset sintético.
- [x] 9.2 Implementar el esquema por `X-Dev-User-Id` y `X-Dev-Role-Code`, validado contra `identity` y habilitado sólo mediante ambiente y opción explícita.
- [x] 9.3 Implementar `GET /api/desarrollo/identidades` con roles y ámbitos elegibles, sin exponer datos innecesarios.
- [x] 9.4 Agregar una prueba arrancando el Host como Production que confirme que controller, esquema y lectura de headers de desarrollo no están registrados.
- [x] 9.5 Adaptar el interceptor Axios y la UI de ingreso/cambio de rol para consultar identidades sembradas y guardar localmente sólo los IDs seleccionados.
- [x] 9.6 Reemplazar `mockUsers`, el usuario stub y el `ActorContexto` hardcodeado por el usuario actual resuelto mediante API, con estados de carga/error de sesión.

## 10. Migración frontend de usuarios, roles y permisos

- [x] 10.1 Crear adapters, hooks y pruebas HTTP de usuarios para listado, alta, edición y activación/desactivación.
- [x] 10.2 Migrar `/usuarios` a React Query, mapear Problem Details a formularios y cubrir carga, vacío, error, reintento e invalidación.
- [x] 10.3 Crear adapters, hooks y pruebas HTTP para roles, catálogo de permisos y reemplazo de membresías.
- [x] 10.4 Migrar `/roles` y `/membresia-roles`, eliminando `ConfiguracionContext` como fuente de estado remoto y guardando permisos sólo al confirmar.
- [x] 10.5 Eliminar imports runtime de los stores mock de usuarios, roles y membresías y trasladar los fixtures necesarios a directorios de tests.

## 11. Migración frontend de docentes

- [x] 11.1 Crear tipos, adapters, hooks y pruebas HTTP para listar, crear, editar, activar/desactivar docentes y consultar catálogos.
- [x] 11.2 Migrar `/docentes` a React Query preservando la UI existente y usando IDs canónicos para persona, rol, materia y cargo.
- [x] 11.3 Incorporar carga, vacío, error, reintento y errores de validación backend en tabla y modales de docentes.
- [x] 11.4 Eliminar los datos duplicados de personas, materias, cargos y docentes del store runtime, conservando sólo fixtures propias de tests.

## 12. Migración frontend de períodos y pedidos

- [x] 12.1 Crear adapters y hooks HTTP para períodos y catálogos de Designaciones con pruebas de consultas, mutaciones e invalidaciones.
- [x] 12.2 Migrar `/designaciones/periodos` y formularios de pedido para eliminar `periodosMock` y los catálogos de filas persistidas.
- [x] 12.3 Sustituir el cuerpo del seam `pedidosApi` por llamadas al backend, adaptando DTOs sin propagar Axios a hooks o componentes.
- [x] 12.4 Migrar “Mis pedidos”, detalle y tablero de revisión para que ámbito, acciones disponibles e historial provengan de respuestas autorizadas.
- [x] 12.5 Actualizar pruebas frontend para simular HTTP y cubrir happy path, devolución/reenviado, rechazo, prioridad, eliminación y errores concurrentes.
- [x] 12.6 Eliminar `pedidosStore`, `pedidosSeed`, la clave histórica de `localStorage` y la autoridad de transición del frontend; conservar sólo mapeos presentacionales necesarios.

## 13. Estados remotos y erradicación de mocks runtime

- [x] 13.1 Auditar cada pantalla migrada y agregar estados accesibles diferenciados de carga inicial, vacío, error recuperable y mutación pendiente.
- [x] 13.2 Agregar un chequeo estático que falle si runtime importa directorios `/mock/`, seeds de negocio o accede a claves eliminadas de `localStorage`.
- [x] 13.3 Verificar el bundle de producción para confirmar que no contiene datasets sintéticos, selector dev ni fuentes mock de registros.
- [x] 13.4 Ejecutar lint, typecheck y toda la suite frontend, corrigiendo fixtures/tests sin introducir fallback de datos en runtime.

## 14. Documentación y validación integral

- [x] 14.1 Actualizar `docs/architecture/api-contracts.md` con rutas, DTOs, permisos, idempotencia y Problem Details de todas las APIs nuevas.
- [x] 14.2 Actualizar `docs/architecture/dependency-graph.md` con la orquestación administrativa vía `Modules.Designaciones.Contracts` y comprobar que el grafo continúa acíclico.
- [x] 14.3 Actualizar la documentación de modelo de datos y dominios de identidad/designaciones con propiedad del seed, ámbitos, designaciones vigentes y fronteras de escritura.
- [x] 14.4 Actualizar documentación de infraestructura con ejecución, reejecución, versión y restricciones del seed no productivo, además del opt-in de autenticación dev.
- [x] 14.5 Ejecutar build y tests completos del backend, incluidos arquitectura e integración con PostgreSQL, verificando logs Serilog y tamaño razonable de archivos.
- [x] 14.6 Ejecutar recorridos end-to-end contra una base limpia sembrada para usuarios, roles/permisos, docentes, períodos, pedidos y toda la cadena de aprobación.
- [x] 14.7 Ejecutar `openspec validate datos-ejemplo-y-frontend-con-api --strict` y resolver cualquier inconsistencia antes de dar la implementación por terminada.
