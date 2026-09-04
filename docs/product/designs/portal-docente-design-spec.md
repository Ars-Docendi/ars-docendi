---
status: draft # draft | review | approved
owner: "Julian Castellana"
feature: "openspec/changes/portal-docente-perfil/specs/perfil-docente-portal/spec.md"
last_updated: 2026-09-02
---

# Design spec: Portal del docente — autogestión del perfil

## Resumen

Se diseña **"Mi Portal"** (`/portal`), la pantalla donde el docente mantiene su propia información profesional: contacto, CV, educación, certificaciones, experiencia, proyectos, habilidades e intereses. Es el CV del docente convertido en datos consultables, y hoy es además **la primera pantalla que ve al ingresar al sistema** (la raíz redirige ahí). El alcance de esta iteración es solo la cara del docente, sobre datos mockeados.

> **Sin prototipo Pencil en esta iteración** (decisión del equipo, 2026-09-02). El diseño se especifica acá en texto. La adhesión a la guía de estilos se mantiene íntegra: componentes de `@ars-docendi/ui`, tokens del theme, principios de `design-principles.md`.

## Roles que ven esta surface

- [ ] Jefe de Cátedra
- [ ] Coordinador de Carrera
- [ ] Secretaría Académica
- [ ] Decanato
- [ ] Administrativos
- [x] Docente

> El Jefe de Cátedra también es docente y edita su propio perfil, pero lo hace con el rol Docente. Qué ve en `/portal` alguien que no es docente queda como open question.

## Flujo principal

1. El docente ingresa al sistema y aterriza en `/portal`.
2. Ve el bloque **Perfil** ya poblado con su identidad y sus datos institucionales — no cargó nada y el sistema ya lo reconoce.
3. Las secciones sin datos se ven como **filas compactas**; la de CV se ve como **zona de arrastre**.
4. Carga su CV arrastrando el PDF: es la acción de mayor valor por menor esfuerzo, y con eso el perfil deja de estar vacío.
5. Abre una sección cualquiera, la edita y confirma. Un `Toast` avisa que se guardó. La sección vuelve a modo lectura y se expande con su contenido.
6. Vuelve otro día, toca una sola sección y se va. No hay nada que "enviar".

## Layout / IA

Una **sola página scrolleable, sin pestañas**. `Breadcrumbs` (Inicio › Mi Portal) + `PageHeader` con eyebrow mono `PORTAL DOCENTE` y título "Mi Portal", y debajo las ocho secciones en este orden:

| #   | Sección                         | Modo           | Contenido                                                                        |
| --- | ------------------------------- | -------------- | -------------------------------------------------------------------------------- |
| 1   | **Perfil**                      | solo lectura   | Nombre y apellido, mail institucional (Azure AD); DNI, legajo, CUIL (Secretaría) |
| 2   | **Contacto**                    | edición inline | Teléfono, mail                                                                   |
| 3   | **CV**                          | archivo único  | PDF, reemplazable                                                                |
| 4   | **Experiencia**                 | lista          | Puesto, organización, período (con "actual"), descripción                        |
| 5   | **Educación**                   | lista          | Nivel, carrera o título, institución, período                                    |
| 6   | **Certificaciones**             | lista          | Nombre, emisor, fecha, vencimiento opcional                                      |
| 7   | **Proyectos**                   | lista          | Nombre, rol, años, descripción, PDF o DOI                                        |
| 8   | **Habilidades** / **Intereses** | tags           | Dos listas separadas, mismo vocabulario                                          |

El orden va de lo más estable e identitario a lo más discrecional, con lo que más se edita (Contacto) y lo de mayor valor por esfuerzo (CV) arriba.

### Anatomía de una sección

```
┌ Nombre de la sección ──────────────────  [acción] ─┐
│  contenido en modo lectura                         │
└────────────────────────────────────────────────────┘
```

- **Encabezado**: nombre + control de acción a la derecha (`Editar` en Contacto y tags, `+ Agregar` en las listas, `Reemplazar` en CV). El bloque Perfil **no tiene ninguno** — así se comunica que es de solo lectura, sin una línea de texto que lo explique.
- **Sección de lista**: cada fila muestra el ítem resumido en una línea y termina en un menú kebab ⋮ con Editar y Eliminar, siguiendo el patrón transversal ya usado en Designaciones.
- **Ancho**: el mismo que la tarjeta del formulario de pedido (máx. 860 px), para no romper la cohesión con el resto del sistema.

