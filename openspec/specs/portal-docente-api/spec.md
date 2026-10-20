# portal-docente-api Specification

## Purpose

Provee una API persistente y segura para que cada docente consulte y mantenga su perfil profesional, reemplazando el estado mock y habilitando futuros consumidores de lectura.

## Requirements

### Requirement: Consulta del perfil propio

La API SHALL exponer `GET /api/portal/perfil` para devolver el perfil completo del usuario autenticado. La respuesta MUST incluir identidad y datos institucionales de `identity` como solo lectura, junto con contacto, CV, experiencia, educación, certificaciones, proyectos, habilidades e intereses persistidos en Portal.

#### Scenario: Docente con perfil

- **GIVEN** un usuario autenticado vinculado a una persona
- **WHEN** solicita `GET /api/portal/perfil`
- **THEN** recibe HTTP 200 con sus datos institucionales y todas sus secciones propias

#### Scenario: Usuario sin datos propios

- **GIVEN** un usuario autenticado vinculado a una persona sin secciones cargadas
- **WHEN** solicita `GET /api/portal/perfil`
- **THEN** recibe HTTP 200 con identidad institucional poblada y colecciones vacías o valores nulos para el resto

#### Scenario: Sin autenticación

- **WHEN** un cliente solicita el perfil sin autenticarse
- **THEN** recibe HTTP 401 y no obtiene datos del perfil

### Requirement: Actualización independiente de secciones

La API SHALL permitir actualizar por separado contacto, CV y cada colección del perfil mediante endpoints autenticados. Cada operación MUST afectar únicamente la sección indicada y MUST conservar los datos de las demás secciones. El docente MUST NOT poder modificar nombre, apellido, UPN, documento, legajo ni CUIL desde Portal.

#### Scenario: Actualizar contacto

- **GIVEN** un docente autenticado
- **WHEN** envía un contacto con teléfono y/o mail válidos
- **THEN** la API guarda solo contacto y devuelve la sección actualizada

#### Scenario: Contacto opcional con mail inválido

- **GIVEN** un docente autenticado
- **WHEN** envía un mail de contacto con formato inválido
- **THEN** la API responde HTTP 400, informa el campo inválido y no modifica el contacto anterior

#### Scenario: Intentar modificar identidad institucional

- **WHEN** un cliente incluye campos institucionales en una operación de actualización
- **THEN** la API rechaza la modificación y los valores institucionales permanecen sin cambios

### Requirement: Gestión de experiencia, educación, certificaciones y proyectos

La API SHALL permitir crear, consultar, modificar y eliminar ítems propios de experiencia, educación, certificaciones y proyectos. Las validaciones MUST exigir los campos definidos por la pantalla, permitir períodos vigentes y fechas opcionales según corresponda, y MUST tratar estos datos como informativos sin circuito de aprobación.

#### Scenario: Crear ítem válido

- **GIVEN** un docente autenticado y datos válidos de una sección
- **WHEN** crea el ítem mediante la API de Portal
- **THEN** recibe HTTP 201 con un identificador estable y el ítem aparece en su perfil

#### Scenario: Editar ítem propio

- **GIVEN** un ítem perteneciente al docente
- **WHEN** el docente lo modifica
- **THEN** la API devuelve HTTP 200 con los valores nuevos y conserva el identificador

#### Scenario: Eliminar ítem propio

- **GIVEN** un ítem perteneciente al docente
- **WHEN** el docente solicita eliminarlo
- **THEN** la API responde HTTP 204 y el ítem deja de aparecer en el perfil

#### Scenario: Acceder a ítem ajeno

- **GIVEN** un identificador perteneciente a otro docente
- **WHEN** el docente intenta modificar o eliminar ese ítem
- **THEN** la API responde como recurso no disponible y no revela ni modifica el dato ajeno

### Requirement: CV y documentación de proyectos como metadata

La API SHALL persistir la metadata y el `archivoId` del CV único del docente y de los documentos PDF asociados a proyectos. Los bytes SHALL almacenarse en SeaweedFS privado, fuera de PostgreSQL, y MUST quedar disponibles únicamente después de validar contenido, tamaño, hash y antivirus. SHALL permitir reemplazar y eliminar el CV, y SHALL permitir DOI, PDF, ambos o ninguno en un proyecto. La API MUST NOT aceptar URI arbitrarias, publicar objetos directamente ni devolver claves internas del almacenamiento.

#### Scenario: Cargar o reemplazar CV

- **GIVEN** un docente autenticado y un archivo PDF que pasó la validación
- **WHEN** registra o reemplaza su CV
- **THEN** el perfil referencia un único archivo privado con nombre, fecha, tamaño, hash y estado disponible

#### Scenario: CV inválido

- **GIVEN** un archivo que no es PDF, supera el límite o falla el antivirus
- **WHEN** el docente intenta confirmarlo como CV
- **THEN** la API responde un error consumible por el formulario y no modifica el CV anterior

#### Scenario: Eliminar CV

- **WHEN** el docente elimina su CV
- **THEN** la referencia queda nula o desvinculada, no se puede descargar por el perfil y la eliminación queda auditada según la retención configurada

#### Scenario: Proyecto sin documentación

- **WHEN** el docente crea un proyecto sin PDF ni DOI
- **THEN** el proyecto se guarda correctamente con ambos valores nulos o vacíos

#### Scenario: Proyecto con PDF

- **GIVEN** un PDF de proyecto validado
- **WHEN** el docente lo asocia al proyecto
- **THEN** el proyecto conserva el `archivoId` y permite una descarga autorizada sin exponer una URL permanente de SeaweedFS

### Requirement: Habilidades e intereses independientes

La API SHALL persistir habilidades e intereses como relaciones independientes del mismo vocabulario. SHALL permitir agregar y quitar términos existentes o sugeridos, sin que quitar un término de una lista lo quite de la otra. Los términos nuevos MUST normalizarse para evitar duplicados equivalentes.

#### Scenario: Agregar el mismo término en ambas listas

- **GIVEN** un docente autenticado
- **WHEN** agrega un término como habilidad y como interés
- **THEN** el término queda asociado a ambas listas de forma independiente

#### Scenario: Sugerir término nuevo

- **WHEN** el docente agrega un término inexistente en el vocabulario
- **THEN** la API crea o reutiliza su término normalizado y lo marca como sugerido en la lista solicitada

#### Scenario: Quitar un término

- **GIVEN** un término asociado como habilidad y como interés
- **WHEN** el docente lo quita de habilidades
- **THEN** desaparece de habilidades y permanece en intereses

### Requirement: Persistencia, auditoría y seed sintético

Las entidades del perfil SHALL persistirse en el schema `portal`, incluir `created_at`, quedar auditadas según la infraestructura existente y referenciar la persona canónica por ID. El dataset sintético no productivo SHALL incluir perfiles con datos completos, perfiles parcialmente vacíos, CV/documentos metadata y habilidades sugeridas, mediante UUIDs estables y upserts idempotentes.

#### Scenario: Reejecutar seed

- **GIVEN** una base migrada con el dataset sintético aplicado
- **WHEN** se ejecuta nuevamente el seed
- **THEN** las fixtures Portal quedan restauradas sin duplicados ni eliminación de filas ajenas

#### Scenario: Registrar cambio

- **WHEN** una operación autenticada modifica una entidad Portal
- **THEN** queda un evento en el mecanismo de auditoría con el actor y la operación correspondientes

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
