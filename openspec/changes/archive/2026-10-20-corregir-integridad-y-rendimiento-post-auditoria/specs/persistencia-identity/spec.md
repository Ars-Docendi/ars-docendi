# Spec Delta

## ADDED Requirements

### Requirement: UPN normalizado único para todos los escritores

Los UPN SHALL compararse por una normalización única de espacios exteriores y mayúsculas/minúsculas, común a altas, ediciones e ingreso. La base MUST impedir cuentas equivalentes bajo esa normalización. El upgrade MUST bloquear duplicados existentes sin fusionar cuentas, permisos o referencias automáticamente.

#### Scenario: Duplicado desde SQL

- **GIVEN** una cuenta con UPN normalizado existente
- **WHEN** otro escritor intenta insertar la variante con mayúsculas o espacios exteriores
- **THEN** la base rechaza el duplicado y el ingreso conserva una coincidencia única

#### Scenario: Cuenta existente conserva identidad

- **GIVEN** una cuenta sin duplicados normalizados con roles y persona asociados
- **WHEN** se activa la garantía
- **THEN** conserva ID, persona, roles y vínculo Microsoft sin recreación de identidad

### Requirement: Nombre único de rol activo bajo concurrencia

Los nombres de roles activos MUST ser únicos bajo trim y comparación sin distinguir mayúsculas/minúsculas. La garantía SHALL alcanzar altas y renombres concurrentes de filas distintas; un conflicto MUST devolver error de dominio estable y no 500. Desactivar un rol SHALL conservar su identidad e historial.

#### Scenario: Dos renombres simultáneos

- **GIVEN** dos roles activos distintos y un nombre libre
- **WHEN** ambos se renombran concurrentemente a ese nombre equivalente
- **THEN** solo uno confirma y el otro recibe conflicto sin cambiar su código ni permisos

### Requirement: Consultas de identidad selectivas

Las consultas para actor, usuarios/personas por ID o lote y candidatos SHALL materializar solo los resultados necesarios al caso. Filtrado, jerarquía y límites MUST preceder a materialización para búsquedas acotadas, preservando autorización, términos sin distinguir tildes y resolución histórica de identidades inactivas.

#### Scenario: Resolver actor de Portal

- **GIVEN** una cuenta autenticada entre muchas cuentas
- **WHEN** Portal resuelve su persona
- **THEN** consulta esa identidad sin materializar el directorio completo

#### Scenario: Personas de un conjunto de tareas

- **GIVEN** un lote de IDs de actores/responsables visibles
- **WHEN** se resuelven sus etiquetas
- **THEN** no se carga cada persona individualmente ni el directorio global

### Requirement: Roles personalizados y metadatos de sistema protegidos

El sistema SHALL permitir roles personalizados con `scope` válido y código único. Los roles de sistema SHALL tener `es_sistema = TRUE` y metadatos inmutables: código, ámbito, nombre, descripción, actividad y marca de sistema; su borrado MUST denegarse. Los personalizados SHALL permitir edición autorizada de nombre/descripción y ámbito solo si no tienen asignaciones vivas. La membresía de permisos conserva su superficie autorizada.

#### Scenario: Creación de un rol nuevo con scope

- **GIVEN** un operador autorizado
- **WHEN** crea un rol con ámbito válido
- **THEN** se persiste como personalizado y queda disponible para asignación

#### Scenario: El code de un rol de sistema es inmutable

- **GIVEN** un rol de sistema
- **WHEN** se intenta modificar código o ámbito
- **THEN** la base rechaza la operación

#### Scenario: Un rol de sistema no se puede borrar

- **GIVEN** un rol de sistema
- **WHEN** se intenta borrarlo físicamente
- **THEN** la base rechaza el borrado

#### Scenario: Los metadatos de un rol de sistema no se editan

- **GIVEN** un rol de sistema
- **WHEN** se intenta editar nombre o descripción
- **THEN** se rechaza conforme a la protección vigente y a la capability editar-rol

#### Scenario: Un rol sin scope es rechazado

- **GIVEN** una creación de rol
- **WHEN** se omite el ámbito o se usa uno fuera del catálogo permitido
- **THEN** la base rechaza la operación

### Requirement: Asignaciones coherentes con el ámbito vigente

Toda asignación SHALL ser coherente: global sin materia/carrera; Docente con ambas; Jefe de Cátedra y personalizado de materia con materia y sin carrera; Coordinador y personalizado de carrera con carrera y sin materia. Las referencias SHALL ser canónicas. Cambiar ámbito con asignaciones vivas MUST rechazarse; las revocadas SHALL conservar contexto histórico y poder revocarse aun si existe incoherencia legada, sin habilitar permisos nuevos.

#### Scenario: Asignación de un rol de materia sin materia

- **GIVEN** un rol personalizado de ámbito materia
- **WHEN** se intenta asignarlo sin materia
- **THEN** se rechaza; una materia válida sin carrera sí satisface su ámbito

#### Scenario: Asignación de Docente sin carrera

- **GIVEN** el rol Docente
- **WHEN** se asigna materia sin carrera
- **THEN** se rechaza por ausencia del par requerido

#### Scenario: Jefe de Cátedra con una sola membresía por materia

- **GIVEN** una materia compartida por dos carreras
- **WHEN** se asigna Jefe de Cátedra sobre ella
- **THEN** existe una membresía con materia y sin carrera, válida en ambas

#### Scenario: Asignación de un rol global con ámbito

- **GIVEN** un rol global
- **WHEN** se asigna materia o carrera
- **THEN** se rechaza

#### Scenario: Un rol creado por el operador respeta su propio scope

- **GIVEN** un rol personalizado de carrera
- **WHEN** se asigna carrera válida sin materia
- **THEN** se acepta con la misma integridad del ámbito correspondiente

#### Scenario: Revocar y volver a otorgar la misma asignación

- **GIVEN** una asignación revocada
- **WHEN** se vuelve a otorgar el mismo usuario, rol y ámbito
- **THEN** se permite sin duplicar asignaciones vivas

#### Scenario: Cambiar scope con asignaciones vigentes

- **GIVEN** un rol personalizado global con una asignación viva
- **WHEN** se intenta cambiarlo a materia
- **THEN** API y base rechazan el cambio y mantienen coherencia y permisos anteriores

#### Scenario: Carrera de asignación contra cambio de ámbito

- **GIVEN** una asignación y un cambio de ámbito concurrentes
- **WHEN** ambos intentan confirmar
- **THEN** se serializan o uno falla sin dejar asignaciones vivas incoherentes

## REMOVED Requirements

### Requirement: Roles creables con scope, y protección del catálogo de sistema

**Reason**: El bloque mezcla protección de sistema con un escenario de renombre permitido que contradice el SQL publicado y editar-rol. Creación personalizada, ámbito obligatorio y protección de código/borrado se mantienen íntegros en el reemplazo.

**Migration**: Usar Roles personalizados y metadatos de sistema protegidos. Los metadatos de sistema son inmutables; no se modifica ningún rol publicado ni se retira la gestión autorizada de sus permisos.

### Requirement: El scope declarado por el rol gobierna la asignación a usuarios

**Reason**: El escenario genérico materia exige carrera, contradiciendo el mismo requisito, el trigger y la distinción Docente/Jefe. Las garantías y escenarios de asignación válidos se conservan y se agregan protección de cambios concurrentes y preservación histórica.

**Migration**: Usar Asignaciones coherentes con el ámbito vigente. Docente requiere materia+carrera; Jefe y personalizado de materia usan materia sin carrera. No migrar ni reasignar automáticamente membresías existentes.
