## Context

El módulo Designaciones es hoy un prototipo **frontend-only**: `PedidoDesignacion` vive en
`frontend/src/features/designaciones/types.ts`, se persiste en un store singleton
(`api/pedidosStore.ts`) hidratado desde `localStorage`, y toda lectura/escritura pasa por
`api/pedidosApi.ts` (capa async, único punto de reemplazo cuando exista el backend real). No hay
módulo `.NET` de Designaciones todavía, así que este change no toca `Modules.*` ni Contracts.

El modelo actual captura **una sola materia por pedido** (`materiaAsociada: string`) con cargo y
dedicación "solicitados" a nivel del pedido, un toggle booleano `haceHorasOtroDepto`, y un campo
`horasInvestigacion?` que ya existe en el tipo pero no está expuesto en el form
(`PedidoForm.tsx`/`SeccionDesignacionSolicitada.tsx`). Los mockups de referencia
(`docs/product/designs/screens.pen`, frames `n1zz2M`/`tZANr`/`JOHDw`) ya resuelven visualmente el
modelo objetivo: lista de materias+horas en Alta, fila de horas en Cambio, horas de
investigación/externas explícitas, y tipo de baja en Baja.

## Goals / Non-Goals

**Goals:**

- Pasar de "una materia por pedido" a "una o más asignaciones (materia + horas) por pedido" en Alta,
  sin romper BR-designaciones-001 (un pedido por **docente** por período).
- Exponer horas de investigación y horas externas como campos numéricos libres (D2: sin validar
  cierre contra la dedicación) en los tres tipos de novedad que hoy muestran la sección de
  designación solicitada/horas (Alta, Cambio) y agregar el equivalente de horas de materia editable
  en Cambio ("modificar horas docente" dentro del pedido).
- Reemplazar `haceHorasOtroDepto: boolean` por `horasExternas: number` en el modelo y el form.
- Agregar tipificación de Baja (`tipoBaja`: Renuncia / Jubilación / Otro + texto libre) como campo
  obligatorio antes de la justificación.
- Extender el catálogo `DEDICACIONES` de 6 a 7 valores (agrega "Categoría 0").
- Mantener consistencia con el código real ya implementado (Cargo+Dedicación solicitados se editan
  juntos en Alta y Cambio; solo la materia es condicional a Alta).

**Non-Goals:**

- No se implementa la jerarquía de cargos ni "quitar prioritario" (tema C) — el cargo sigue sin
  restricción de "solo hacia arriba" en este change; eso es un change futuro con su propia BR.
- No se toca la pantalla Datos Docente (no existe todavía) ni "modificar horas externas/investigación"
  fuera del pedido.
- No se implementa la restricción real de período abierto (tema G) — la alerta informativa en Mis
  Pedidos ya está mockeada, pero el bloqueo real queda para el change que conecta con
  `gestion-periodos`.
- No se reconcilia `AsignacionMateria.cargo` de `admin-docentes` con D1 (cargo único por docente):
  queda registrado como deuda técnica, no se modifica ese change en vuelo.
- No hay cambios de backend/API real: sigue siendo store mock + `localStorage`.

## Decisions

### D-1: `materiaAsociada: string` → `asignaciones: AsignacionMateria[]`, con distinta mutabilidad por novedad

En vez de introducir un tipo distinto para "una materia" vs "varias materias", `PedidoDesignacion`
pasa a tener siempre `asignaciones: { materia: string; horas: number }[]` (mínimo 1 elemento,
invariante en las 4 novedades). Lo que cambia por novedad es **qué se puede editar**, no la forma del
dato:

- **Alta** y **Cambio de cargo o dedicación**: lista abierta — mismo patrón repetible en ambas (Select
  materia + Input horas por fila, botón ghost "Agregar materia", acción de quitar fila). En Cambio la
  lista se precarga con las materias que ya tiene el docente (`DocenteExistente.materiasActuales`,
  D-5), pero a partir de ahí es tan editable como en Alta: se puede cambiar la materia de una fila,
  agregar filas nuevas o quitar filas — siempre y cuando quede **al menos 1** (la UI MUST impedir
  quitar la última fila; ver spec para el detalle de validación).
- **Baja**: listado de solo lectura — se precarga con **todas** las `materiasActuales` del docente
  (mismo dato que alimenta Cambio), pero sin ningún control editable ni acción de agregar/quitar; es
  contexto informativo de qué queda vacante, no un dato a modificar.
- **Sin novedad**: fila fija de solo lectura (una asignación, la materia vigente del docente, sin
  editar ni materia ni horas).

