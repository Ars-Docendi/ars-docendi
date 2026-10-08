## ADDED Requirements

### Requirement: El manifiesto es la única declaración de las aristas del grafo de proyectos

El repositorio SHALL declarar las aristas del grafo de proyectos del backend en un único archivo versionado y legible por máquina. Cada arista SHALL declarar origen, destino, vía y motivo; y MAY declarar una excepción a un invariante de arquitectura.

La vía declarada SHALL pertenecer a un vocabulario cerrado que el verificador sepa comprobar. El manifiesto MUST NOT contener aristas proyectadas, planeadas ni condicionales: una fila existe cuando la referencia existe en el código.

#### Scenario: Cada arista declara sus cuatro campos

- **GIVEN** el manifiesto de aristas
- **WHEN** se lo carga
- **THEN** cada arista tiene origen, destino, vía y motivo
- **AND** ninguno de esos campos está vacío

#### Scenario: Una vía que el verificador no sabe comprobar no carga

- **GIVEN** una arista cuya vía no pertenece al vocabulario cerrado
- **WHEN** se carga el manifiesto
- **THEN** la carga falla nombrando la arista y la vía declarada

#### Scenario: El manifiesto no admite aristas todavía inexistentes

- **GIVEN** una arista que el equipo planea agregar pero que ningún `.csproj` referencia
- **WHEN** se la escribe en el manifiesto
- **THEN** la verificación falla, porque la fila no corresponde a ninguna referencia real

### Requirement: Toda arista presente en el código tiene su fila

El verificador SHALL leer los `ProjectReference` de **todos** los `.csproj` bajo `backend/src`, sin restringirse a los proyectos `Modules.*`, y SHALL fallar cuando exista una referencia que el manifiesto no declare.

#### Scenario: Una referencia nueva sin fila pone el test en rojo

- **GIVEN** el manifiesto sincronizado con el código
- **WHEN** alguien agrega un `ProjectReference` en cualquier `.csproj` de `backend/src` sin agregar la fila
- **THEN** la verificación falla nombrando origen y destino de la arista no declarada

#### Scenario: El barrido alcanza a los proyectos que no son módulos

- **GIVEN** un proyecto de `backend/src` cuyo nombre no empieza con `Modules.`
- **WHEN** ese proyecto referencia el proyecto interno de un módulo
- **THEN** la arista aparece en el barrido y exige su fila en el manifiesto

#### Scenario: El barrido no puede quedarse vacío

- **GIVEN** el verificador
- **WHEN** el barrido de `backend/src` no encuentra ningún `.csproj`
- **THEN** la verificación falla, en lugar de pasar en verde por no haber mirado nada

### Requirement: Toda fila del manifiesto corresponde a una arista real

El verificador SHALL fallar cuando el manifiesto declare una arista que ningún `.csproj` de `backend/src` referencia.

#### Scenario: Una fila de papel pone el test en rojo

- **GIVEN** una fila del manifiesto cuya referencia se borró del `.csproj`
- **WHEN** corre la verificación
- **THEN** falla nombrando la arista declarada que no existe en el código

### Requirement: Todo proyecto de `backend/src` está clasificado

El manifiesto SHALL enumerar todos los proyectos de `backend/src` y SHALL declarar el estado de cada uno. Un proyecto que ninguna arista alcanza —ni como origen ni como destino— SHALL declararse explícitamente como huérfano y con motivo escrito.

#### Scenario: Un proyecto nuevo sin clasificar pone el test en rojo

- **GIVEN** el manifiesto sincronizado con el código
- **WHEN** aparece un `.csproj` nuevo bajo `backend/src` que el manifiesto no enumera
- **THEN** la verificación falla nombrando el proyecto sin clasificar

#### Scenario: Un proyecto huérfano se declara con motivo

- **GIVEN** un proyecto que ninguna arista alcanza
- **WHEN** se lo declara en el manifiesto
- **THEN** su estado es huérfano
- **AND** lleva un motivo escrito que explica por qué se lo conserva

#### Scenario: Un proyecto declarado que ya no existe pone el test en rojo

- **GIVEN** un proyecto enumerado en el manifiesto
- **WHEN** se borra su `.csproj` de `backend/src`
- **THEN** la verificación falla nombrando el proyecto declarado inexistente

### Requirement: El grafo de proyectos del backend es acíclico

El verificador SHALL comprobar que el grafo formado por los `ProjectReference` reales de `backend/src` no contiene ciclos, cumpliendo el invariante #2. La comprobación SHALL correr sobre las aristas leídas del código, no sobre las declaradas en el manifiesto.

#### Scenario: Un ciclo pone el test en rojo

- **GIVEN** el grafo de proyectos acíclico
- **WHEN** se agrega una referencia que cierra un ciclo
- **THEN** la verificación falla enumerando los proyectos que lo forman

#### Scenario: La aciclicidad se afirma sobre el código

- **GIVEN** un manifiesto que declara un conjunto de aristas sin ciclos
- **WHEN** el código contiene un ciclo que el manifiesto no refleja
- **THEN** la verificación de aciclicidad falla igual

### Requirement: Una excepción a un invariante es una fila con motivo y ticket

Una arista que constituya una excepción declarada a un invariante de arquitectura SHALL registrarse como tal en su propia fila, indicando el invariante al que excede y el ticket que la aprobó. El verificador SHALL fallar cuando una arista se declare como excepción sin motivo escrito o sin ticket.

Una excepción a un invariante MUST NOT documentarse únicamente en prosa.

#### Scenario: Una excepción sin ticket pone el test en rojo

- **GIVEN** una arista declarada como excepción a un invariante
- **WHEN** su fila no indica el ticket que la aprobó
- **THEN** la verificación falla nombrando la arista

#### Scenario: Una excepción sin motivo pone el test en rojo

- **GIVEN** una arista declarada como excepción a un invariante
- **WHEN** su motivo está vacío
- **THEN** la verificación falla nombrando la arista

#### Scenario: Ensanchar una excepción exige su propia fila

- **GIVEN** una excepción ya aprobada para una arista
- **WHEN** alguien agrega una segunda arista amparándose en el mismo invariante
- **THEN** esa segunda arista necesita su propia fila con su propio motivo y su propio ticket

### Requirement: La documentación de arquitectura cita el manifiesto y no lo replica

`docs/architecture/dependency-graph.md` MUST NOT contener una segunda lista de aristas. SHALL citar el manifiesto como fuente única, y todo diagrama que conserve SHALL estar marcado como no normativo.

El procedimiento para agregar una arista SHALL describir la edición del manifiesto, y `/architecture-drift-check` SHALL referenciar el manifiesto en lugar de cruzar el código contra una tabla escrita a mano.

#### Scenario: El documento no tiene una tabla que pueda desincronizarse

- **GIVEN** `docs/architecture/dependency-graph.md`
- **WHEN** un lector busca la lista de aristas vigentes
- **THEN** el documento lo remite al manifiesto
- **AND** no contiene ninguna tabla de aristas propia

#### Scenario: El diagrama se declara no normativo

- **GIVEN** el diagrama del grafo en la documentación
- **WHEN** un lector lo consulta
- **THEN** el documento indica que es un dibujo de orientación y que la lista verificada es el manifiesto

#### Scenario: La pasada de drift lee el manifiesto

- **GIVEN** la skill `/architecture-drift-check`
- **WHEN** se ejecuta su detección de aristas no registradas
- **THEN** cruza el código contra el manifiesto y no contra una tabla markdown
