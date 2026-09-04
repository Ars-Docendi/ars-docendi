# perfil-docente-portal Specification

## Purpose

Ofrece al docente autenticado una vista única para consultar y mantener su perfil profesional.

## Requirements

### Requirement: Pantalla "Mi Portal" del docente autenticado

El sistema SHALL ofrecer al docente autenticado una pantalla "Mi Portal" en la ruta `/portal` que presente su perfil en una **sola página**, con las secciones Perfil, Contacto, CV, Experiencia, Educación, Certificaciones, Proyectos, Habilidades e Intereses. La pantalla MUST NOT organizar las secciones en pestañas. Los datos MUST provenir del store mock local, sin llamadas HTTP reales.

#### Scenario: Perfil con datos cargados

- **GIVEN** un docente autenticado con datos en el store mock
- **WHEN** abre `/portal`
- **THEN** ve su perfil en una sola página con todas las secciones visibles

#### Scenario: Estado de carga

- **WHEN** la lectura del perfil está en curso
- **THEN** la pantalla muestra el estado Loading

#### Scenario: Estado de error

- **WHEN** la lectura del perfil falla
- **THEN** la pantalla muestra el estado Error con un mensaje accionable y sin romper la navegación

### Requirement: Bloque Perfil de solo lectura

El sistema SHALL mostrar un bloque **Perfil** con la identidad del docente provista por Azure AD (nombre, apellido y mail institucional) y sus datos institucionales provistos por Secretaría (DNI, legajo y CUIL). El docente MUST NOT poder editar ninguno de estos campos desde el Portal. El bloque MUST estar siempre poblado: sus datos no dependen de que el docente haya cargado algo.

#### Scenario: El bloque Perfil viene precargado

- **GIVEN** un docente que nunca cargó información en su Portal
- **WHEN** abre `/portal`
- **THEN** el bloque Perfil ya muestra su nombre, mail institucional, DNI, legajo y CUIL

#### Scenario: Los datos del Perfil no son editables

- **WHEN** el docente visualiza el bloque Perfil
- **THEN** el bloque no ofrece ningún control de edición, a diferencia del resto de las secciones

#### Scenario: El mail institucional se distingue del de contacto

- **WHEN** el docente visualiza el bloque Perfil y la sección Contacto
- **THEN** el campo del Perfil se rotula "Mail institucional" y el de Contacto se rotula "Mail"

### Requirement: Edición por sección sin guardado global

El sistema SHALL presentar cada sección editable en modo lectura por defecto y SHALL permitir editarla de forma independiente, con su propio guardado. La pantalla MUST NOT ofrecer una acción de guardado global que abarque varias secciones. Guardar una sección MUST NOT requerir que otras secciones estén completas, y ningún campo del perfil MUST ser obligatorio.

#### Scenario: Guardar una sección sin tocar las demás

- **GIVEN** un docente en su Portal
- **WHEN** edita una sección y confirma el cambio
- **THEN** solo esa sección se guarda y el resto del perfil queda intacto

#### Scenario: Confirmación del guardado

- **WHEN** el docente guarda los cambios de una sección
- **THEN** el sistema muestra una confirmación de que los cambios se guardaron

#### Scenario: Descartar la edición de una sección

- **GIVEN** una sección en modo edición con cambios sin confirmar
- **WHEN** el docente cancela la edición
- **THEN** la sección vuelve a modo lectura con los valores previos

#### Scenario: Ninguna sección bloquea el perfil

- **GIVEN** un docente con secciones vacías
- **WHEN** guarda cualquier otra sección
- **THEN** el guardado se completa sin exigir que las secciones vacías se llenen

### Requirement: Sección vacía compacta

El sistema SHALL renderizar una sección sin datos como **una fila compacta** con su nombre y su control de alta, y SHALL expandirla a su presentación completa cuando tenga contenido. La pantalla MUST NOT mostrar barras de progreso, porcentajes de completitud ni avisos que enumeren lo que falta cargar.

#### Scenario: Perfil recién estrenado

- **GIVEN** un docente sin ningún dato propio cargado
- **WHEN** abre `/portal`
- **THEN** ve el bloque Perfil poblado y el resto de las secciones como filas compactas con su control de alta

#### Scenario: La sección se expande al tener contenido

- **GIVEN** una sección vacía presentada como fila compacta
- **WHEN** el docente carga su primer ítem
- **THEN** la sección se expande y muestra el ítem cargado

#### Scenario: Sin indicadores de completitud

- **WHEN** el docente visualiza su Portal con secciones vacías
- **THEN** la pantalla no muestra barra de progreso, porcentaje ni aviso listando lo que falta

### Requirement: Comportamiento independiente de la visita

El sistema SHALL determinar la presentación de cada sección exclusivamente por su **estado** (vacía o con datos). La pantalla MUST NOT tener un modo distinto para el primer ingreso del docente ni requerir registrar si el docente ya visitó el Portal.

#### Scenario: Primer ingreso y visitas posteriores se comportan igual

- **GIVEN** dos docentes con el mismo estado de perfil, uno en su primer ingreso y otro en su vigésimo
- **WHEN** ambos abren `/portal`
- **THEN** ven exactamente la misma pantalla