Esto evita una unión discriminada `materiaAsociada | asignaciones[]` que obligaría a cada consumidor
(tabla, card, resumen, adapters) a chequear la novedad para saber qué campo leer — siempre es
`asignaciones[]`, y solo cambia si el form permite mutarla y en qué grado. También significa que Alta
y Cambio pueden compartir el mismo subcomponente de lista de materias (`SeccionDesignacionSolicitada`
ya lo hace en un único bloque, sin ramificar por `esAlta`/`esCambio` más que en la precarga inicial).

**Alternativa descartada**: mantener `materiaAsociada: string` para Baja/Cambio/Sin novedad y agregar
`asignaciones?: AsignacionMateria[]` solo para Alta. Se descartó porque duplica la fuente de verdad de
"qué materia tiene este pedido" según la novedad, y todo lector (`TablaMisPedidos`, `PedidoCard`,
`ResumenPedido`, `detalleAdapters`, `tableroRevisionModelo`) necesitaría una rama condicional en vez
de leer siempre `asignaciones[]` de forma uniforme.

**Nota de UI**: el panel de solo lectura "datos actuales" (antigüedad, cargo, dedicación) deja de
mostrar la materia — queda representada una sola vez, en la lista de asignaciones, para no duplicar
la misma información en dos lugares del form. Aplica tanto a Cambio como a Baja (ambos muestran la
lista de materias por separado).

### D-2: Horas de investigación y externas como campos sueltos del pedido, no de `asignaciones[]`

`horasInvestigacion` y `horasExternas` quedan a nivel `PedidoDesignacion` (no por materia), reflejando
que son horas del **docente** en ese pedido, no de una materia puntual — coherente con D1 del doc de
exploración (cargo/dedicación únicos por docente) y con cómo ya está mockeado en `n1zz2M`/`tZANr`
(filas separadas de la lista de materias).

### D-3: `haceHorasOtroDepto: boolean` se elimina, no se deprecia

Se reemplaza directamente por `horasExternas: number` (default `0`). Al ser un prototipo
frontend-only sin persistencia real más allá de `localStorage` y sin API pública consumida por otro
módulo, no hay contrato externo que romper — se trata como refactor de tipo interno, no como cambio
de API versionado. `pedidosSeed.ts` y los tests que referencian `haceHorasOtroDepto` se actualizan en
el mismo change (**BREAKING** dentro del propio feature, sin superficie pública).

### D-4: `tipoBaja` como campo obligatorio solo cuando `novedad === "Baja"`

Igual patrón que los adjuntos condicionales por novedad: `tipoBaja?: "Renuncia" | "Jubilación" |
"Otro"` + `tipoBajaDetalle?: string` (solo si `tipoBaja === "Otro"`). La validación
(`pedidoValidacion.ts`) agrega un nuevo campo a `ErroresValidacion` (`tipoBaja`) siguiendo el mismo
estilo que `cargoSolicitado`/`dedicacionSolicitada`. No se registra como BR-designaciones-NNN porque
es una categorización operativa (no surge de normativa institucional citable) — a diferencia de
BR-002/003/004 que sí exigen adjuntos/justificación por mandato de proceso.

### D-5: `DocenteExistente.materiaActual: string` → `materiasActuales: AsignacionMateria[]`

El catálogo mock (`api/catalogos.ts` → `DOCENTES_EXISTENTES`) hoy modela una sola materia por docente
(`materiaActual: string`), insuficiente para precargar el listado de Cambio (D-1). Se extiende a
`materiasActuales: { materia: string; horas: number }[]` (mínimo 1 elemento, igual que
`PedidoDesignacion.asignaciones`). Los docentes seed existentes migran su `materiaActual` a una lista
de un elemento con una carga horaria de ejemplo; no hace falta agregar docentes con múltiples materias
al seed, pero el tipo debe soportarlo para que Cambio funcione correctamente cuando lo tenga. Este
mismo campo alimenta la precarga de Sin novedad (materia única), el listado editable de Cambio, y el
listado de solo lectura de Baja.

### D-6: Cargo solicitado sin restricción de jerarquía

En Cambio, `cargoSolicitado` es un `Select` sobre el catálogo completo (`CARGOS`), sin ningún filtro
ni validación que limite las opciones al cargo actual del docente. No se implementa "solo se puede
subir de cargo" — esa regla (jerarquía de cargos, tema C) es explícitamente un change futuro con su
propia BR (`docs/product/designs/rediseno-designaciones-exploracion.md` → tema C). Cualquier hint o
copy en el mockup que sugiera lo contrario es un error a corregir (surgió de mezclar el mockup de este
change con contenido del tema C durante la sesión de diseño; ver historial de `screens.pen`).
**Aclaración (revisión posterior del cliente): esta libertad es exclusiva del cargo — la dedicación
sí tiene una restricción real, ver D-7.**

