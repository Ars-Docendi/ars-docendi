## MODIFIED Requirements

### Requirement: CV y documentación de proyectos como metadata

La API SHALL persistir la metadata y el `archivoId` del CV único del docente y de los documentos PDF asociados a proyectos. Los bytes SHALL almacenarse en MinIO privado, fuera de PostgreSQL, y MUST quedar disponibles únicamente después de validar contenido, tamaño, hash y antivirus. SHALL permitir reemplazar y eliminar el CV, y SHALL permitir DOI, PDF, ambos o ninguno en un proyecto. La API MUST NOT aceptar URI arbitrarias, publicar objetos directamente ni devolver claves internas del almacenamiento.

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
- **THEN** el proyecto conserva el `archivoId` y permite una descarga autorizada sin exponer una URL permanente de MinIO