### Sección vacía

Una sección sin datos ocupa **una fila**, no una tarjeta hueca:

```
  Experiencia                                      +
```

Al cargarse el primer ítem, se expande a su presentación completa. Consecuencia buscada: el perfil vacío entra en una sola pantalla, y **la página crece con el perfil** — el avance se ve sin barra de progreso ni porcentaje.

La excepción es **CV**, que vacío se presenta como zona de arrastre (`FileUpload`): la forma explica la acción sin texto.

### Perfil vacío (primer ingreso)

```
Inicio › Mi Portal
PORTAL DOCENTE
Mi Portal

┌ Perfil ─────────────────────────────────┐
│ Nombre               Marina Díaz        │
│ Mail institucional   marina.diaz@…      │
│ DNI                  31.089.234         │
│ Legajo               0033               │
│ CUIL                 27-31089234-8      │
└─────────────────────────────────────────┘
┌ CV ─────────────────────────────────────┐
│    arrastrá tu CV en PDF acá            │
└─────────────────────────────────────────┘
  Contacto                               +
  Experiencia                            +
  Educación                              +
  Certificaciones                        +
  Proyectos                              +
  Habilidades                            +
  Intereses                              +
```

### Edición

- **Contacto**: edición **inline** dentro de la tarjeta (dos campos; es lo que más se edita). `Field` + `Input`, con Guardar y Cancelar.
- **Listas**: alta y edición en `Modal`, siguiendo `ModalNuevoDocente` / `ModalEditarDocente`.
- **Borrado**: `Modal` de confirmación con el patrón de `ModalEliminarPeriodo` — qué se borra, aviso de que no se puede deshacer, sin justificativo.
- **Tags**: selector compuesto a nivel feature (no existe en la librería), con `Select` del vocabulario, lista de tags con "×" y opción de sugerir un término nuevo.

## Estados a diseñar

| Estado            | Descripción                                                             | Cuándo se muestra                         |
| ----------------- | ----------------------------------------------------------------------- | ----------------------------------------- |
| Loading           | "Cargando tu perfil…"                                                   | Carga inicial del perfil                  |
| Empty             | Por sección: fila compacta con su control de alta. CV: zona de arrastre | La sección no tiene datos                 |
| Error             | `InlineAlert` accionable, sin romper la navegación                      | Falla la lectura del perfil o un guardado |
| Success           | Perfil en modo lectura con sus secciones expandidas                     | Estado normal                             |
| Guardado          | `Toast` "Cambios guardados"                                             | Al confirmar la edición de una sección    |
| Awaiting approval | _No aplica._ Nada del Portal entra en un circuito de aprobación         | —                                         |

## Decisiones de diseño

- **Perfil vivo, no formulario.** Lectura por defecto y edición **por sección**, cada una con su guardado. Sin "Guardar" global al pie. El resto del sistema es transaccional (el pedido de designación se completa, se envía y entra a un circuito); el Portal se visita muchas veces para tocar una sola cosa. Con guardado global, quien entra a corregir su teléfono recorre siete secciones para llegar al botón, y apretarlo se siente como enviar el perfil entero.
- **Nada es obligatorio y nada bloquea.** La única validación es el formato del mail de contacto, y solo frena esa sección.
- **Sin copy explicativo.** No hay textos de ayuda por sección, ni avisos de "todavía no cargaste X", ni notas explicando por qué un campo no se edita. Lo read-only se comunica por **ausencia de afordancia**; las etiquetas desambiguan solas ("Mail institucional" en Perfil vs "Mail" en Contacto).
- **Sin indicador de completitud.** ¿Cuántas certificaciones son "completo"? Solo Contacto y CV tienen un final definible; un porcentaje sería precisión falsa. El largo de la página ya comunica el avance.
- **Sin pestañas.** Reducirían el scroll pero esconderían justo lo que se necesita ver —los huecos—, y el problema del Departamento es que los docentes no cargan nada.
- **El estado manda, no la visita.** Todo depende de si la sección tiene datos, nunca de si es el primer ingreso. No hay modo "primera vez" ni estado de onboarding que persistir, y el día que exista un dashboard el Portal se muda al menú sin cambios.
- **Habilidades e intereses separados.** Ante una vacante son señales distintas: quién puede tomarla ya y quién la tomaría formándose. Una sola lista pierde la distinción.
- **Un patrón de lista reutilizado** para Experiencia, Educación, Certificaciones y Proyectos, en vez de cuatro secciones a medida.
- **El bloque Perfil nunca está vacío.** Viene precargado de Azure AD y Secretaría, así que lo primero que ve un docente nuevo es su nombre y su legajo correctos — mejor primera impresión que un formulario en blanco.

