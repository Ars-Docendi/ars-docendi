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

> **Actualización (2026-07-10):** los temas **A + B + D** ya bajaron a código en el change
> [`openspec/changes/rediseno-form-pedido-designaciones/`](../../../openspec/changes/rediseno-form-pedido-designaciones/)
> (implementación completa, no solo mockup — ver su `proposal.md`/`design.md`/`tasks.md`). Durante
> la implementación se corrigieron/ampliaron varias decisiones respecto a lo que este documento
> describía originalmente: Cambio pasó a tener el mismo listado editable de materias que Alta (no
> una sola fila fija), Baja también lista todas las materias del docente (de solo lectura, no
> contemplado acá), y el hint "solo cargo superior" que se había mockeado en `tZANr` se **sacó**
> (pertenece al tema C, fuera de alcance — el cargo queda con selección libre). Revisión posterior
> del cliente (mismo día, ya con el código andando): la **dedicación sí tiene una restricción real**
> en Cambio — solo puede mejorar (Categoría 0 = mayor jerarquía, Categoría 6 = menor), a diferencia
> del cargo que sigue libre; y el panel de "datos actuales" en Cambio se convirtió en un **resumen de
> cambios** que muestra la transición `actual → solicitado` de cargo, dedicación, cada materia (con
> sus horas) y horas de investigación/externas — no solo dedicación como en el mockup original (ver
> D-6 a D-9 en el `design.md` del change). Los temas **C, E, F, G** y la pantalla Datos Docente siguen
> pendientes, cada uno como change propio.

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

- ✅ Alerta de tiempo de períodos activos → mockeado en `m3xg` (`InlineAlert/Info` "Período abierto ·
  2026 · 1C — cierra el 20/03/2026 · quedan 5 días"). Sigue sin BR redactada (falta definir con
  cuántos días de anticipación se muestra, y si cambia de color/urgencia cerca del cierre).
- ✅ Doble click para abrir además de los 3 puntitos → mockeado como hint de texto sobre la tabla en
  `m3xg` ("Tip: doble click en una fila también abre el pedido"). Es solo la intención visual — el
  gesto de doble-click no es representable en un mockup estático; queda para la implementación real.

**Cargando un pedido — Todo tipo**

- ✅ Horas de investigación → mockeado en Alta (`n1zz2M`) y Cambio (`tZANr`).
- ✅ Modificar horas docente → **corrección (2026-07-06):** esto es modificar las horas de materia
  **dentro del pedido**, no la pantalla Datos Docente (son dos ítems distintos). Ya está mockeado:
  Alta vía "Materias y horas asignadas", Cambio vía el campo editable "Horas · Programación I". No
  se tocó para "Sin novedad" (no existe un frame propio en el mockup para esa novedad — a definir si
  hace falta) ni para Baja (no aplica, el docente se va).

**Alta**

- ✅ Selección de múltiples materias → mockeado en `n1zz2M` (2 filas materia+horas + "Agregar materia").
- ✅ Horas de materia / investigación / externas → mockeado en `n1zz2M`.
- ⏳ Usuarios que son Jefe de Cátedra y Docente a la vez → parkeado (P3); el modelo de datos ya lo
  soporta (`roles: RolDocente[]` en `admin-docentes`), falta decidir el comportamiento (conflicto de
  interés al autocargarse un pedido).

**Baja**

- ✅ Tipificado de la baja → **decidido y mockeado**: Select "Tipo de baja" agregado en `JOHDw`
  ("Pedido (Baja)"), antes de "Motivo de la baja", ejemplo "Renuncia" (enum cerrado
  Renuncia/Jubilación/Otro).

**Cambio**

- ✅ No se puede bajar el cargo, solo superiores → **decidido y mockeado**: hint de texto agregado
  bajo "Cargo solicitado" en `tZANr` ("Solo podés solicitar un cargo superior al actual (Adjunto)").
  Es solo la señal visual — la restricción real la impone el dominio (BR + jerarquía de 7 cargos), acá
  solo se comunica al usuario.
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

> **Corrección (2026-07-06, verificado directo en `screens.pen`, no de memoria):** varios de estos
> puntos **ya existían antes de esta sesión** (obra del design-spec original), no son gaps nuevos.

- ✅ Mostrar bien el tipo de cambio (alta/baja/cambio) → **ya existe** en `q6OrQB` (Kanban: `tipoChip`
  con ícono+color por novedad) y en `ebl4U` (Tabla: columna Novedad con el mismo chip). Nada que
  agregar; solo sobrevive con la Tabla al eliminar el Kanban.
- 📝 Estado más cerca del título / más visible → en `hcCfk` (Detalle) el badge de estado **ya está en
  la misma fila que el título** (no está lejos estructuralmente). El reclamo ("se pierde") es de **peso
  visual**, no de posición — falta una iteración de diseño, no agregarlo de cero.
