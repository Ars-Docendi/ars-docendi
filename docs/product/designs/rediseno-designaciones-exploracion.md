---
status: exploration # exploration | draft | review | approved
owner: "Julian Castellana"
branch: feature/rediseno-designaciones-ui
last_updated: 2026-07-06
---

# Exploración: rediseño de nuevas designaciones

Captura de las decisiones y el mapa de cambios acordados en `/opsx:explore` (2026-07-05),
como referencia compartida **antes de mockear en Pencil** (`docs/product/designs/screens.pen`)
y de abrir los changes OpenSpec correspondientes.

> **No es una spec de feature todavía.** Es el documento de exploración que agrupa un lote de
> cambios de diseño en temas, fija las decisiones estructurales y lista las preguntas abiertas.
> Cada tema derivará luego en su propio design-spec + change OpenSpec.

## Origen

Lote de cambios de diseño sobre el flujo de designaciones, agrupados por el cliente en pantallas:
Mis Pedidos (inicio + form de carga), Datos Docente, Revisión (dedicaciones), reglas de negocio
transversales y una pantalla nueva de Historial. El detalle crudo original quedó en el prompt de
exploración; este doc lo reorganiza por **tema** (los cambios cruzan varias pantallas).

## Estado de los comentarios del profesor (checklist trazable)

Cada bullet es tal como lo trajo el profesor/cliente, mapeado 1:1 a su estado real. Leyenda:
**✅ Mockeado** (ya se ve en `screens.pen`) · **📝 Decidido** (hay decisión/BR, falta dibujarlo) ·
**⏳ Sin decidir** (todavía abierto).

**Pantalla Mis Pedidos — Inicio**

- ⏳ Alerta de tiempo de períodos activos → tema G, no mockeado, sin BR redactada todavía.
- ⏳ Doble click para abrir además de los 3 puntitos → micro-UX, no mockeado.

**Cargando un pedido — Todo tipo**

- ✅ Horas de investigación → mockeado en Alta (`n1zz2M`) y Cambio (`tZANr`).
- ⏳ Modificar horas docente → es la pantalla **Datos Docente**, no tocada todavía.

**Alta**

- ✅ Selección de múltiples materias → mockeado en `n1zz2M` (2 filas materia+horas + "Agregar materia").
- ✅ Horas de materia / investigación / externas → mockeado en `n1zz2M`.
- ⏳ Usuarios que son Jefe de Cátedra y Docente a la vez → parkeado (P3); el modelo de datos ya lo
  soporta (`roles: RolDocente[]` en `admin-docentes`), falta decidir el comportamiento (conflicto de
  interés al autocargarse un pedido).

**Baja**

- 📝 Tipificado de la baja → **decidido**: enum cerrado Renuncia/Jubilación/Otro. **No mockeado**
  (el frame `JOHDw` "Pedido (Baja)" no se tocó en esta sesión).

**Cambio**

- 📝 No se puede bajar el cargo, solo superiores → **decidido**: BR + jerarquía de 7 cargos. **No
  mockeado** (no hay tratamiento visual de la restricción en `tZANr` — la validación es de dominio,
  se podría reflejar con un hint o deshabilitando opciones inferiores del Select).
- ✅ Horas de materia / investigación / externas → mockeado en `tZANr` (y de paso se completó
  "Dedicación solicitada", que faltaba desde antes).

**Pantalla Datos Docente**

- 📝 Cargo y dedicación únicos → decidido (D1), pero la pantalla **Datos Docente no se tocó** —
  sigue sin mockear.
- ⏳ Modificación de horas externas / de investigación → no mockeado (misma pantalla).

**Reglas de negocio**

- ✅ Check depto externo → **resuelto distinto de lo planteado**: en vez de reinterpretar el toggle,
  se **eliminó** y se reemplazó por el campo explícito "Horas externas" (mockeado en Alta y Cambio).
- 📝 Restricción de período abierto → decidida como BR candidata (tema G), **no mockeada**.

**Nueva — Historial de pedidos**

- 📝 Pantalla nueva de historial → dirección elegida (submódulo "Historial de designaciones": ver
  todas las designaciones propias, entrar a una para ver el detalle del período). **No hay ni frame
  todavía en `screens.pen`** — arranca de cero.

**Pantalla Revisión (Dedicaciones)**

