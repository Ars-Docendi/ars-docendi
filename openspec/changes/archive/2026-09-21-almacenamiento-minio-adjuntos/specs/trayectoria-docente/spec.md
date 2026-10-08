## MODIFIED Requirements

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