- ⏳ Sacar el prioritario si cargo mayor → **confirmado ausente**: solo existe "Priorizar novedad"
  (marcar); no hay ninguna acción de "despriorizar/quitar" en `hcCfk` ni en los modales. Decidido como
  BR (P1, falta el umbral >/≥) — genuinamente pendiente de mockear.
- ⏳ Simplificar bastante la info → juicio de diseño, no verificable como "hecho/no hecho" en el
  archivo; sigue abierto.
- ⏳ Agregar botón de Volver → **confirmado ausente** en `hcCfk` y en `ebl4U`. Pendiente, trivial.
- 📝 Vista modo grilla para todos → la grilla (`ebl4U`, con columna Estado+avance y Novedad) **ya
  existe** desde antes; lo que falta es **sacar el Kanban y el switcher** (`viewSwitch` en `ebl4U`),
  no construir la grilla de cero. Frames a limpiar: `q6OrQB`/`kWSjh`/`Z0S9T` (Kanban, 3 variantes).

**Resumen:** mockeados hasta ahora — form de pedido completo (Alta + Baja + Cambio, temas A+B+C+D) y
Mis Pedidos (tema G + micro-UX). **Falta:** Historial (tema F, de cero) y la pantalla Datos Docente
(parte de tema A, de cero). **Revisión (tema E) es parcial**: el tipo de cambio y la vista de grilla
ya existían de antes; lo que falta ahí es acotado — sacar Kanban/switcher, agregar "quitar
prioritario" y botón Volver, e iterar el peso visual del estado y la simplificación de info.

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

### Mockup en Pencil — ronda 2: Mis Pedidos, Baja, Cambio (2026-07-06)

Capturas actualizadas en `exports/rediseno-designaciones-ui/`:

- **`m3xg`** ("Designaciones - Mis pedidos") — [mis-pedidos.png](./exports/rediseno-designaciones-ui/mis-pedidos.png):
  tema G — `InlineAlert/Info` **"Período abierto · 2026 · 1C — cierra el 20/03/2026 · quedan 5 días"**
  agregada entre el header y la tabla; y el hint de texto **"Tip: doble click en una fila también abre
  el pedido"** sobre la tabla (micro-UX). El gesto de doble-click en sí no es representable en un
  mockup estático — queda como intención para la implementación.
- **`JOHDw`** ("Designaciones - Pedido (Baja)") — [pedido-baja.png](./exports/rediseno-designaciones-ui/pedido-baja.png):
  tema D — Select **"Tipo de baja"** agregado antes de "Motivo de la baja", ejemplo "Renuncia".
- **`tZANr`** (Cambio) — [pedido-editar-cambio.png](./exports/rediseno-designaciones-ui/pedido-editar-cambio.png):
  tema C — hint de texto **"Solo podés solicitar un cargo superior al actual (Adjunto)"** bajo "Cargo
  solicitado". Es solo la señal visual; la restricción real la impone el dominio (BR + jerarquía).
- Los tres verificados sin problemas de layout (`snapshot_layout`).

**Con esto, el checklist de "Estado de los comentarios del profesor" queda así:** mockeados Mis
Pedidos (G), Baja (D) y Cambio (A+B+C) completos. Faltan de cero: Historial (F) y Datos Docente
(resto de A). Revisión (E) es **parcial** (ver corrección más abajo, verificada directo en el
archivo): el tipo de cambio y la vista de grilla ya existían de antes; lo acotado que falta es sacar
Kanban/switcher, "quitar prioritario", botón Volver, y refinar visualmente el estado.

## Mapa de temas

