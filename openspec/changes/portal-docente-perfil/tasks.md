## 1. Design spec (previo a todo código)

> Sin prototipo Pencil en este change (decisión del equipo, 2026-09-02). El diseño se especifica en texto; la adhesión a la guía de estilos se mantiene.

- [x] 1.1 Cargar el contexto de estilo: `docs/product/design-principles.md`, los componentes de `@ars-docendi/ui` (`USAGE.md` y el theme de tokens) y el patrón visual de `proyecto-docente-design-spec.md`. No inventar componentes ni estilos nuevos
- [x] 1.2 Escribir `docs/product/designs/portal-docente-design-spec.md` desde `_design-spec-template.md` (invariante #12): layout de las ocho secciones, perfil poblado y vacío, patrón de sección de lista, edición inline de Contacto, widget de tags, estados y mapeo a componentes

## 2. Fundaciones del slice y store mock

- [x] 2.1 Definir en `frontend/src/features/portal/types.ts` los tipos del perfil: perfil read-only, contacto, CV, experiencia, educación (nivel enum), certificación, proyecto y tag
- [x] 2.2 Crear `frontend/src/features/portal/mock/mockStore.ts` con un perfil seed y helpers de lectura/escritura en memoria, reutilizando los datos de identidad y datos institucionales de `features/docentes` sin duplicar campos
- [x] 2.3 Crear el vocabulario mock de tags para Habilidades e Intereses, con soporte de términos sugeridos
- [x] 2.4 Verificar la ruta `/portal` en `frontend/src/app/router.tsx` y la entrada "Mi Portal" en `shell/nav.ts` (ambas ya existen)

## 3. Patrones transversales de la pantalla

- [x] 3.1 Implementar el componente de sección con modo lectura y edición independiente, su propio guardado y confirmación vía `Toast`, sin guardado global
- [x] 3.2 Implementar la presentación compacta de sección vacía y su expansión al tener contenido
- [x] 3.3 Implementar el componente de sección de lista (encabezado con acción, filas, menú kebab ⋮ con Editar/Eliminar) reutilizable por experiencia, educación, certificaciones y proyectos
- [x] 3.4 Implementar el diálogo de confirmación de borrado siguiendo el patrón de `ModalEliminarPeriodo`, sin justificativo
- [x] 3.5 Implementar la página "Mi Portal" componiendo las secciones, con los estados Loading, Error y Success leyendo del store mock

## 4. Perfil y contacto

- [x] 4.1 Implementar el bloque Perfil read-only con `DataList`: nombre, apellido y mail institucional (Azure AD) + DNI, legajo y CUIL (Secretaría), sin control de edición
- [x] 4.2 Implementar la sección Contacto con edición inline de teléfono y mail, ambos opcionales
- [x] 4.3 Implementar la validación de formato del mail de contacto, señalada en el campo y bloqueante solo para esa sección

## 5. CV

- [x] 5.1 Implementar la sección CV vacía como zona de arrastre con `FileUpload`, sin texto explicativo
- [x] 5.2 Implementar la carga de un PDF único con su nombre y fecha, rechazando formatos distintos de PDF
- [x] 5.3 Implementar el reemplazo y la eliminación del CV, dejando referenciado un solo archivo (sin historial)

## 6. Formación

- [x] 6.1 Implementar la sección Educación sobre el patrón de lista: alta, edición y borrado con nivel (enum Grado/Especialización/Maestría/Doctorado), carrera o título, institución y período
- [x] 6.2 Implementar la sección Certificaciones sobre el patrón de lista: alta, edición y borrado con nombre, emisor, fecha y vencimiento opcional

## 7. Trayectoria

- [x] 7.1 Implementar la sección Experiencia sobre el patrón de lista: puesto, organización, período con opción "actual" y descripción
- [x] 7.2 Implementar la sección Proyectos sobre el patrón de lista: nombre, rol, años y descripción
- [x] 7.3 Implementar la documentación del proyecto: PDF adjunto (metadata mock) y/o enlace DOI, ambos opcionales

## 8. Habilidades e intereses

- [x] 8.1 Implementar el widget de tags a nivel feature siguiendo el precedente de `MateriasSelector` / `AsignacionesSelector` (`Select` del vocabulario + lista con "×"), ya que `@ars-docendi/ui` no tiene Tag/Chip/Combobox
- [x] 8.2 Implementar las secciones Habilidades e Intereses como dos listas separadas sobre el mismo widget y el mismo vocabulario
- [x] 8.3 Implementar la opción de sugerir un término nuevo, que queda en la lista del docente marcado como pendiente de incorporarse al vocabulario
- [x] 8.4 Registrar en `docs/quality/tech-debt.md` la deuda del widget de tags: si Secretaría busca por habilidad, debe subir a `ui-lib`

## 9. Tests

- [x] 9.1 Tests de los estados de la pantalla (Loading / Error / Success) y del render del bloque Perfil read-only sin controles de edición
- [x] 9.2 Tests de la presentación compacta de sección vacía y su expansión al cargar el primer ítem
- [x] 9.3 Tests de que guardar una sección no exige ni altera a las demás, y de que no existe guardado global
- [x] 9.4 Tests de alta, edición y borrado en las listas (experiencia, educación, certificaciones, proyectos) contra el store mock
- [x] 9.5 Tests de la validación del formato del mail de contacto
- [x] 9.6 Tests de habilidades e intereses como listas independientes: quitar un tag de una no afecta la otra
- [x] 9.7 Tests del CV: solo PDF, reemplazo sin historial y eliminación

## 10. Cierre

- [x] 10.1 Verificar que no hay fake UI (invariante #7): todas las secciones operan sobre el store mock, sin botones inertes, sin lorem ipsum, sin TODO visible al usuario
- [x] 10.2 Verificar que no hay copy explicativo por sección, ni avisos de "te falta cargar X", ni barra de progreso
- [x] 10.3 Verificar fidelidad al design spec y a los tokens y componentes existentes
- [x] 10.4 Verificar que el grafo de dependencias no cambió: `Modules.Portal` sigue siendo hoja y no hay Contracts nuevos
