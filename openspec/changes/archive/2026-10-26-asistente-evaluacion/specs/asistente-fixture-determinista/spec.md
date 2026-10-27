## ADDED Requirements

### Requirement: Fixture reproducible byte a byte

El generador del fixture SHALL producir el mismo contenido en dos ejecuciones distintas, en cualquier proceso y en cualquier máquina.

El fixture MUST NOT contener datos personales reales. MUST NOT depender del reloj.

#### Scenario: Dos ejecuciones producen lo mismo

- **WHEN** se ejecuta el generador dos veces
- **THEN** el contenido producido es idéntico byte a byte

#### Scenario: Dos procesos distintos producen lo mismo

- **WHEN** se ejecuta el generador en dos procesos distintos
- **THEN** el contenido producido es idéntico

#### Scenario: El fixture no usa funciones de reloj

- **WHEN** se inspecciona el contenido generado
- **THEN** no contiene ninguna función de reloj de la base de datos

#### Scenario: El fixture tiene una fecha ancla fija

- **WHEN** se inspeccionan las fechas del contenido generado
- **THEN** todas derivan de la fecha ancla declarada y ninguna del día de ejecución

### Requirement: Fuente de aleatoriedad por sección

Cada sección del generador MUST usar su propia fuente de aleatoriedad.

Agregar o quitar elementos de una sección MUST NOT alterar los valores de las secciones siguientes.

#### Scenario: Un cambio en una sección no corre las demás

- **GIVEN** el fixture generado con la cantidad actual de personas
- **WHEN** se regenera con una persona más
- **THEN** los identificadores y valores de las secciones posteriores no cambian

### Requirement: Colisiones del dominio garantizadas

El fixture SHALL contener nombres de materia repetidos entre carreras y apellidos compartidos entre personas, con cardinalidades declaradas.

#### Scenario: Hay nombres de materia repetidos entre carreras

- **WHEN** se agrupan las materias por nombre
- **THEN** al menos dos nombres aparecen en más de una carrera, y al menos uno aparece en tres

#### Scenario: Hay apellidos compartidos

- **WHEN** se agrupan las personas por apellido
- **THEN** al menos tres apellidos corresponden a más de una persona, y al menos uno a tres

#### Scenario: Lo actual se expresa con banderas del dominio

- **WHEN** se inspecciona el fixture
- **THEN** hay exactamente un período marcado como activo y hay designaciones con vigencia abierta

### Requirement: Huella del fixture

El generador SHALL exponer una huella estable del fixture, para el sellado de reportes.

#### Scenario: La huella no cambia entre ejecuciones

- **WHEN** se calcula la huella dos veces
- **THEN** es la misma

#### Scenario: Un cambio del fixture cambia la huella

- **GIVEN** la huella del fixture vigente
- **WHEN** se cambia cualquier dato del fixture y se recalcula
- **THEN** la huella es distinta
