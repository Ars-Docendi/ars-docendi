# Spec Delta

## ADDED Requirements

### Requirement: Perfil completo sin multiplicar colecciones

Consultar el perfil SHALL devolver sus secciones completas sin obtener un producto cartesiano entre colecciones hermanas. La obtención MUST resolver identidad por cuenta/persona y metadata documental por lote, sin cargar usuarios globales ni hacer una consulta independiente por documento. No SHALL omitirse contenido para aparentar rendimiento.

#### Scenario: Varias colecciones pobladas

- **GIVEN** un perfil con 3 experiencias, 3 educaciones, 3 certificaciones, 2 proyectos y 4 habilidades
- **WHEN** se consulta completo
- **THEN** contiene los 15 elementos correspondientes sin materializar las 216 combinaciones entre colecciones

#### Scenario: Documentos de varios proyectos

- **GIVEN** un perfil con varios documentos de proyecto
- **WHEN** se completa su metadata autorizada
- **THEN** la cantidad de consultas documentales no crece una por documento

### Requirement: Mutaciones de sección independientes y versionadas

Actualizar o eliminar una sección existente SHALL aportar su versión observada y devolver la versión resultante cuando corresponda. Una versión obsoleta MUST producir 409 sin sobrescritura ni efectos documentales parciales. La obtención para una mutación MUST limitarse a la sección y referencias necesarias, sin cargar el perfil completo.

#### Scenario: Contacto concurrente

- **GIVEN** dos clientes leen la misma versión del contacto
- **WHEN** uno guarda y el otro intenta confirmar su versión anterior
- **THEN** el segundo recibe 409 y no pisa los valores confirmados

#### Scenario: Modificar una experiencia

- **GIVEN** un perfil con grandes colecciones y una experiencia existente
- **WHEN** se modifica esa experiencia con versión válida
- **THEN** conserva las otras secciones sin cargar todas sus colecciones para validar el ítem

#### Scenario: Reemplazo documental rechazado

- **GIVEN** una sección documental fue modificada después de la lectura del cliente
- **WHEN** este intenta reemplazarla con versión obsoleta
- **THEN** conserva la asociación confirmada y no elimina ni desvincula sus bytes

### Requirement: Perfil y vocabulario únicos bajo altas concurrentes

Las primeras escrituras concurrentes del mismo perfil SHALL resolver una identidad única de perfil sin fallar por su creación incidental. Crear un término normalizado concurrentemente SHALL reutilizar el vocabulario confirmado y conservar asociaciones independientes de habilidades/intereses. MUST NOT tratar una edición incompatible de sección como éxito idempotente.

#### Scenario: Dos primeras secciones

- **GIVEN** una persona sin perfil y dos solicitudes válidas para secciones diferentes
- **WHEN** ambas crean incidentalmente el perfil
- **THEN** existe un perfil único y se conservan las dos secciones sin 500 de unicidad

#### Scenario: Dos docentes sugieren el mismo término

- **GIVEN** un término normalizado aún inexistente
- **WHEN** dos perfiles lo sugieren concurrentemente
- **THEN** existe un término canónico normalizado y cada perfil conserva su asociación autorizada

### Requirement: Acceso selectivo por perfil en persistencia

Las colecciones SHALL poder consultarse por perfil sin recorrer innecesariamente las colecciones de otros perfiles. La verificación de rendimiento MUST mantener cantidad de resultados constante y comparar planes/cardinalidad con volumen sintético representativo; no SHALL imponer un tiempo absoluto derivado de una sola máquina.

#### Scenario: Perfil pequeño en un conjunto grande

- **GIVEN** 2.000 perfiles con 20.000 experiencias y 10 del perfil consultado
- **WHEN** se verifica su acceso selectivo
- **THEN** existe un plan de acceso compatible con el filtro que evita descartar las otras 19.990 filas por falta de acceso selectivo