### D-7: Dedicación solicitada en Cambio: solo puede mejorar

A diferencia del cargo (D-6), la dedicación **sí** está restringida en Cambio: `dedicacionSolicitada`
SHALL ser jerárquicamente mejor que `dedicacionActual`. La escala de `Dedicacion` (`Categoría 0` a
`Categoría 6`) es descendente: **Categoría 0 es la de mayor jerarquía**, Categoría 6 la de menor —
convención análoga a las categorías de investigador (I es la más alta). "Mejorar" significa un índice
numérico **estrictamente menor** al actual (no se admite igual). Esto revierte la lectura original del
doc de exploración ("la dedicación se puede subir o bajar libremente") — el cliente aclaró en una
revisión posterior que esa libertad no aplica.

Dos capas de enforcement, no solo una:

- **UI**: el `Select` de "Dedicación solicitada" en Cambio filtra sus `<option>` a los valores
  estrictamente mejores que `dedicacionActual` (usando el nuevo helper `indiceDedicacion` de
  `catalogos.ts`) — el usuario no puede ni intentar elegir una dedicación peor o igual.
- **Validación**: `pedidoValidacion.ts` igual rechaza si por algún medio `dedicacionSolicitada` no
  mejora la actual (defensa en profundidad, mismo patrón que "no se puede dejar sin materias").

**Caso límite**: si `dedicacionActual` ya es `Categoría 0` (la máxima), no existe ninguna dedicación
"mejor" — el `Select` queda solo con el placeholder y ninguna opción real. No se resuelve un estado
especial para esto en este change (es un mock de catálogo chico); si aparece en la práctica, el JC no
podría completar un Cambio de dedicación para ese docente (sí podría cambiar el cargo).

### D-8: El recuadro gris de datos actuales se convierte en resumen de cambios (solo Cambio)

"Todo los datos actuales se deben ver bien reflejados con sus respectivos cambios" (pedido explícito
del cliente). `DatosActualesPanel` se extiende para, cuando recibe los valores "solicitados" (solo en
Cambio — Baja/Sin novedad no los pasan), mostrar la transición `actual → solicitado` de **todos** los
campos que Cambio puede modificar, no solo dedicación como hacía antes:

- **Cargo**: mismo patrón de transición que ya existía para dedicación (antes cargo solo mostraba el
  valor actual, sin reflejar `cargoSolicitado`).
- **Dedicación**: transición existente, sin cambios de comportamiento (solo ahora comparte helper con
  cargo).
- **Materias y horas**: nueva sub-sección dentro del mismo recuadro. Compara `materiasActuales` (la
  precarga real del docente, ver D-9) contra `materiasSolicitadas` (`datos.asignaciones`, editado en
  vivo por el usuario en la sección "Designación solicitada") **por nombre de materia**: una fila por
  cada materia presente en cualquiera de los dos conjuntos, marcada `sin-cambios` / `horas-cambiadas`
  (con transición `Xh → Yh`) / `agregada` (nueva, no estaba antes) / `quitada` (estaba, ya no).
- **Horas de investigación / externas**: nueva sub-sección con la transición `actual → nueva` de cada
  una (o el valor plano si no cambió).

El panel pasa de ~4 columnas horizontales fijas a: franja superior horizontal (Antigüedad / Cargo /
Dedicación, como antes) + sub-secciones verticales apiladas debajo (Materias, Horas) que solo aparecen
cuando corresponde (Cambio). Baja y Sin novedad no pasan los props "solicitados" → el panel renderiza
igual que antes de este ajuste (sin sub-secciones).

### D-9: `DocenteExistente` suma `horasInvestigacionActuales` / `horasExternasActuales`

Para poder comparar "actual vs. solicitado" en horas de investigación/externas (D-8), el catálogo mock
necesita una noción de "cuántas horas de investigación/externas tiene HOY el docente" — dato que antes
no existía en `DocenteExistente` (esos campos solo vivían a nivel `PedidoDesignacion`, post-carga).
Se agregan `horasInvestigacionActuales: number` y `horasExternasActuales: number` al catálogo mock,
con valores de ejemplo variados entre los 7 docentes seed (para que el resumen de cambios tenga casos
con y sin diferencia). Al seleccionar un docente en Cambio, estos valores precargan
`datos.horasInvestigacion` / `datos.horasExternas` (mismo patrón que la precarga de `asignaciones`,
D-1) — el usuario edita desde ahí, y el panel compara contra el valor original del catálogo (no contra
el valor editado, que sería comparar contra sí mismo).