| #     | Tema                                                                                                                                                       | Pantallas                                      | Estado                                                                                                                                                                                                                                                                                                                                                                                                     | Agrupación (change)          |
| ----- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------- |
| **A** | **Modelo de horas** (materia / investigación / externas; modificar horas docente)                                                                          | Form (todo tipo, Alta, Cambio) + Datos Docente | ✅✅ **Implementado en código** (change `rediseno-form-pedido-designaciones`) en Alta/Cambio/Baja. Falta la pantalla **Datos Docente** en sí (fuera de ese change)                                                                                                                                                                                                                                         | Propio (el más profundo)     |
| **B** | **Múltiples materias en el pedido** (D3)                                                                                                                   | Form Alta/Cambio                               | ✅✅ **Implementado en código**, en Alta **y** Cambio (ampliado respecto al mockup original, que solo cubría Alta)                                                                                                                                                                                                                                                                                         | Junto con A                  |
| **C** | **Jerarquía de cargos** (cambio solo a cargo superior; quitar prioritario si tenés cargo mayor)                                                            | Form Cambio + Revisión                         | ⏳ El hint de "solo cargo superior" que se había mockeado en Cambio (`tZANr`) se **sacó** — el cargo queda sin restricción en el change de A+B+D. Tema C (jerarquía de cargos, "quitar prioritario") sigue sin implementar. Nota: la **dedicación** sí quedó con una restricción propia ("solo puede mejorar") decidida durante ese mismo change — no es tema C, es una regla de D-7, ver banner de arriba | BRs nuevas + orden de cargos |
| **D** | **Tipificaciones** (tipo de baja: Renuncia/Jubilación/Otro; check "depto externo" = solo menciona que trabaja en otro depto)                               | Form Baja + toggle depto                       | ✅✅ **Implementado en código** (change `rediseno-form-pedido-designaciones`). Check depto externo resuelto distinto (toggle eliminado)                                                                                                                                                                                                                                                                    | Con el form                  |
| **E** | **Rediseño Revisión** (tipo de cambio visible; estado cerca del título; simplificar info; botón Volver; **solo grilla, sin switcher**; quitar prioritario) | Revisión (grilla) + Detalle                    | Parcial ya existe de antes (tipo de cambio y grilla). Falta: sacar Kanban/switcher, quitar prioritario, botón Volver, iterar peso visual del estado y simplificar info                                                                                                                                                                                                                                     | UX-only, propio              |
| **F** | **Historial de pedidos** (pantalla nueva)                                                                                                                  | Nueva                                          | Nuevo (distinto del AuditLog por-pedido ya existente). **No mockeado todavía**                                                                                                                                                                                                                                                                                                                             | Propio                       |
| **G** | **Período abierto** (alerta de tiempo en Mis Pedidos; restricción de carga fuera de período)                                                               | Mis Pedidos (inicio)                           | ✅ Alerta mockeada en `m3xg`. Falta la BR de restricción de carga (conecta con `gestion-periodos`)                                                                                                                                                                                                                                                                                                         | Con Mis Pedidos              |
| —     | **Doble click para abrir** (además del kebab ⋮)                                                                                                            | Mis Pedidos                                    | ✅ Mockeado como hint de texto en `m3xg`                                                                                                                                                                                                                                                                                                                                                                   | Trivial, con E o G           |

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
- **Restricción real de período abierto (G):** hoy solo existe la alerta informativa en `m3xg`; falta
  la validación/BR que efectivamente bloquee crear o enviar pedidos fuera del período abierto (no hay
  ninguna acción deshabilitada ni mensaje de bloqueo mockeado todavía).
- **"Sin novedad" en el form de pedido:** no existe un frame propio para esta novedad en `screens.pen`
  (solo Alta/Baja/Cambio). Falta definir si necesita su propia sección de horas editables o si de
  verdad no expone campos adicionales (como dice la spec vigente).
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

**Frames ya tocados (terminados):**

- `n1zz2M` — "Designaciones - Pedido (Alta)" (temas A+B)
- `tZANr` — "Designaciones - Pedido (Editar · Cambio)" (temas A+B+C)
- `JOHDw` — "Designaciones - Pedido (Baja)" (tema D)
- `m3xg` — "Designaciones - Mis pedidos" (tema G + micro-UX doble click)

**Frames a tocar para lo que sigue (en orden sugerido, de menor a mayor esfuerzo):**

1. **Tema E (Revisión → solo grilla)**: parcial, no de cero — verificado directo en `screens.pen`.
   `ebl4U` (Tabla/grilla, vigente) **ya tiene** columna Novedad con chip de tipo de cambio (ícono+color)
   y columna Estado+avance combinada; `q6OrQB` (Kanban) también ya muestra el tipo con `tipoChip`. Lo
   que falta hacer, concretamente:
   - Sacar el Kanban: eliminar/archivar `q6OrQB`, `kWSjh`, `Z0S9T` (2 variantes descartadas, opciones
     C/D) y quitar el `viewSwitch` (segTabla/segTablero) del header de `ebl4U`, dejando la Tabla como
     única vista.
   - En `hcCfk` ("Revisión de novedad", Detalle): agregar la acción de **"quitar prioritario"** (tema
     C/P1 — hoy solo existe "Priorizar novedad", no hay acción inversa) y un **botón Volver** (no
     existe en ningún frame de Revisión).
   - Iterar el **peso visual del badge de estado** en `hcCfk` (ya está en la fila del título, pero el
     profesor dice que "se pierde" — es refinamiento visual, no reposicionarlo).
   - **Simplificar la info** de `hcCfk` es un juicio de diseño abierto, a definir iterando.
   - Probablemente convenga **copiar `ebl4U` a un frame nuevo** (no editar el original hasta
     confirmar) para hacer los cambios de switcher/limpieza.
2. **Tema F (Historial)**: no hay frame de partida. Usar `FindEmptySpace` (ver ejemplo en la sección
   "batch_design API" del propio Pencil) anclado a algún frame de Designaciones existente, y armar
   la pantalla desde cero reusando los componentes del design system (`Table`/`DataList`,
   `Breadcrumbs`, `PageHeader` pattern) — mismo patrón que ya usan las demás pantallas del módulo.
3. **Datos Docente**: no existe frame en `screens.pen` todavía para esta pantalla en absoluto — hay
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
