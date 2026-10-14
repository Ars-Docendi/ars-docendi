# Spec Delta

## Purpose

Garantizar la coherencia de referencias entre módulos que comparten la base pero conservan enlaces lógicos, y diagnosticar inconsistencias sin reparar o borrar datos automáticamente.

## ADDED Requirements

### Requirement: Referencias lógicas válidas al confirmar una mutación

El sistema MUST verificar la existencia y elegibilidad de los destinos de nuevas referencias lógicas antes de confirmar. La validación y la escritura MUST coordinarse con cambios incompatibles o borrados del destino. Las referencias históricas a entidades inactivas SHALL seguir resolviéndose; desactivar no equivale a borrar.

#### Scenario: Responsable inexistente

- **GIVEN** un identificador de usuario inexistente
- **WHEN** se intenta asignarlo a una tarea o proyecto
- **THEN** se rechaza la mutación sin filas ni eventos parciales

#### Scenario: Persona canónica del perfil

- **GIVEN** una cuenta asociada a una persona existente
- **WHEN** se crea su perfil
- **THEN** el perfil referencia esa persona y ninguna identidad suministrada arbitrariamente por el cliente

#### Scenario: Destino cambia durante la validación

- **GIVEN** dos operaciones que validan una referencia y borran o vuelven inelegible su destino
- **WHEN** sus ejecuciones se intercalan
- **THEN** no pueden confirmar una nueva referencia inválida y un borrado incompatible como si ambas hubieran sido exitosas

#### Scenario: Usuario inactivo en un historial

- **GIVEN** un autor histórico que fue desactivado pero conserva su identidad
- **WHEN** se consulta el historial autorizado
- **THEN** la referencia se resuelve sin exigir actividad actual ni inventar un autor sustituto

### Requirement: Borrado físico protegido por referencias existentes

Un destino con referencias lógicas persistidas MUST NOT borrarse físicamente por una operación ordinaria. La protección SHALL abarcar las rutas administrativas y de mantenimiento controladas por la aplicación, y MUST coordinarse con altas de referencias concurrentes. Una operación sobre recursos ajenos SHALL conservar la respuesta segura de autorización existente.

#### Scenario: Borrado de usuario referenciado

- **GIVEN** un usuario referenciado por tareas, proyectos, comentarios o historial
- **WHEN** una operación ordinaria intenta borrarlo físicamente
- **THEN** el borrado es rechazado y se conserva la opción de desactivación cuando corresponda

#### Scenario: Borrado de persona con perfil

- **GIVEN** una persona referenciada por un perfil Portal
- **WHEN** se solicita su borrado físico
- **THEN** no se deja un perfil huérfano

### Requirement: Diagnóstico de integridad de solo lectura

El sistema SHALL ofrecer un diagnóstico operativo explícito de referencias huérfanas, scopes incoherentes, duplicados normalizados y jerarquías inválidas. MUST identificar ambiente, versión e historial observado, devolver estado no exitoso ante incompatibilidades y limitar la salida a tipos, conteos e identificadores técnicos necesarios. MUST NOT emitir PII, secretos ni ejecutar saneamientos.

#### Scenario: Datos incompatibles antes del upgrade

- **GIVEN** una base con duplicados de UPN normalizado y referencias huérfanas
- **WHEN** un operador ejecuta el diagnóstico
- **THEN** recibe un resultado incompatible y categorías/conteos comprobables sin modificaciones de filas, histories, objetos ni auditoría de negocio

#### Scenario: Instalación limpia sin hallazgos

- **GIVEN** una instalación reconocida sin inconsistencias
- **WHEN** se ejecuta el diagnóstico
- **THEN** finaliza exitosamente y distingue integridad de datos de compatibilidad de historial y ausencia de drift no inspeccionado

#### Scenario: Acceso no disponible

- **GIVEN** faltan permisos de lectura de alguno de los consumidores requeridos
- **WHEN** se ejecuta el diagnóstico o una protección de borrado que necesita esa consulta
- **THEN** falla cerrado y no presenta la falta de acceso como ausencia de referencias

### Requirement: Saneamiento explícito sin adopción silenciosa

La detección de huérfanos o duplicados MUST NOT habilitar eliminación, fusión de identidades, reseed, stamp ni reasignación automática. Toda corrección de datos existentes SHALL requerir un procedimiento separado aprobado, respaldo y verificación de referencias e historial preservados.

#### Scenario: Dos cuentas con UPN equivalente

- **GIVEN** dos cuentas distintas con roles, historial y referencias propias
- **WHEN** el preflight detecta su equivalencia normalizada
- **THEN** bloquea la activación de la unicidad nueva sin elegir una cuenta ganadora ni fusionar sus datos
