## ADDED Requirements

### Requirement: La línea de base registra el veredicto de cada ítem

El sistema SHALL guardar, por eje, el desenlace de cada ítem identificado por su identificador, junto con los tres hashes del sellado.

#### Scenario: Cada ítem tiene su veredicto

- **GIVEN** un reporte de una corrida
- **WHEN** se genera la línea de base
- **THEN** contiene una entrada por ítem con su desenlace

#### Scenario: La línea de base guarda el sello

- **GIVEN** una línea de base
- **WHEN** se la inspecciona
- **THEN** contiene los tres hashes de la corrida que la generó

### Requirement: El gate falla si un ítem que pasaba ahora falla

El sistema SHALL comparar cada ítem contra su veredicto en la línea de base, y SHALL fallar si alguno pasó de correcto a incorrecto, aunque el agregado haya mejorado.

#### Scenario: Un ítem roto hace fallar el gate aunque el promedio suba

- **GIVEN** una línea de base y una corrida con más aciertos en total
- **WHEN** un ítem que pasaba ahora falla
- **THEN** el gate falla y nombra ese ítem

#### Scenario: Una corrida igual pasa

- **GIVEN** una corrida con los mismos desenlaces que la línea de base
- **WHEN** corre el gate
- **THEN** pasa

#### Scenario: Un ítem que se arregla no hace fallar nada

- **GIVEN** un ítem que fallaba en la línea de base
- **WHEN** ahora pasa
- **THEN** el gate pasa y lo informa como mejora

#### Scenario: Un ítem nuevo no hace fallar el gate

- **GIVEN** una corrida con un ítem que no está en la línea de base
- **WHEN** corre el gate
- **THEN** no falla por ese ítem y lo informa como nuevo

### Requirement: Con el sello cambiado el gate exige regenerar

El sistema MUST NOT comparar ítem a ítem cuando alguno de los tres hashes difiera, y SHALL pedir que se regenere la línea de base.

#### Scenario: Un hash distinto detiene la comparación

- **GIVEN** una línea de base con un hash de dataset distinto al de la corrida
- **WHEN** corre el gate
- **THEN** no compara y exige regenerar, nombrando qué cambió

### Requirement: La regeneración es explícita

El sistema MUST NOT regenerar la línea de base como efecto de una corrida.

#### Scenario: Correr el eje no reescribe la línea de base

- **GIVEN** una línea de base existente
- **WHEN** se corre el eje sin pedir regeneración
- **THEN** el archivo no cambia
