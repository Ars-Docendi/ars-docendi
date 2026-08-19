## Context

Ver `proposal.md` para la motivación. Hoy PostgreSQL ya contiene el modelo de `identity`, `audit` y `designaciones`, pero `infra/scripts/seed-data/sintetico.sql` sólo escribe metadata. En el frontend, usuarios, docentes, roles, permisos, períodos, catálogos y pedidos tienen fuentes mock distintas; Designaciones encapsula parte de ellas detrás de funciones async y React Query, mientras las features administrativas mutan `useState` o un context compartido.

El backend ya posee entidades, repositorios y servicios parciales de Designaciones, además de `IConsultasIdentity` como frontera de lectura. Sin embargo, sólo publica endpoints `ping`. La autenticación real con Azure AD tampoco está conectada: el frontend selecciona identidades hardcodeadas y el backend no registra todavía un esquema de autenticación.

El cambio cruza infraestructura, persistencia transversal de identidad, el módulo Designaciones y varias features frontend. Debe conservar el DAG, la exclusividad de escritura administrativa sobre `identity`, Controller → Service → Repository y las reglas de dominio ya documentadas.

## Goals / Non-Goals

**Goals:**

- Definir un dataset único con identificadores estables y relaciones coherentes para todas las pantallas existentes.
- Publicar contratos HTTP completos y consistentes, con autorización y Problem Details.
- Hacer que las mutaciones y transiciones sean durables, atómicas y decididas por el backend.
- Migrar cada feature a un adapter HTTP y React Query sin acoplar componentes a Axios.
- Permitir recorrer roles y ámbitos sembrados en desarrollo sin incorporar una puerta de suplantación a producción.
- Poder verificar automáticamente que el runtime frontend ya no alcanza fuentes mock.

**Non-Goals:**

- Integrar Azure AD/MSAL; el adapter de identidad actual deberá poder ser sustituido por SSO sin cambiar los contratos de negocio.
- Copiar, anonimizar o derivar datos de producción.
- Convertir constantes presentacionales, etiquetas o reglas cerradas del dominio en tablas configurables.
- Crear APIs reales para Aulas, Portal o Tareas, cuyas features aún no contienen registros mock funcionales.
- Cambiar el modelo relacional salvo que la implementación descubra una restricción indispensable para los contratos aprobados.
- Diseñar una API pública para terceros; V1 sigue siendo una superficie interna frontend/backend.

## Decisions

### D1 — Seed SQL transaccional con IDs estables y propiedad explícita

`sintetico.sql` será un orquestador por secciones, en orden de dependencias: metadata → catálogos de identidad → personas/usuarios → roles/permisos/ámbitos → catálogos de Designaciones → períodos → designaciones vigentes → pedidos/adjuntos/historial. Usará UUIDs deterministas reservados para fixtures, una transacción y un advisory lock para evitar ejecuciones concurrentes.

Cada fila de fixture se insertará o actualizará por su clave estable. No se truncarán tablas ni se eliminarán filas creadas por desarrolladores. Reejecutar el seed restaura los valores declarados de las filas que le pertenecen y deja intactos los demás registros. La metadata registrará origen sintético, versión del dataset y fecha de última ejecución.

**Alternativas descartadas:** `HasData` de EF Core mezcla fixtures de ambiente con migraciones productivas; generar UUIDs al ejecutar rompe referencias y reproducibilidad; `TRUNCATE` borra trabajo local y hace riesgosa una tarea destinada a ambientes compartidos.

### D2 — Un solo universo narrativo con matriz de cobertura

El seed reutilizará las identidades y relaciones que hoy aparecen duplicadas en `mockUsers`, usuarios, docentes y pedidos. Incluirá como mínimo:

- una identidad activa por cada rol de sistema y una identidad multirol;
- un usuario inactivo, una persona sin cuenta y roles personalizados editables;
- dos carreras y varias materias para probar límites de ámbito;
- cargos y designaciones vigentes con combinaciones docente/materia diferentes;
- un período activo y períodos históricos inactivos;
- pedidos en cada estado soportado, incluyendo prioritario, devuelto y rechazado con historial consistente.

Los casos de lista vacía, errores o combinaciones artificiales se cubrirán mediante bases aisladas o simulación HTTP en tests, no agregando incoherencias al dataset compartido.

### D3 — Superficie administrativa en Host, persistencia de identidad en Shared

Los endpoints administrativos vivirán bajo `/api/administracion/*` en el Host, que es la superficie transversal autorizada para escribir `identity`. Los controllers dependerán de servicios de administración registrados por `ArsDocendi.Shared`; éstos usarán repositorios específicos sobre `IdentityDbContext`. Shared no adquirirá estado mutable ajeno a `identity/audit` ni lógica de otros dominios.