## Risks / Trade-offs

- **[Riesgo] Cambiar `materiaAsociada` a `asignaciones[]` es breaking para todo lector del tipo** (7+
  componentes/tests listados en el proposal) → **Mitigación**: el change incluye la actualización de
  todos los consumidores en el mismo PR (invariante de "cambios de schema/API → docs en el mismo PR"
  aplicada por analogía al modelo frontend); `tasks.md` desglosa archivo por archivo para no dejar
  ninguno desactualizado, y los tests existentes (`pedidoValidacion.test.ts`, `pedidosApi.test.ts`,
  `PedidoForm.test.tsx`, `PedidoCard.test.tsx`, `TablaRevision.test.tsx`, etc.) actúan como red de
  seguridad.
- **[Riesgo] Datos mock desincronizados con el catálogo real** (p.ej. `DEDICACIONES` pasa a 0–6, pero
  `DOCENTES_EXISTENTES` en `catalogos.ts` usa valores "Categoría 1"–"Categoría 6" hardcodeados) →
  **Mitigación**: no hace falta migrar los datos seed existentes (siguen siendo válidos dentro del
  rango 0–6), solo verificar que el nuevo valor "Categoría 0" sea seleccionable en el Select.
- **[Trade-off] `asignaciones[]` con mínimo 1 elemento incluso cuando conceptualmente "no aplica"
  (Sin novedad)** → se acepta la fila fija de 1 asignación no editable en vez de modelar
  "materia opcional", porque simplifica los lectores (siempre hay al menos una materia) y coincide con
  el mockup vigente (Sin novedad ya muestra la materia actual, solo no la deja editar).
- **[Riesgo] Cambio permite agregar/quitar materias — el usuario podría intentar quitar la última fila
  y quedar sin ninguna materia** → **Mitigación**: la UI MUST deshabilitar/ocultar la acción de quitar
  cuando queda 1 sola fila, y `pedidoValidacion.ts` MUST bloquear igual el guardado si por algún medio
  `asignaciones` queda vacío (misma regla de "al menos 1" que ya aplica a Alta) — doble resguardo
  UI + validación, no solo uno de los dos.
- **[Riesgo] Catálogo de cargos fragmentado en 3 fuentes** (`designaciones/api/catalogos.ts` →
  `CARGOS`, `docentes/mock/mockStore.ts` → `CARGOS_DOCENTES`, jerarquía de 7 del doc de exploración) →
  **Fuera de alcance de este change** (pertenece al tema C); se deja registrado en
  `docs/quality/tech-debt.md` al cerrar este change para que no se pierda.

## Migration Plan

1. Actualizar `types.ts` (modelo, incl. `DocenteExistente.materiasActuales`) y `catalogos.ts`
   (catálogo Dedicación 0–6 + migrar `DOCENTES_EXISTENTES` a `materiasActuales`) primero — son la
   base de todo lo demás.
2. Actualizar `pedidoValidacion.ts` + su test (nuevo campo `tipoBaja`, sin validación cruzada de
   horas).
3. Actualizar `PedidoForm.tsx` y las secciones (`SeccionDesignacionSolicitada`,
   `SeccionDocentePedido`) para leer/escribir el nuevo modelo.
4. Actualizar lectores read-only: `TablaMisPedidos`, `TablaRevision`, `PedidoCard`, `ResumenPedido`,
   `DatosActualesPanel`, `detalleAdapters`, `tableroRevisionModelo`.
5. Actualizar `pedidosSeed.ts` (datos mock) y correr toda la suite de tests del feature.
6. Sin pasos de infraestructura ni base de datos — no hay migración de datos reales (prototipo mock).

**Rollback**: revertir el PR del feature branch `feature/<kebab>` restaura el modelo anterior sin
efectos secundarios (no hay datos persistidos fuera de `localStorage` del navegador de cada usuario).

## Open Questions

- Ninguna bloqueante para este change. Quedan abiertas en el doc de exploración (fuera de este
  alcance): umbral `>` vs `≥` para "quitar prioritario" (P1), confirmación del cliente sobre la
  jerarquía de 7 cargos, y alcance por rol del futuro Historial (P4).
