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

La API SHALL persistir como metadata el CV único del docente y los documentos PDF asociados a proyectos. SHALL permitir reemplazar y eliminar el CV, y SHALL permitir DOI, PDF, ambos o ninguno en un proyecto. Esta versión MUST NOT almacenar ni servir contenido binario.

#### Scenario: Cargar o reemplazar CV

- **GIVEN** un docente autenticado
- **WHEN** registra metadata de un PDF como CV
- **THEN** el perfil referencia un único CV con nombre y fecha de carga actuales

#### Scenario: Eliminar CV

- **WHEN** el docente elimina su CV
- **THEN** la referencia queda nula y la API no conserva un historial visible

#### Scenario: Proyecto sin documentación

- **WHEN** el docente crea un proyecto sin PDF ni DOI
- **THEN** el proyecto se guarda correctamente con ambos valores nulos o vacíos

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