Grupos previstos:

- `/usuarios`: listado, detalle, alta, edición, activación y desactivación;
- `/docentes`: listado, detalle, alta/edición de persona y mantenimiento coordinado de roles docentes y designaciones vigentes;
- `/roles`: listado, alta y edición;
- `/roles/{id}/permisos`: consulta y reemplazo atómico de membresía;
- `/catalogos`: permisos, carreras, materias, roles y personas elegibles.

Administrar un docente requiere coordinar escritura de `identity` y `designaciones`. La orquestación estará en la superficie administrativa del Host y dependerá de una interfaz pública de comandos definida por `Modules.Designaciones.Contracts`; nunca accederá a internals ni al `DesignacionesDbContext`. Esto agrega el edge `Host → Modules.Designaciones.Contracts`, que ya existe en el grafo, sin ciclos. La transacción distribuida entre contextos no es necesaria porque ambos usan la misma base: el caso de uso deberá compartir una transacción o ejecutar una estrategia compensable explícita, validada por integración.

**Alternativas descartadas:** ubicar administración en Portal mezcla perfiles públicos con gobierno de identidad; permitir escritura directa desde Designaciones viola la frontera; exponer `IdentityDbContext` a controllers salta capas.

### D4 — API de Designaciones dentro del módulo

`Modules.Designaciones` publicará controllers bajo `/api/designaciones` que delegan en servicios y repositorios existentes o ampliados. Los recursos serán:

- `/pedidos` y `/pedidos/{id}` para consultas, creación, edición y eliminación;
- comandos explícitos `/enviar`, `/aceptar`, `/rechazar`, `/devolver`, `/reenviar`, `/priorizar` y `/despriorizar`;
- `/periodos` para ABM y cambios de estado;
- `/catalogos` para cargos, materias/personas visibles y demás opciones necesarias.

Los DTOs transportarán IDs canónicos y valores de pantalla necesarios, nunca entidades EF. Los comandos de cambio de estado aceptarán `Idempotency-Key`; el backend resolverá el actor desde `ICurrentUser` y `IConsultasIdentity`, no desde un `ActorContexto` enviado por el cliente. Las reglas se ejecutan en servicio y pedido + historial se confirman en una transacción.

Los endpoints HTTP consumidos sólo por el frontend no obligan a agregar DTOs a `*.Contracts`. Ese proyecto se reserva para interfaces y DTOs de consumidores cross-module; cualquier comando administrativo hacia Designaciones sí se define allí.

### D5 — Problem Details como contrato de error

Todas las superficies nuevas usarán el formato documentado en `docs/architecture/api-contracts.md`, con códigos estables por tipo de error y extensiones `errors` por campo cuando corresponda. El mapeo será común en el Host:

- `400` para forma o tipos inválidos;
- `401/403` para autenticación y autorización;
- `404` para IDs inexistentes o no visibles;
- `409` para unicidad, concurrencia o referencias que impiden eliminar;
- `422` para reglas de dominio.

El frontend decidirá el mensaje y el estado del formulario a partir de status/código/campos, no parseando mensajes de excepciones.

### D6 — Autenticación de desarrollo por headers validados, registrada condicionalmente

En Development/Testing, un handler de autenticación específico aceptará `X-Dev-User-Id` y `X-Dev-Role-Code`. Antes de construir claims consultará `identity`, comprobará que el usuario está activo, que pertenece al dataset sintético y que posee el rol/ámbito solicitado. `GET /api/desarrollo/identidades` devolverá únicamente identidades elegibles de la base sembrada.

El frontend guardará sólo los IDs elegidos como preferencia local y el interceptor Axios añadirá ambos headers. Datos personales, roles y ámbitos siempre se resolverán en backend. Cambiar de rol modifica el header de solicitudes siguientes, sin crear una identidad paralela en TypeScript.

El handler y controller se registrarán dentro de una rama de composición que exige ambiente no productivo y una opción explícita. En Production no existirán rutas, esquema ni lectura de estos headers. Una prueba de integración arrancará el Host como Production y comprobará esa ausencia.

**Alternativas descartadas:** un JWT de desarrollo agrega gestión de claves y expiración sin aportar seguridad en un mecanismo local; una cookie complica CORS en Vite; confiar directamente en headers sin validar contra la DB conservaría el problema actual y permitiría inventar ámbitos.

### D7 — Adapters por feature y React Query para estado remoto

Cada feature tendrá tipos de transporte/adapters, funciones API que usan exclusivamente `shared/api/client.ts` y hooks de consulta/mutación. Componentes y páginas consumirán hooks o callbacks y no importarán Axios. Las query keys serán factories estables por recurso y filtros.