- ⏳ Mostrar bien el tipo de cambio (alta/baja/cambio) → no mockeado.
- ⏳ Estado más cerca del título / más visible → no mockeado.
- 📝 Sacar el prioritario si cargo mayor → decidido como BR (P1, falta solo el umbral >/≥), **no
  mockeado** (no hay affordance de "quitar prioritario" en la UI todavía).
- ⏳ Simplificar bastante la info → no mockeado.
- ⏳ Agregar botón de Volver → no mockeado (trivial).
- 📝 Vista modo grilla para todos → **decidido**: se elimina el Kanban, queda solo grilla/tabla y
  desaparece el switcher. **No mockeado**: los frames `q6OrQB`/`kWSjh`/`Z0S9T` (tablero, 2 variantes)
  y `ebl4U` (tabla) siguen como estaban: falta unificarlos en un solo diseño de grilla y limpiar/archivar
  las variantes de Kanban que quedan obsoletas.

**Resumen:** de todo el lote, **solo el form de pedido (Alta + Cambio, temas A+B) está mockeado**.
Todo lo demás (temas C, D-Baja, E, F, G, y la pantalla Datos Docente) tiene la decisión tomada pero
falta dibujarlo en Pencil.

## Decisiones estructurales (cerradas)

Estas tres gobiernan cómo se dibujan todas las pantallas. Dos quedaron cerradas; la tercera es
consecuencia directa.

### D1 — Cargo y dedicación son únicos por docente

Un docente tiene **un solo cargo** y **una sola dedicación**, que aplican a **todas** sus materias.
Puede estar asignado a 1 o varias materias, y en cada materia tiene **X horas** distintas, pero el
cargo y la dedicación no varían por materia. Es la lógica del modelo de negocio de la facultad.

- **Tensión con `admin-docentes` (a reconciliar, NO ahora):** el change en vuelo `admin-docentes`
  modeló `AsignacionMateria = { materia, cargo, horas }`, es decir **cargo por materia**. Eso
  contradice D1. Decisión explícita del cliente: **no tocar `admin-docentes`** en esta iteración.
  El `cargo` per-materia de `AsignacionMateria` queda como deuda de reconciliación — a resolver
  cuando esta línea de trabajo baje a código (registrar en `docs/quality/tech-debt.md` en ese
  momento). Para el rediseño de designaciones, el modelo canónico es: **cargo + dedicación a nivel
  docente; horas a nivel materia.**

### D2 — Las horas son campos libres (no cierran contra la dedicación)

La dedicación (Simple / Semiexclusiva / Exclusiva) **no** fija un techo que la suma de horas deba
respetar. Las horas de materia + investigación + externas se cargan sueltas, sin validación de
cierre contra la dedicación. Simplifica el form (menos validación cruzada).

### D3 — Múltiples materias dentro de un mismo pedido

