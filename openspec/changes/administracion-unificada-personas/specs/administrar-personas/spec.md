## ADDED Requirements

### Requirement: Pantalla única de personas

El sistema SHALL ofrecer la pantalla **Administrar personas** en `/personas`, accesible desde el menú Configuración con el permiso `personas.ver` o `personas.ver_ambito`, que reúne a todas las personas con su cuenta, roles y asignaciones. Las rutas `/usuarios` y `/docentes` MUST redirigir a `/personas`, conservando `personaId` cuando venga en la URL.

#### Scenario: Entrada en el menú

- **GIVEN** un usuario con `personas.ver` o `personas.ver_ambito`
- **WHEN** abre el menú lateral
- **THEN** ve la entrada "Personas" en Configuración y no ve "Usuarios" ni "Docentes"

#### Scenario: Ruta anterior

- **WHEN** alguien navega a `/docentes?personaId=X`
- **THEN** el sistema redirige a `/personas/X`

### Requirement: Listado de personas

El listado SHALL mostrar, por persona, Nombre (apellido, nombre), Documento, Legajo, Rol (un badge por rol sin repetir), Asignaciones (badge "código – cargo abreviado"), Estado y Acciones. Cada columna salvo Acciones SHALL tener filtro por encabezado con el componente de filtros del sistema: texto para Nombre, Documento, Legajo y Asignaciones; opciones para Rol y Estado. Nombre, Documento, Legajo y Estado SHALL ordenarse por encabezado con el ciclo ascendente, descendente, sin orden. La fila completa SHALL abrir la persona con mouse o teclado. Una persona sin rol o sin cuenta SHALL verse marcada ("Sin rol", "Sin cuenta") para su corrección.

#### Scenario: Filtrar por rol

- **WHEN** el operador marca "Jefe de Cátedra" en el filtro de Rol
- **THEN** sólo se listan las personas con ese rol y el encabezado muestra el indicador de filtro activo

#### Scenario: Persona heredada sin rol

- **GIVEN** una persona persistida sin membresías de rol
- **WHEN** aparece en el listado
- **THEN** su columna Rol muestra "Sin rol"

### Requirement: Alta y edición a pantalla completa

La alta (`/personas/nueva`) y la edición (`/personas/:personaId`) SHALL ser pantallas completas con dos pestañas: **Datos personales**, que se abre primero, y **Datos docentes**. Datos personales SHALL contener Nombre, Apellido, Documento, Legajo, CUIL, Fecha de nacimiento, UPN y Teléfono. Datos docentes SHALL contener los roles con su ámbito (Docente: materia y carrera; Jefe de Cátedra: materia; Coordinador de Carrera: carrera; roles de departamento: sin ámbito), las asignaciones y el perfil de Mi Portal. Cada asignación SHALL mostrar la materia y la carrera completas en un renglón y debajo Cargo, Dedicación y Horas. Las materias SHALL elegirse con el buscador de materias. Si al guardar hay errores en la pestaña no visible, la pestaña SHALL marcarse y un aviso SHALL indicarlo.

#### Scenario: Abrir una persona

- **WHEN** el operador hace clic en una fila del listado
- **THEN** se abre su edición en la pestaña Datos personales con los datos actuales

#### Scenario: Error en la otra pestaña

- **GIVEN** el operador está en Datos personales y falta un rol completo
- **WHEN** hace clic en "Guardar cambios"
- **THEN** no se guarda, la pestaña Datos docentes queda marcada y se avisa que hay datos incompletos en ella

### Requirement: Rol y cuenta obligatorios

Toda persona creada o editada MUST quedar con al menos un rol con su ámbito completo y con una cuenta (UPN). Las asignaciones MUST ser al menos una completa (materia, carrera, cargo, dedicación y horas > 0) cuando la persona tenga rol Docente o Jefe de Cátedra. La API MUST rechazar un guardado que no cumpla estas reglas. El editor SHALL conservar siempre al menos una fila de rol. No se modifican personas existentes hasta que se editan.

#### Scenario: Alta sin rol

- **WHEN** el operador intenta crear una persona sin rol
- **THEN** se muestra "Toda persona necesita al menos un rol con su ámbito" y no se crea

#### Scenario: Rol de departamento sin asignaciones

- **GIVEN** una persona con rol Secretaría Académica y sin asignaciones
- **WHEN** se guarda
- **THEN** se guarda sin exigir asignaciones

