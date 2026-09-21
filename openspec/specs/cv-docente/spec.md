# cv-docente Specification

## Purpose

Permite al docente administrar un único CV en formato PDF dentro de su perfil profesional.

## Requirements

### Requirement: Carga del CV en PDF

El sistema SHALL permitir al docente cargar un CV en formato PDF como archivo único de su perfil. La sección CV sin archivo cargado MUST presentarse como una zona de arrastre, no como una fila con control de alta, de modo que la acción se entienda por la forma del control y sin texto explicativo. La carga SHALL usar una sesión emitida por el backend y el CV SHALL quedar asociado mediante un `archivoId` después de superar validación de contenido, tamaño, hash y antivirus. La API persiste nombre, fecha, tamaño, hash y referencia al objeto privado; el frontend MUST NOT enviar una URI arbitraria ni marcar la carga como disponible.

#### Scenario: Sección CV vacía

- **GIVEN** un docente que no cargó su CV
- **WHEN** abre `/portal`
- **THEN** la sección CV se presenta como zona de arrastre

#### Scenario: Cargar el CV

- **GIVEN** la sección CV vacía y un PDF válido
- **WHEN** el docente completa la carga y la revisión del backend termina correctamente
- **THEN** la sección muestra el nombre y fecha del archivo, y el documento queda descargable solo con autorización

#### Scenario: Solo se acepta PDF

- **WHEN** el docente intenta confirmar un archivo que no es un PDF válido
- **THEN** el sistema rechaza el archivo, informa el formato admitido y conserva el estado anterior

### Requirement: Reemplazo y baja del CV

El sistema SHALL permitir al docente reemplazar el CV cargado por otro archivo validado y SHALL permitir eliminarlo. Al reemplazarlo, el perfil MUST referenciar solo el archivo nuevo; el objeto anterior debe quedar desvinculado y seguir la política de retención, sin quedar accesible mediante una URL permanente.

#### Scenario: Reemplazar el CV

- **GIVEN** un docente con un CV ya cargado
- **WHEN** carga y confirma un PDF nuevo válido
- **THEN** el perfil referencia solo el archivo nuevo con su fecha, tamaño y hash actualizados

#### Scenario: Eliminar el CV

- **GIVEN** un docente con un CV cargado
- **WHEN** lo elimina
- **THEN** la sección CV vuelve a presentarse como zona de arrastre y el archivo deja de estar disponible para descargas del perfil
