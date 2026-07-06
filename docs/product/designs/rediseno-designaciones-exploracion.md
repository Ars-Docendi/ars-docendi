---
status: exploration # exploration | draft | review | approved
owner: "Julian Castellana"
branch: feature/rediseno-designaciones-ui
last_updated: 2026-07-05
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

## Mapa de temas

| #     | Tema                                                                                                                                                       | Pantallas                                      | Estado                                                                                             | Agrupación (change)          |
| ----- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------- | -------------------------------------------------------------------------------------------------- | ---------------------------- |
| **A** | **Modelo de horas** (materia / investigación / externas; modificar horas docente)                                                                          | Form (todo tipo, Alta, Cambio) + Datos Docente | Horas por materia ya en `admin-docentes`; falta en el pedido y desagregar investigación/externas   | Propio (el más profundo)     |
| **B** | **Múltiples materias en el pedido** (D3)                                                                                                                   | Form Alta/Cambio                               | Modelo existe en el docente; falta en el pedido (hoy `materia asociada` es singular)               | Junto con A                  |
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
- **Aclaración de semántica — check "depto externo" (parkeado).** Propuesta original: el toggle pasa a
  significar solo _"trabaja en otro departamento"_ (mención, sin horas). Por ahora **se deja como está**
  (toggle actual "hace más horas en otro Departamento"); se revisa después.

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
- **Semántica check "depto externo" (D):** se **deja como está por ahora**; se revisa después.

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

## Referencias

- Design-spec vigente del flujo: [`proyecto-docente-design-spec.md`](./proyecto-docente-design-spec.md)
- Mockups: [`screens.pen`](./screens.pen)
- Business rules: [`docs/business-rules/designaciones.md`](../../business-rules/designaciones.md)
- Spec funcional pedidos: [`openspec/specs/pedidos-designacion/spec.md`](../../../openspec/specs/pedidos-designacion/spec.md)
- Changes en vuelo: `openspec/changes/admin-docentes/`, `openspec/changes/roles-membresia/`
