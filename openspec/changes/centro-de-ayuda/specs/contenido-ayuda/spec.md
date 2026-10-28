## ADDED Requirements

### Requirement: Contenido tipado en el repositorio

El contenido de la ayuda SHALL vivir en `frontend/src/features/ayuda/contenido/` como objetos TypeScript tipados registrados en un índice, sin backend ni CMS. Los módulos tienen slug, título, descripción, grupos ordenados y permisos; los artículos tienen slug, módulo, grupo, título, resumen, introducción, permisos, pasos, y opcionalmente palabras clave, "¿Qué sucede después?", preguntas frecuentes y relacionados. Los slugs SHALL escribirse a mano en kebab-case y los relacionados referenciarse como `modulo/articulo`. El contenido y los identificadores propios MUST estar en castellano rioplatense, con voseo, como el resto de la UI.

#### Scenario: Orden de los artículos

- **GIVEN** artículos declarados en un módulo con grupos A y B
- **WHEN** se listan
- **THEN** salen ordenados por grupo y, dentro del grupo, por orden de declaración

### Requirement: Integridad del contenido verificada por test

Una suite de Vitest MUST fallar si: hay slugs de módulo o de artículo repetidos; un artículo referencia un módulo o un grupo inexistente; un relacionado no resuelve a un artículo existente; una captura tiene `alt` vacío o su archivo no existe bajo `frontend/public`; un slug no es kebab-case o es el reservado `buscar`; un permiso declarado no figura en `app/shell/nav.ts`; los permisos de un módulo no cubren la unión de los de sus artículos; o un artículo no tiene pasos.

#### Scenario: Relacionado roto

- **GIVEN** un artículo que relaciona `tareas/inexistente`
- **WHEN** corre la suite
- **THEN** falla indicando el artículo y la referencia

#### Scenario: Captura sin alt

- **GIVEN** una captura con `alt: ""`
- **WHEN** corre la suite
- **THEN** falla

#### Scenario: Permiso inventado

- **GIVEN** un artículo con el permiso `tareas.volar`
- **WHEN** corre la suite
- **THEN** falla porque no existe en la navegación

### Requirement: Cobertura de módulos documentados

El contenido inicial SHALL documentar los módulos con pantallas funcionales en `develop`: Primeros pasos (ingreso y uso general), Mi portal, Tareas, Designaciones y Configuración y sistema (usuarios, docentes, roles, dashboard del sistema y auditoría). MUST NOT documentar el asistente conversacional ni Aulas mientras Aulas sea un placeholder; al incorporarse una pantalla funcional nueva, su documentación se agrega en el mismo change que la entrega.

#### Scenario: Aulas sin documentar

- **WHEN** un usuario con `aulas.ver` busca "reserva de aulas"
- **THEN** no hay resultados de un artículo sobre Aulas

### Requirement: Capturas de entorno descartable

Las capturas SHALL guardarse como archivos estáticos en `frontend/public/ayuda/<modulo>/` y referenciarse con ruta absoluta `/ayuda/<modulo>/<archivo>`. MUST tomarse de la app corriendo contra un entorno descartable sembrado solo con `infra/scripts/seed-data/sintetico.sql`, sin `sga.sql` ni datos reales. Las capturas MUST NOT mostrar correos, nombres ni legajos reales.

#### Scenario: Revisión de una captura

- **GIVEN** una captura nueva en `frontend/public/ayuda/`
- **WHEN** se revisa el PR
- **THEN** solo contiene datos del dataset sintético y su `alt` describe lo que muestra

### Requirement: Actualización del contenido junto con la pantalla

Un cambio que altere el flujo, los textos o los permisos de una pantalla documentada SHALL actualizar su artículo (y sus capturas si dejan de ser fieles) en el mismo diff.

#### Scenario: Cambia un permiso

- **GIVEN** un change que mueve una pantalla a otro permiso
- **WHEN** se revisa
- **THEN** los artículos de esa pantalla declaran el permiso nuevo