"Selección de múltiples materias" = varias asignaciones (materia + horas) **dentro de un pedido**,
no un pedido por materia. Esto **no rompe** BR-designaciones-001 ("un pedido por docente por
período"): sigue habiendo un pedido por docente; lo que cambia es que ese pedido puede referir a
varias materias.

## El modelo de horas (keystone)

Casi la mitad del lote es el mismo cambio asomando en pantallas distintas. Modelo resultante de
D1–D3:

```
Docente
 ├── cargo        (único — CARGOS_DOCENTES, jerárquicos; ver "Jerarquía de cargos")
 ├── dedicación   (única — Simple / Semiexclusiva / Exclusiva)
 └── horas (libres, sin cierre contra dedicación)
       ├── horas de materia      · Materia A: Xh
       │                         · Materia B: Yh   ← múltiples materias (D3)
       ├── horas de investigación
       └── horas externas (otro departamento)
```

Superficies afectadas por este modelo: form de pedido (todo tipo → horas de investigación y
modificar horas docente; Alta/Cambio → horas de materia, investigación y externas) y Datos Docente
(modificar horas externas y de investigación).

### Mockup en Pencil (2026-07-06)

Temas A+B mockeados en `screens.pen` sobre los frames existentes del form de pedido. Capturas en
`exports/rediseno-designaciones-ui/` para revisar sin abrir Pencil:

- **`n1zz2M`** ("Designaciones - Pedido (Alta)") — [pedido-alta.png](./exports/rediseno-designaciones-ui/pedido-alta.png):
  sección "Designación solicitada" reordenada
  (Cargo + Dedicación solicitados primero, docente-level) seguida de una nueva subsección
  **"Materias y horas asignadas"** — lista de filas repetibles (Select materia + Input horas; 2 filas
  de ejemplo: Programación I · 6h, Programación II · 4h) + botón ghost "Agregar materia" — y una fila
  nueva **Horas de investigación / Horas externas (otro depto.)**. Frame reajustado en altura
  (1160→1330) para evitar recorte del `formCard`.
- **`tZANr`** ("Designaciones - Pedido (Editar · Cambio)") — [pedido-editar-cambio.png](./exports/rediseno-designaciones-ui/pedido-editar-cambio.png):
  fila nueva **Horas · {materia actual} / Horas de investigación / Horas externas (otro depto.)**
  agregada debajo de "Cargo solicitado". No se agregó lista de múltiples materias acá (D3/"selección
  de múltiples materias" es un ítem propio de Alta; Cambio solo pedía exponer las horas).
- **Pendiente de mockear (tema A, no bloqueante):** "modificar horas docente" y las horas de
  investigación/externas en la pantalla **Datos Docente** (fuera de este lote, es otra pantalla).
- **Fix (mismo día) — faltaba "Dedicación solicitada" en Cambio.** El frame `tZANr` solo tenía
  "Cargo solicitado" en la fila de "Designación solicitada"; le faltaba el Select de **Dedicación
  solicitada**. No fue algo que se sacó en este lote — ya faltaba en el mockup antes. Se detectó al
  revisar el código real ya implementado (`frontend/src/features/designaciones/components/
SeccionDesignacionSolicitada.tsx` + `DatosActualesPanel.tsx`): ahí **Cargo y Dedicación
  solicitados se editan juntos también en Cambio** (la materia es lo único condicional a Alta), y la
  transición "Cat. 3 → Cat. 2" del panel de datos actuales se calcula reactivamente a partir de ese
  Select. Se agregó el campo al mockup para que quede consistente con el código real. **Confirmado
  por el cliente: la dedicación se puede subir o bajar libremente, sin restricción** (a diferencia
  del cargo, que solo sube — tema C).
- **Hallazgo — catálogo real de "Dedicación" (`Categoría N`) necesita agregar Categoría 0.** El
  código real (`frontend/src/features/designaciones/api/catalogos.ts` → `DEDICACIONES`) hoy define
  **Categoría 1 a 6** (6 valores). El cliente pidió agregar **Categoría 0**, quedando el catálogo en
  **0 a 6** (7 valores). Es un cambio de catálogo en código (no solo de mockup) — a aplicar cuando
  esta línea de trabajo baje a implementación.
- **Nota — fragmentación del catálogo de cargos entre 3 fuentes.** De paso se detectó que "cargo"
  hoy vive en tres catálogos distintos y **no coinciden entre sí**: `designaciones/api/catalogos.ts`
  → `CARGOS` (4: Titular, Adjunto, JTP, Ayudante); `docentes/mock/mockStore.ts` → `CARGOS_DOCENTES`
  (6, con Primera/Segunda separadas); y la jerarquía de 7 que definimos en este documento (suma
  "Ayudante alumno"). Al implementar el tema C (jerarquía de cargos) va a hacer falta reconciliar
  estas tres listas en una sola fuente de verdad — sumarlo a la deuda de reconciliación junto con
  D1/`admin-docentes`.
- **Actualización (mismo día):** se **eliminó el toggle** "El docente hace más horas en otro
  Departamento (se gestiona como externo)" de ambos frames (Alta y Cambio) — quedaba redundante una
  vez que el campo numérico **Horas externas** es explícito. Esto **resuelve** (ya no queda parkeada)
  la aclaración de semántica del tema D: en vez de reinterpretar el toggle, se lo reemplaza
  directamente por el campo de horas.

## Mapa de temas

| #     | Tema                                                                                                                                                       | Pantallas                                      | Estado                                                                                             | Agrupación (change)          |
| ----- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------- | -------------------------------------------------------------------------------------------------- | ---------------------------- |
| **A** | **Modelo de horas** (materia / investigación / externas; modificar horas docente)                                                                          | Form (todo tipo, Alta, Cambio) + Datos Docente | ✅ Mockeado en Pencil (Alta + Cambio). Falta Datos Docente y "modificar horas docente"             | Propio (el más profundo)     |
| **B** | **Múltiples materias en el pedido** (D3)                                                                                                                   | Form Alta/Cambio                               | ✅ Mockeado en Pencil (Alta)                                                                       | Junto con A                  |
| **C** | **Jerarquía de cargos** (cambio solo a cargo superior; quitar prioritario si tenés cargo mayor)                                                            | Form Cambio + Revisión                         | Orden en `CARGOS_DOCENTES`; se define ladder de **7** (suma "Ayudante alumno") + se hace explícito | BRs nuevas + orden de cargos |
| **D** | **Tipificaciones** (tipo de baja: Renuncia/Jubilación/Otro; check "depto externo" = solo menciona que trabaja en otro depto)                               | Form Baja + toggle depto                       | Enum de baja nuevo (cerrado); el toggle se deja como está por ahora                                | Con el form                  |
| **E** | **Rediseño Revisión** (tipo de cambio visible; estado cerca del título; simplificar info; botón Volver; **solo grilla, sin switcher**; quitar prioritario) | Revisión (grilla) + Detalle                    | Se **elimina Kanban** → solo grilla; + regla nueva de quitar prioritario                           | UX-only, propio              |
| **F** | **Historial de pedidos** (pantalla nueva)                                                                                                                  | Nueva                                          | Nuevo (distinto del AuditLog por-pedido ya existente)                                              | Propio                       |
| **G** | **Período abierto** (alerta de tiempo en Mis Pedidos; restricción de carga fuera de período)                                                               | Mis Pedidos (inicio)                           | Conecta con `gestion-periodos`; la restricción es regla nueva                                      | Con Mis Pedidos              |
| —     | **Doble click para abrir** (además del kebab ⋮)                                                                                                            | Mis Pedidos                                    | Micro-UX                                                                                           | Trivial, con E o G           |

### Detalle por pantalla (crudo del cliente, mapeado a tema)

- **Mis Pedidos — Inicio:** alerta de tiempo de períodos activos [G] · doble click para abrir además del kebab [micro-UX].
- **Mis Pedidos — Form, todo tipo:** añadir horas de investigación [A] · modificar horas docente [A].
- **Form — Alta:** selección de múltiples materias [B] · faltan horas de materia, investigación y externas [A] · manejo de usuarios que son JC y Docente a la vez [ver "Cruces"].
- **Form — Baja:** tipificado de la baja (renuncia, jubilación, etc.) [D].
- **Form — Cambio:** no se puede bajar el cargo, solo superiores [C] · faltan horas de materia, investigación y externas [A].
- **Datos Docente:** cargo y dedicación únicos [D1] · modificación de horas externas [A] · modificación de horas de investigación [A].
- **Reglas de negocio:** check depto externo = solo menciona que trabaja en otro depto [D] · restricción de período abierto [G].
- **Revisión (Dedicaciones):** mostrar bien el tipo de cambio (alta/baja/cambio) · estado más cerca del título / más visible · quitar prioritario si tenés cargo mayor que quien lo puso [C] · simplificar la info · botón Volver · vista modo grilla para todos [todo E].

## Reglas de negocio nuevas (candidatas a BR-designaciones-\*)

A registrar en `docs/business-rules/designaciones.md` cuando se aprueben los changes (invariante #11,
con cita normativa a confirmar con el cliente):

- **BR nueva — Cambio de cargo solo hacia arriba.** "Cambio de cargo o dedicación" solo puede proponer
  un cargo **superior**; no se puede bajar. Usa la jerarquía de cargos (ver abajo).
- **BR nueva — Quitar prioritario acotado por cargo (P1).** Solo puede sacar el flag _prioritario_ quien
  tenga un **cargo docente superior** a quien lo marcó. Decisión de escala (ronda 2): se compara por el
  **cargo académico de la persona** (Titular, Adjunto…), **no** por el rol de sistema (Coordinador,
  Secretaría…) — misma jerarquía que "cambio hacia arriba". Implicación de modelo: el actor que revisa
  debe **llevar su cargo docente** (hoy el flujo lo maneja por rol). Falta cerrar: si el umbral es
  estricto (>) o incluye igual (≥).
- **BR nueva — Carga acotada al período abierto.** No se pueden crear/enviar pedidos fuera del
  período abierto de designaciones (conecta con `gestion-periodos`; ver tema G).
- **✅ Resuelto — check "depto externo".** Se **eliminó el toggle** "hace más horas en otro
  Departamento"; queda reemplazado por el campo explícito **Horas externas** (ver "Mockup en
  Pencil"). Ya no queda ambigüedad de semántica: las horas externas se cargan como número, no como
  flag.

**Jerarquía de cargos (a confirmar por el cliente)** — de menor a mayor:

```
1. Ayudante alumno        ▼ menor
2. Ayudante de Segunda
3. Ayudante de Primera
4. Jefe de Trabajos Prácticos (JTP)
5. Profesor Adjunto
6. Profesor Asociado
7. Profesor Titular       ▲ mayor
```

⚠️ Difiere del catálogo actual `CARGOS_DOCENTES` (6 cargos) en dos puntos: suma **"Ayudante alumno"**
(⇒ hay que extender el catálogo en `admin-docentes/mock/mockStore.ts`) y fija **Segunda por debajo de
Primera** (convención UNLaM). Un cargo es "superior" si tiene índice mayor en esta escala.

## Cruces con changes en vuelo

- **`admin-docentes` (22/42):** ya tiene múltiples materias con horas (`AsignacionMateria[]`) y
  `roles: RolDocente[]` (array — **ya soporta ser JC y Docente a la vez**, ver P3). Ver tensión D1
  (cargo per-materia vs cargo único).
- **`roles-membresia` (0/31):** eje de identidad/roles. El ítem _"usuarios que son JC y Docente a la
  vez"_ vive acá, no en designaciones — es un tema de identidad (una persona con dos roles), no del
  flujo de pedidos. A resolver junto con el modelo de roles.
- **`gestion-periodos` (spec vigente):** fuente del "período abierto" para los temas A/G.

## Preguntas abiertas — estado

### Decidido (ronda 2, 2026-07-05)

- **Jerarquía de cargos (C):** ladder de **7** cargos (menor→mayor): Ayudante alumno < Ay. de Segunda <
  Ay. de Primera < JTP < Adjunto < Asociado < Titular. **A confirmar por el cliente.** Se hace explícito
  y autoritativo; extiende el catálogo actual (suma "Ayudante alumno", fija Segunda < Primera). Ver
  "Reglas de negocio nuevas".
- **P1 — escala de "quitar prioritario" (C):** se compara por **cargo docente de la persona**, no por
  rol de sistema. Misma jerarquía de cargos de arriba. (Queda pendiente solo el umbral > vs ≥.)
- **Tipos de baja (D):** enum cerrado = **Renuncia · Jubilación · Otro** (Otro con texto libre).
- **Vista de Revisión (E):** se **elimina el formato Kanban**. La Revisión queda **solo grilla/tabla**
  y **desaparece el switcher** Tabla/Tablero. ⚠️ Revierte la decisión "opción D" del design-spec
  vigente — al bajar a código hay que actualizar `proyecto-docente-design-spec.md` y la spec
  `tablero-revision-tabla`.
- **Semántica check "depto externo" (D):** resuelto — toggle **eliminado**, reemplazado por el campo
  **Horas externas** (ver "Mockup en Pencil", actualización del mismo día).

### Pendiente

- **P4 — Historial (F):** dirección elegida = **submódulo nuevo "Historial de designaciones"**: el
  docente ve **todas las designaciones que hizo** y entra a una para ver las de ese período. Falta:
  alcance por rol (¿solo propio o depto-wide para revisores?), filtros, y si reusa la tabla de Mis
  Pedidos. A explorar/diseñar en Pencil.
- **Reconciliación `admin-docentes`:** cuándo y cómo alinear `AsignacionMateria.cargo` con D1.

### Parking lot (definido más adelante, no bloquea el mockup)

- **P1 — umbral de "quitar prioritario":** definido que se compara por cargo docente (ver arriba).
  Falta solo si el umbral es estricto (>) o incluye igual (≥) — si un par puede sacar el flag de otro
  par del mismo cargo.
- **Confirmación cliente — jerarquía de cargos:** validar el ladder de 7 con el cliente (sobre todo
  "Ayudante alumno" como cargo aparte y Segunda < Primera).
- **P3 — JC + Docente (identidad):** ¿un JC puede cargar un pedido para sí mismo? ¿Conflicto de
  interés a marcar/bloquear? El modelo ya lo soporta (`roles[]`).

## Cómo seguir en Pencil (handoff)

Para quien continúe el mockup en `docs/product/designs/screens.pen`:

**Frames ya tocados (temas A+B, terminados):**

- `n1zz2M` — "Designaciones - Pedido (Alta)"
- `tZANr` — "Designaciones - Pedido (Editar · Cambio)"

**Frames a tocar para lo que sigue (en orden sugerido, de menor a mayor esfuerzo):**

1. **Tema G + micro-UX** en `m3xg` ("Designaciones - Mis pedidos"): agregar alerta de tiempo de
   período activo + soporte visual de doble-click (puede ser solo una nota, el doble-click no se ve
   en un mockup estático).
2. **Tema D (Baja)** en `JOHDw` ("Pedido (Baja)"): agregar el Select de tipificación
   (Renuncia/Jubilación/Otro) — mismo patrón de campo que "Cargo solicitado" en los otros frames.
3. **Tema C** en `tZANr` (Cambio): reflejar visualmente "no bajar de cargo" (ej. opciones inferiores
   del Select deshabilitadas/grisadas, o un hint bajo el campo).
4. **Tema E (Revisión → solo grilla)**: el más grande. Frames existentes a **reconciliar**:
   `q6OrQB` (Tablero de revisión, vigente), `kWSjh` / `Z0S9T` (2 variantes de tablero descartadas en
   su momento, opciones C/D) y `ebl4U` (Tabla/grilla, vigente). Como se elimina el Kanban, `ebl4U` es
   el punto de partida; probablemente convenga **copiar `ebl4U` a un frame nuevo** (no editar el
   original hasta confirmar) e iterar ahí: sacar tipo de cambio más visible, estado cerca del título,
   simplificar info, botón Volver, y la acción de "quitar prioritario" (tema C/P1) en el detalle
   (`hcCfk`, "Revisión de novedad"). Al terminar, marcar `q6OrQB`/`kWSjh`/`Z0S9T` como obsoletos (no
   borrar todavía — dejarlos de referencia hasta el PR).
5. **Tema F (Historial)**: no hay frame de partida. Usar `FindEmptySpace` (ver ejemplo en la sección
   "batch_design API" del propio Pencil) anclado a algún frame de Designaciones existente, y armar
   la pantalla desde cero reusando los componentes del design system (`Table`/`DataList`,
   `Breadcrumbs`, `PageHeader` pattern) — mismo patrón que ya usan las demás pantallas del módulo.
6. **Datos Docente**: no existe frame en `screens.pen` todavía para esta pantalla en absoluto — hay
   que crearla si se va a mockear "modificar horas docente" (A) y "cargo/dedicación únicos" (D1) ahí.

**Tips de flujo aprendidos en esta sesión:**

- Llamar `get_editor_state(include_schema: true)` **una sola vez** al empezar si no tenés el schema
  en contexto — trae la API completa de `batch_design` (`Insert`/`Update`/`Replace`/`Move`/`Delete`/
  `FindEmptySpace`).
- Antes de editar un frame de pantalla completa, `Update(frameId, {placeholder: true})`; al terminar,
  `placeholder: false`.
- **Siempre** correr `snapshot_layout({parentId: frameId, problemsOnly: true})` después de agregar
  contenido. Si devuelve `"partially clipped"` en el `formCard` (o similar), el frame raíz tiene
  `clip: true` con altura fija — hay que subirle la `height` (mirá cuánto necesita con
  `snapshot_layout({parentId: ..., maxDepth: 1})` antes de decidir el número).
- `export_nodes` a veces tira `MCP error -32603` con arrays de varios `nodeIds` — si pasa, reintentar
  de a un `nodeId` por llamada.
- **Cruzá el mockup contra el código real** en `frontend/src/features/designaciones/` antes de
  inventar un layout — varias pantallas de este módulo (Alta/Cambio del form, panel de datos
  actuales) **ya están implementadas** (`SeccionDesignacionSolicitada.tsx`, `DatosActualesPanel.tsx`,
  `catalogos.ts`), no son solo mockup. El mockup en Pencil a veces está desactualizado respecto al
  código (así se encontró el "Dedicación solicitada" faltante en Cambio).
- Catálogos con inconsistencias detectadas a tener en cuenta si el mockup necesita mostrar valores
  de ejemplo: `CARGOS` (4, en `designaciones/api/catalogos.ts`) vs `CARGOS_DOCENTES` (6, en
  `docentes/mock/mockStore.ts`) vs la jerarquía de 7 de este doc; `DEDICACIONES` (hoy 1–6, falta
  agregar Categoría 0).

## Referencias

- Design-spec vigente del flujo: [`proyecto-docente-design-spec.md`](./proyecto-docente-design-spec.md)
- Mockups: [`screens.pen`](./screens.pen)
- Business rules: [`docs/business-rules/designaciones.md`](../../business-rules/designaciones.md)
- Spec funcional pedidos: [`openspec/specs/pedidos-designacion/spec.md`](../../../openspec/specs/pedidos-designacion/spec.md)
- Changes en vuelo: `openspec/changes/admin-docentes/`, `openspec/changes/roles-membresia/`
