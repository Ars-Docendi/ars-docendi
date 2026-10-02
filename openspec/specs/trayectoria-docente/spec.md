# trayectoria-docente Specification

## Purpose

Permite al docente administrar su experiencia laboral y trayectoria profesional.

## Requirements

### Requirement: Gestión de la experiencia laboral

El sistema SHALL permitir al docente administrar su experiencia como una lista de ítems, pudiendo agregar, editar y eliminar entradas mediante la API del Portal. Cada ítem MUST tener puesto, organización, período y una descripción de qué se trató. El período MUST admitir marcarse como vigente ("actual") en lugar de una fecha de fin. El alta y la edición MUST hacerse en un diálogo.

#### Scenario: Agregar un empleo

- **GIVEN** un docente en la sección Experiencia de su Portal
- **WHEN** agrega un ítem con puesto, organización, período y descripción, y confirma
- **THEN** el ítem aparece en su lista de experiencia

#### Scenario: Empleo vigente

- **WHEN** el docente marca un empleo como actual
- **THEN** el ítem se muestra sin fecha de fin, indicando que sigue vigente

#### Scenario: Editar un empleo

- **GIVEN** un ítem de experiencia ya cargado
- **WHEN** el docente lo edita y confirma
- **THEN** la lista refleja los valores actualizados

#### Scenario: Eliminar un empleo

- **GIVEN** un ítem de experiencia ya cargado
- **WHEN** el docente lo elimina y confirma la acción
- **THEN** el ítem desaparece de su lista

### Requirement: Gestión de proyectos e investigaciones

El sistema SHALL permitir al docente administrar sus proyectos como una lista de ítems, pudiendo agregar, editar y eliminar entradas. Cada ítem MUST tener nombre, rol, años y descripción. Los **trabajos de investigación y su documentación forman parte de esta sección**: el sistema MUST NOT ofrecer una sección separada para producción científica.

#### Scenario: Agregar un proyecto

- **GIVEN** un docente en la sección Proyectos de su Portal
- **WHEN** agrega un ítem con nombre, rol, años y descripción, y confirma
- **THEN** el ítem aparece en su lista de proyectos

#### Scenario: Cargar una investigación

- **WHEN** el docente carga un trabajo de investigación
- **THEN** lo hace como un ítem más de la sección Proyectos, sin una sección aparte

#### Scenario: Eliminar un proyecto

- **GIVEN** un proyecto ya cargado
- **WHEN** el docente lo elimina y confirma la acción
- **THEN** el ítem desaparece de su lista

### Requirement: Documentación del proyecto por archivo o por enlace

El sistema SHALL permitir registrar para cada proyecto un documento PDF privado, un enlace DOI, ambos o ninguno. El documento SHALL cargarse mediante una sesión emitida por el backend, persistirse en SeaweedFS mediante un `archivoId` y quedar asociado solo después de superar la validación de contenido, tamaño, hash y antivirus. La API persiste la metadata necesaria y una referencia interna al objeto; el almacenamiento binario queda fuera de PostgreSQL. El sistema MUST NOT exigir documentación para guardar un proyecto y MUST NOT aceptar una URI arbitraria ni exponer el bucket.

#### Scenario: Proyecto con PDF

- **GIVEN** un PDF válido cargado mediante una sesión autorizada
- **WHEN** el docente confirma el proyecto
- **THEN** el ítem conserva el documento asociado y permite descargarlo mediante autorización o URL temporal

#### Scenario: Proyecto con DOI

- **WHEN** el docente carga un DOI en un proyecto y confirma
- **THEN** el ítem muestra el enlace sin exigir un archivo

#### Scenario: Proyecto con PDF y DOI

- **GIVEN** un proyecto con un PDF validado y un DOI válido
- **WHEN** el docente confirma
- **THEN** el proyecto conserva ambas referencias de forma independiente

#### Scenario: Proyecto sin documentación

- **WHEN** el docente guarda un proyecto sin adjunto ni enlace
- **THEN** el ítem se guarda igualmente

#### Scenario: Archivo inválido

- **WHEN** el docente intenta asociar un archivo que no es un PDF válido, está pendiente, fue rechazado o pertenece a otra persona
- **THEN** la API rechaza la operación y no modifica el proyecto anterior

### Requirement: Confirmación antes de eliminar

El sistema SHALL pedir confirmación antes de eliminar un ítem de experiencia o un proyecto, indicando qué se está por borrar y advirtiendo que la acción no se puede deshacer. El sistema MUST NOT exigir un justificativo.

#### Scenario: Confirmar el borrado

- **GIVEN** un ítem cargado
- **WHEN** el docente pide eliminarlo
- **THEN** el sistema pide confirmación indicando qué se borra y que no se puede deshacer

#### Scenario: Cancelar el borrado

- **GIVEN** la confirmación de borrado abierta
- **WHEN** el docente cancela
- **THEN** el ítem se conserva en la lista