## Anti-patterns a evitar (específicos de esta feature)

- **Copiar el formulario de pedido de designación**: tarjeta única con guardado al pie y validación bloqueante. Es el patrón correcto para un trámite y el incorrecto para un perfil.
- **Agregar textos de ayuda** para explicar qué hace cada sección o por qué un campo no se edita. Si hace falta explicarlo, el control está mal elegido.
- **Avisos de completitud** del tipo "te falta cargar tu CV": duplican lo que ya se ve en la página.
- **Botones inertes** (invariante #7): toda acción debe operar sobre el store mock — agregar, editar y borrar reflejan estado.
- **Simular que los adjuntos se suben a un servidor**: son metadata mock, igual que en pedidos de designación.
- **Inventar componentes o estilos**: todo sale de `@ars-docendi/ui` y sus tokens, salvo el selector de tags, que se compone con piezas existentes.

## Mapeo a componentes

| Bloque                  | Componentes de `@ars-docendi/ui`                               |
| ----------------------- | -------------------------------------------------------------- |
| Encabezado de página    | `Breadcrumbs` · `PageHeader` (de `shared/ui`)                  |
| Perfil (solo lectura)   | `DataList`                                                     |
| Contacto en edición     | `Field` · `Input` · `Button`                                   |
| Formularios de ítem     | `Field` · `Input` · `Select` · `DatePicker` · `Textarea`       |
| CV y adjuntos           | `FileUpload`                                                   |
| Alta, edición y borrado | `Modal`                                                        |
| Avisos y confirmaciones | `InlineAlert` · `Toast`                                        |
| Tags                    | **No existe.** Se compone a nivel feature con `Select` + lista |

La librería no tiene `Tag`, `Chip`, `Combobox` ni `MultiSelect`. El selector de tags se arma dentro de la feature siguiendo el precedente de `MateriasSelector` y `AsignacionesSelector` en `features/docentes`. Si más adelante Secretaría busca docentes por habilidad, va a necesitar el mismo widget y ahí conviene subirlo a `ui-lib` — queda anotado en `docs/quality/tech-debt.md`.

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- [`docs/product/designs/proyecto-docente-design-spec.md`](proyecto-docente-design-spec.md) — patrón visual de referencia
- Specs funcionales: [`openspec/changes/portal-docente-perfil/specs/`](../../../openspec/changes/portal-docente-perfil/specs/)
- Diseño técnico: [`openspec/changes/portal-docente-perfil/design.md`](../../../openspec/changes/portal-docente-perfil/design.md)

## Open questions de diseño

- ~~**Vocabulario de habilidades e intereses.**~~ **Resuelto el 2026-09-03:** no se cura un catálogo de antemano —hay demasiadas especialidades de ingeniería— sino que el vocabulario se forma por uso, con **autocompletado sobre lo ya cargado** por otros docentes. Ver D13 en el design del change. La pantalla de esta iteración todavía usa el `Select` de catálogo; la migración del widget está registrada como TD-007.
- **Se le pide el CV dos veces**, subido y cargado a mano. Es la causa típica de que estos módulos queden vacíos. Se mitiga poniendo el CV como puerta de entrada, pero la redundancia queda.
- **Quién consume estos datos y cuándo** (concursos, categorización, informes a la Facultad). El uso define qué campos importan de verdad.
- **Qué ve en `/portal` alguien que no es docente**, por ejemplo un administrativo de Secretaría. La raíz redirige ahí para los seis perfiles de rol.
- **Sin copy explicativo, un dato institucional mal cargado deja al docente sin saber a quién reclamarle.** Aceptado a cambio de una interfaz limpia; reversible con una línea de texto si aparece como problema real.