### Requirement: DNI único con aviso inmediato

El documento MUST ser único entre personas, comparando sólo sus dígitos. El editor SHALL avisar mientras se escribe cuando el DNI ya pertenece a otra persona, nombrándola con su legajo e indicando si está inactiva, y SHALL impedir guardar. Al editar, el DNI propio no cuenta como repetido. La API SHALL exponer la verificación y MUST rechazar el duplicado al guardar.

#### Scenario: DNI de otra persona

- **WHEN** el operador escribe "28.456.123" y ya existe Gómez, Laura con documento 28456123
- **THEN** el campo muestra "Ya existe una persona con este DNI: Gómez, Laura (legajo 0412)." y "Crear persona" no guarda

### Requirement: Permisos por rol del operador

`personas.ver` SHALL habilitar el listado y el detalle de todas las personas en sólo lectura; `personas.ver_ambito`, sólo de las personas del ámbito del Jefe de Cátedra. `personas.administrar` SHALL habilitar crear personas, editar datos personales, roles y asignaciones, y activar o desactivar. `portal.administrar` SHALL habilitar corregir el perfil de Mi Portal de otra persona. La migración SHALL asignar `personas.administrar` a Administrativo, Secretaría Académica, Decanato y Administrador de Sistemas; `portal.administrar` a esos cuatro y a Jefe de Cátedra; `personas.ver` a quienes tenían `usuarios.ver` y a Decanato; `personas.ver_ambito` a Jefe de Cátedra. Sin `personas.administrar` la pantalla SHALL mostrar los datos bloqueados, ocultar Nueva persona, Activar/Desactivar y los controles de edición, mostrar "Ver" en lugar de "Editar", reemplazar Guardar por "Volver al listado" e informar qué puede hacer el rol. El backend MUST validar cada permiso en cada operación.

#### Scenario: Jefe de Cátedra

- **GIVEN** un usuario con rol Jefe de Cátedra
- **WHEN** abre una persona de su ámbito
- **THEN** ve los datos personales, roles y asignaciones bloqueados y puede corregir el perfil de Mi Portal

#### Scenario: Mutación sin permiso

- **GIVEN** un usuario sin `personas.administrar`
- **WHEN** envía un `PUT /api/administracion/personas/{id}`
- **THEN** la API responde 403 sin modificar datos

### Requirement: Ámbito del Jefe de Cátedra

Un usuario con `personas.ver_ambito` y sin `personas.ver` SHALL ver únicamente a las personas con designación vigente en materias donde es jefe, y sólo en ellas SHALL poder corregir el perfil de Mi Portal. Fuera de ese ámbito la API MUST responder como recurso inexistente.

#### Scenario: Persona fuera del ámbito

- **GIVEN** un Jefe de Cátedra de la materia A
- **WHEN** consulta una persona designada sólo en la materia B
- **THEN** la API responde 404 y la persona no aparece en el listado

### Requirement: Perfil de Mi Portal en la persona

Al pie de Datos docentes, la edición SHALL mostrar el perfil de Mi Portal de la persona con la misma estructura y componentes que la pantalla Mi Portal: Contacto (filas con ícono, "Sin cargar" si falta), CV (archivo con tamaño y fecha), Experiencia (cronología con lo vigente marcado), Educación, Certificaciones (semáforo de vencimiento), Proyectos (DOI y documento), Habilidades e Intereses; las secciones vacías compactas. El CV y los documentos de proyecto SHALL poder descargarse. Con `portal.administrar` dentro del ámbito, SHALL ofrecer las mismas acciones que Mi Portal (editar contacto, subir y quitar CV, agregar, editar y eliminar ítems con confirmación, editar etiquetas) y cada cambio SHALL guardarse al momento; sin ese permiso, ninguna acción salvo descargar. En el alta SHALL indicarse que el perfil lo completa la persona.

#### Scenario: Corregir una experiencia

- **GIVEN** un operador con `portal.administrar` y una persona con experiencia cargada
- **WHEN** elige Editar en el menú del ítem y guarda
- **THEN** el cambio se persiste en el perfil de la persona y la auditoría registra al operador como autor

#### Scenario: Descargar el CV en sólo lectura

- **GIVEN** un operador sin `portal.administrar`
- **WHEN** hace clic en "Descargar" del CV
- **THEN** recibe el archivo y no ve acciones de edición