Después de una mutación se actualizará el detalle retornado y se invalidarán listados/catálogos dependientes. No se usarán actualizaciones optimistas para altas, cambios de estado o transiciones de pedidos: la autoridad del backend y las reglas concurrentes hacen preferible esperar confirmación. Los filtros de listas pequeñas MAY seguir siendo client-side sobre la respuesta completa; paginación y filtros server-side se incorporarán sólo si el contrato lo requiere por volumen.

La migración eliminará del grafo runtime `mockStore.ts`, `pedidosStore.ts`, `pedidosSeed.ts`, `periodosMock.ts`, `mockUsers.ts` y catálogos que representen filas persistidas. La máquina de estados TypeScript MAY permanecer como ayuda presentacional para etiquetas y habilitación anticipada, pero nunca decidirá ni persistirá una transición.

### D8 — Migración vertical por feature, sin fallback silencioso

El orden será: infraestructura HTTP/autenticación de desarrollo → seed → administración de usuarios → roles/permisos → docentes → períodos/catálogos → pedidos → revisión. Cada corte vertical incluye backend, adapter frontend, estados remotos y tests antes de quitar su mock.

Una feature migrada no hará fallback a fixtures si la API falla: mostrará estado de error. Durante el desarrollo, commits o flags de build podrán conservar el adapter anterior para rollback técnico, pero el resultado final no incluirá una bifurcación runtime “mock/API”. Esto evita que un fallo de integración quede oculto por datos convincentes pero falsos.

### D9 — Contratos y pruebas por capas

La verificación combinará:

- tests SQL/infra para seguridad, idempotencia, integridad y matriz del seed;
- integración backend con PostgreSQL real para repositorios, autorización, transacciones y endpoints;
- pruebas de arquitectura para Controller → Service → Repository, escritura exclusiva de `identity`, dependencias DAG y ausencia de suplantación en Production;
- tests frontend con simulación a nivel HTTP, no mediante imports de stores mock;
- pruebas de flujo contra Host + PostgreSQL sembrado para los recorridos críticos.

Se agregará un chequeo estático que falle si código runtime importa rutas `/mock/`, seeds de negocio o accede a la clave histórica de `localStorage` de pedidos.

## Risks / Trade-offs

- **[Alcance grande y superposición con cambios activos]** → Implementar por cortes verticales, actualizar `admin-docentes` y `roles-membresia` antes de aplicar, y no duplicar componentes ya terminados.
- **[La administración docente cruza dos contextos]** → Definir el comando en Contracts, probar atomicidad con PostgreSQL y evitar cualquier referencia interna entre proyectos.
- **[El seed puede sobrescribir ediciones sobre fixtures]** → Reservar IDs, documentar que resembrar restaura sólo filas propiedad del dataset y nunca truncar datos ajenos.
- **[Diferencias entre UI mock y modelo persistido]** → Diseñar DTOs desde el modelo canónico; usar adapters de presentación y tests de contrato para detectar pérdidas de campos.
- **[Autorización simulada puede divergir de Azure AD]** → Mantener `ICurrentUser` como frontera común; el handler dev sólo produce claims y toda autorización/ámbito se resuelve desde `identity`.
- **[Listados completos podrían no escalar]** → Mantener el contrato preparado para filtros/paginación, pero no introducir complejidad hasta medir volúmenes reales.
- **[Rollback frontend parcial muestra contratos incompatibles]** → Desplegar backend aditivamente primero, conservar endpoints durante la reversión y retirar mocks sólo después de pruebas end-to-end.

## Migration Plan

1. Documentar los contratos HTTP y agregar de forma aditiva servicios/endpoints, autenticación de desarrollo condicional y tests de seguridad.
2. Completar y validar el seed en una base no productiva limpia; reejecutarlo para verificar idempotencia e integridad.
3. Migrar las features administrativas una por una, manteniendo rutas y componentes visuales; retirar su fuente mock al completar cada corte.
4. Migrar períodos, catálogos, pedidos y revisión de Designaciones; verificar happy path, devolución, rechazo, prioridad y concurrencia.
5. Ejecutar chequeos backend, frontend, arquitectura y end-to-end contra una base sembrada.
6. Actualizar documentación de API, datos, dominios y dependencias; eliminar archivos mock de runtime que ya no tengan consumidores.

Para rollback, se revierte primero el frontend de la feature afectada mientras los endpoints aditivos permanecen disponibles. El seed no se ejecuta en producción y no requiere rollback productivo; en no-prod puede restaurarse una base descartable o reejecutarse la versión anterior. Ningún paso elimina columnas ni cambia destructivamente datos productivos.
