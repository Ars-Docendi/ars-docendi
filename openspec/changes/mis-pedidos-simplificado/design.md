## Context

`TablaMisPedidos.tsx` hoy pinta cada pedido como un `<div role="row">` sin `onClick`, con un
`MenuAccionesPedido` (kebab ⋮) que ofrece — según `puedeEditar`/`puedeEnviar`/`puedeCancelar`/
`puedeReenviar` (todos derivados de `estado`/`propietarioActual`) — hasta 4 acciones: Ver detalle,
Editar, Enviar a revisión, Cancelar (o Reenviar en vez de Enviar/Cancelar si está `devuelto`).
`MisPedidosPage.tsx` filtra con un `<input type="search">` combinado (docente/N°/materia) + un
`<Select>` de estado siempre visible. El seed (`pedidosSeed.ts`) tiene 11 pedidos para la cátedra del
JC de prueba, cubriendo los 7 estados posibles — no representa el caso real (una lista casi vacía).

La pantalla de Usuarios (`features/usuarios/components/FiltrosUsuarios.tsx`) ya resuelve un patrón de
filtro que el cliente pidió replicar: campos de texto fijos siempre visibles + un mecanismo
"+ Añadir filtro" para sumar filtros opcionales (con botón "×" para quitarlos), sobre un fondo gris
propio (`.adoc-alert`-like wrapper, ver su implementación con estilos inline).

## Goals / Non-Goals

**Goals:**

- Sacar el prefijo "Prof." de esta tabla específicamente.
- Renombrar "Novedad" a "Tipo" en esta pantalla (columna + filtro).
- Filtro con el mismo patrón que Usuarios: Docente/N° fijos, Tipo/Estado opcionales vía "+ Añadir
  filtro".
- Fila clickeable → detalle; botón "Editar" visible (no en menú) cuando el pedido es editable.
- Mover "Enviar a revisión"/"Reenviar a revisión" al form (`PedidoForm.tsx`), como una acción
  combinada con guardar.
- Eliminar "Cancelar" (la transición de dominio a `cancelado`) sin reemplazo.
- Seed más realista: 4 pedidos de ejemplo para la cátedra del JC en vez de 11.
- _(segunda ronda, amendment post-implementación)_ Botones "Ver"/"Editar" con el formato exacto de
  Usuarios (`Button variant="ghost" size="sm"`, sin ícono).
- _(segunda ronda)_ Acción "Eliminar" (X roja) para pedidos en `borrador`, tanto en la fila de la
  tabla como dentro del detalle del pedido.
- _(segunda ronda)_ Filtro Docente más angosto, igualando el ancho del campo equivalente en Usuarios.
- _(tercera ronda)_ Extraer el bloque de filtro a un componente genérico y reutilizable
  (`shared/ui/FiltrosLista.tsx`), para que otras pantallas lo adopten a futuro.
- _(tercera ronda)_ Nuevo campo Legajo: columna en la tabla + filtro opcional (mismo patrón que
  Tipo/Estado).

**Non-Goals:**

- No se toca `TablaRevision.tsx`, `ModalConfirmacionAccion.tsx` ni ninguna otra pantalla que también
  muestre "Prof. {nombre}" — están fuera del pedido del cliente para este change.
- ~~No se agrega ninguna forma de cancelar un pedido en esta iteración~~ — **superado en la segunda
  ronda**: se agregó "Eliminar" (D-7), acotado a `borrador`. Sigue sin agregarse la transición de
  dominio "Cancelar" (`cancelado`) — Eliminar no es esa transición, ver D-7.
- No se cambia la máquina de estados (`maquinaEstados.ts`) ni los hooks de mutación
  (`useEnviarPedido`/`useReenviarPedido`/`useCrearPedido`/`useEditarPedido`) — se reusan tal cual
  existen hoy, solo cambia DESDE DÓNDE se disparan. (`puedeEliminarPedido`, agregado en la segunda
  ronda, es un predicado nuevo en el mismo archivo, no una transición de estado — ver D-7.)
- No se sincroniza `screens.pen` en esta iteración (decisión ya tomada en changes anteriores de esta
  sesión, por costo de tiempo en Pencil).
- El botón Volver del detalle (`navigate(-1)`) no se diseña acá — es una corrección al requirement de
  `rediseno-revision-solo-grilla`, documentada en el design.md de ese change.
- _(tercera ronda)_ No se migra `FiltrosUsuarios.tsx` al componente genérico nuevo — sigue con su
  implementación propia (JSX + estado hardcodeado). El cliente pidió genericidad **de cara al futuro**
  ("planeo reutilizarlo a futuro en otras pantallas"), no un refactor retroactivo de Usuarios; migrarla
  ahora es riesgo/costo sin pedido explícito (tocar una pantalla que ya funciona y ya tiene tests
  propios) por un beneficio que no se pidió todavía.
- _(tercera ronda)_ No se agrega un input de Legajo al form de pedido (Alta/edición) — el cliente pidió
  filtrar y mostrar el legajo en la tabla, no capturarlo. Un docente de Alta puede no tener legajo
  asignado todavía (lo asigna el sistema/RRHH); por eso `DocentePedido.legajo` es opcional.

## Decisions

### D-1: El filtro de texto usa el mismo criterio "contiene, sin acentos/mayúsculas" ya establecido

Mismo criterio que ya se usó para el filtro nombre/legajo de la Tabla de revisión (tema E) y que ya
usa `FiltrosUsuarios.tsx` — `normalizarTexto` local (minúsculas + `normalize("NFD")` sin
diacríticos), duplicada en el archivo de filtros de esta pantalla en vez de importada desde
`features/usuarios/` (features aisladas: duplicar 3 líneas es más barato que un cruce entre
features).

### D-2: "Editar" es un botón inline en la fila, no un ícono aparte

Un `Button` (variant ghost o secondary, chico) al final de la fila, renderizado condicionalmente con
`puedeEditarPedido(pedido, actor)` (la misma función que ya usa `PedidoFormPage.tsx` para gatear el
form — reusada, no reimplementada). Cuando no es editable, esa celda queda vacía (no hay acción — la
única forma de interactuar con esa fila es hacer click para ver el detalle).

### D-3: La fila entera es clickeable, pero el botón "Editar" no dispara la navegación al detalle

La fila (`<div role="row">`) pasa a tener `onClick` → navega al detalle. El botón "Editar" DENTRO de
la fila hace `event.stopPropagation()` en su `onClick` para no disparar también la navegación al
detalle (mismo patrón ya usado en tablas de este proyecto para acciones inline dentro de filas
clickeables — p. ej. el patrón de "menú kebab dentro de fila" que ya existía, ahora reemplazado por un
botón directo con el mismo cuidado de no burbujear el click).

### D-4: "Guardar y enviar"/"Guardar y reenviar" es un segundo botón `submit`, no una casilla ni un modal

`PedidoForm.tsx` gana un segundo botón en el footer (`type="button"` con su propio `onClick` que llama
`handleGuardar({ enviar: true })`) que corre `validarPedido` y, si pasa, llama `onGuardar(datos,
{ enviar: true })` (se extiende la firma de `onGuardar` con un segundo parámetro opcional) para que
`PedidoFormPage.tsx` decida: crear+enviar (dos mutaciones en cadena: `crearPedido` → `enviarPedido`
con el id devuelto) o editar+enviar/reenviar (`editarPedido` → `enviarPedido`/`reenviarPedido`, según
si el pedido es `borrador` o `devuelto`). Sin modal de confirmación intermedio — es una sola acción
explícita del propio JC sobre su propio pedido, no una acción de revisión con impacto en otros
roles (que sí pasan por `ModalConfirmacionAccion`). ~~Corre la MISMA validación que "Guardar
pedido"~~ — **corregido en la séptima ronda (D-15)**: "Guardar pedido" ya no valida nada, solo
"Guardar y enviar"/"Guardar y reenviar" lo hacen.

**Alternativa descartada**: dos formularios/botones separados en distintas pantallas (guardar acá,
enviar allá) — es exactamente lo que se está simplificando; mantenerlo dividido no resuelve el pedido
del cliente.

### D-5: Reducción del seed — se preservan los 4 pedidos ya referenciados por nombre en otros tests [REVERTIDA, ver D-14]

`flujoAprobacion.test.tsx` referencia "Valeria Suárez" por nombre (test de devolución); `MisPedidosPage.test.tsx`
referencia "Laura Giménez" y "Valeria Suárez" (se reescribe igual en este change, pero conviene no
inventar nombres nuevos sin necesidad). Se conservan esos dos más "Pablo Herrera" (único ejemplo
`devuelto`, necesario para probar Editar+reenviar) y "Brenda Ortiz" (único ejemplo `rechazado`,
terminal). Se eliminan: Diego Morales, Sofía Romano, Martín Acosta (bo­rradores redundantes con Laura
Giménez), Lucía Fernández-semilla (`en_revision_coordinador` redundante con Valeria Suárez — la
docente "Lucía Fernández" del catálogo `DOCENTES_EXISTENTES` en `catalogos.ts` es un dato DISTINTO,
no se toca), Florencia Cabrera (`en_revision_secretaria`), Hernán Vidal (`en_revision_decanato`),
Gabriel Núñez (`en_lote`) — ninguno referenciado por nombre en ningún test (verificado por grep en
todo `frontend/src` antes de este change). `beforeEach` en `src/test/setup.ts` ya resetea
`localStorage` + el store antes de cada test, así que reducir el seed no rompe el aislamiento entre
tests de `flujoAprobacion.test.tsx` (cada test re-siembra desde cero).

### D-6: "Ver" y "Editar" copian el formato exacto de Usuarios, no un formato propio de Designaciones

`Button variant="ghost" size="sm"`, solo texto, sin ícono — igual que las acciones de fila de
`features/usuarios/`. Se descarta cualquier variante con ícono (el proyecto no tiene un patrón de
`Button` icon-only: los controles solo-ícono existentes, como el kebab que ya se eliminó, usan
`<button>` nativo + `aria-label`, no el componente `Button` de la librería). "Ver" navega al mismo
destino que el click en la fila (`/designaciones/pedidos/:id`) — es una forma explícita y descubrible
de llegar al detalle para quien no sabe que la fila entera es clickeable, no una ruta alternativa.

### D-7: "Eliminar" es una función nueva del store (`eliminar`), no una transición de la máquina de estados

A diferencia de aceptar/rechazar/devolver/enviar (que son transiciones de `estado` con evento de
historial), eliminar un borrador saca el pedido del store por completo — no hay "estado eliminado" ni
historial que preservar (un borrador nunca tuvo historial más allá de "crear"). Por eso
`puedeEliminarPedido` vive en `maquinaEstados.ts` (mismo lugar que los demás predicados de permiso,
para mantenerlos todos juntos) pero la operación en sí (`store.eliminar`) no pasa por
`aplicarAccion`/`registrarEvento` como las transiciones reales. **Alcance deliberadamente angosto**:
solo `borrador` + actor Jefe de Cátedra — un `devuelto` queda afuera aunque también sea "editable",
porque un devuelto ya tiene una revisión de por medio (historial no trivial) que eliminar borraría sin
dejar rastro; ese caso, si hace falta, es un "cancelar" con historial preservado (fuera de alcance,
ver Non-Goals).

### D-8: El modal de confirmación de Eliminar reutiliza el patrón de `ModalEliminarPeriodo`, no `ModalConfirmacionAccion`

`ModalConfirmacionAccion` (usado por las acciones de revisión: aceptar/rechazar/devolver/prioridad)
está diseñado alrededor de justificativos opcionales/obligatorios y cajas de aviso de a quién se
notifica — no aplica acá (eliminar un borrador propio no notifica a nadie ni pide justificativo). El
patrón correcto ya existe en el proyecto para "confirmar una eliminación destructiva simple":
`ModalEliminarPeriodo.tsx` (título, texto de confirmación con el nombre de la entidad, aviso "no se
puede deshacer", botones Cancelar/Eliminar). `ModalEliminarPedido.tsx` es una copia de ese patrón, no
una extensión de `ModalConfirmacionAccion`.

### D-9: El filtro se generaliza como componente config-driven (`fijos`/`opcionales`), no como hook headless

`shared/ui/FiltrosLista.tsx` recibe la config de campos (fijos: texto siempre visible; opcionales:
texto o select, agregables vía "+ Añadir filtro") más `valores`/`onChange` (controlado, mismo
contrato que ya usaba `FiltrosUsuarios.tsx`) y renderiza el bloque completo — no es un hook que deje el
JSX a cada pantalla. Se prefirió así porque el HTML/CSS de este patrón (fondo gris, dos filas, botón
"×" con el mismo estilo) es exactamente el mismo en cada pantalla que lo usó hasta ahora (Usuarios,
Mis pedidos); un hook headless obligaría a reescribir ese JSX en cada consumidor nuevo, perdiendo el
punto de generalizarlo. El estado de qué opcionales están activados (`activados`) vive DENTRO del
componente (no se expone) — es un detalle de presentación, no algo que el padre necesite controlar;
solo `valores` (los datos del filtro en sí) son responsabilidad del padre, igual que antes.

**Tipado**: `FiltrosLista<T extends Record<string, string>>` exige que el estado de filtros de cada
pantalla tenga un índice de string (`[clave: string]: string`) para que el genérico type-checkee sin
`as`/casts — `FiltrosMisPedidosState` lo agrega (D-9 lo requiere; no cambia cómo se accede a sus campos
por nombre, que siguen con su tipo literal específico como `Novedad | "todos"`).

**Reset al quitar un opcional**: cada campo opcional declara su propio `valorInicial` (por defecto
`""`); un select como Tipo/Estado declara `valorInicial: "todos"` para no romper el filtrado al
quitarlo (si volviera a `""`, `aplicarFiltrosMisPedidos` lo interpretaría como "coincide con novedad
vacía", ocultando todo en vez de mostrar todo).

### D-10: `DocentePedido.legajo` es opcional; `DocenteExistente.legajo` es obligatorio

Un docente ya existente en el sistema (catálogo `DOCENTES_EXISTENTES`, alimenta Sin novedad/Baja/Cambio)
siempre tiene un legajo asignado — campo obligatorio ahí. Pero un pedido de **Alta** crea un
`DocentePedido` para alguien que **todavía no existe** en el sistema (BR: recién se está dando de alta);
el legajo se lo asigna el sistema/RRHH después, no lo carga el JC en el form — por eso es opcional en
`DocentePedido`, y la tabla de "Mis pedidos" muestra "—" cuando falta (no se inventa un valor).

### D-11: Legajo obligatorio en Baja/Cambio es una regla de negocio nueva (BR-018), no una extensión silenciosa de D-10

Se registra como `BR-designaciones-018` en `docs/business-rules/designaciones.md` (mismo formato que
BR-001..004: statement/rationale/provenance/fuente normativa pendiente/ejemplos/roles, con mapping a
`pedidoValidacion.test.ts`) en vez de solo implementarla sin registro — es una regla de validación con
el mismo peso que las ya registradas (BR-002/003/004), no un detalle interno. Vive en
`pedidoValidacion.ts`, en el mismo bloque `if/else if` que ya valida DNI/nombre del docente (un tercer
`else if` condicionado a `novedad === "Baja" || novedad === "Cambio de cargo o dedicación"`) — reusa el
campo de error `errores.docente` en vez de sumar un campo nuevo, porque conceptualmente es la misma
categoría de error ("faltan datos del docente seleccionado").

**Por qué "Sin novedad" queda afuera**: el cliente pidió explícitamente "cambio o baja"; "Sin novedad"
también referencia un docente existente, pero ampliar el alcance sin pedido explícito es una
suposición de más — queda anotado en Assumptions de `docs/business-rules/designaciones.md` para
confirmar con el cliente, no decidido unilateralmente acá.

**Por qué no rompe los tests existentes**: `pedidoValidacion.test.ts`'s `datosBase()` ya asumía
(desde antes de esta regla) un docente "existente" por defecto (DNI/cargo/dedicación ya cargados, sin
`novedad: "Alta"`); agregarle `legajo: "1001"` al fixture base no cambia el comportamiento de ningún
test previo — cada Alta test sigue sin pasar legajo porque no lo necesita, y cada Baja/Cambio test que
ya pasaba (BR-003/BR-004, tipificación, dedicación) ahora tiene legajo por herencia del fixture base.

### D-12: Bump de versión del store mock en vez de una migración de datos

`pedidosStore.ts` siembra desde `pedidosSeed.ts` únicamente si `localStorage` está vacío bajo su
clave — un navegador que ya cargó la app antes de esta sesión tiene una copia persistida del seed
VIEJO (sin `legajo`), y seguirá usándola indefinidamente aunque el código ya esté corregido. La
solución establecida en este mismo archivo (comentario ya presente desde SCRUM-8, bump v1→v2) es
bumpear el sufijo de la clave (`v2` → `v3`): fuerza a que `asegurarHidratado()` no encuentre nada bajo
la clave nueva y vuelva a sembrar desde `crearSeedPedidos()`, que ya tiene `legajo` poblado. No hace
falta una migración real de datos porque es un prototipo mock — bumpear la clave es el equivalente
barato de un `DROP`+`reseed`, y es exactamente el patrón que este archivo ya documentaba para el caso
anterior (v1→v2, "el seed sumó pedidos de revisión + fechas recientes").

**Alternativa descartada**: escribir un migrador que recorra los pedidos persistidos y complete
`legajo` a partir de `catalogos.ts`/`pedidosSeed.ts` por DNI — over-engineering para un prototipo
frontend-only sin usuarios reales todavía; el bump de versión ya resuelve el problema con una línea.

### D-13: El botón Editar del detalle es una corrección de gap, no una feature nueva

El detalle del pedido nunca ofreció una forma de editar — solo "Mis pedidos" lo tenía, forzando al JC
a volver a la lista antes de poder corregir su propio borrador o devuelto. Se agrega reusando
`puedeEditarPedido` (mismo predicado que ya gatea el botón homónimo en `TablaMisPedidos.tsx` y en
`PedidoFormPage.tsx`) — sin definir un guard nuevo. El requirement que describe el detalle del pedido
pertenece a `rediseno-revision-solo-grilla` (capability `aprobacion-pedidos-designacion`), no a este
change; el delta correspondiente se aplicó ahí (mismo patrón ya usado para la corrección de Volver).

### D-14: Se revierte D-5 — el seed vuelve a 11 entradas, a pedido explícito del cliente

D-5 asumía que un seed chico era estrictamente mejor ("una lista casi vacía es el caso real"), pero
esa era una decisión de diseño de esta sesión, no algo que el cliente hubiera pedido — y cuando lo
necesitó para probar variedad de estados/filtros, lo pidió de vuelta explícitamente ("Volve a
añadirlos porque los necesito"). Se restauran los 7 pedidos retirados, con estos ajustes respecto a
los originales (que ya no existen en el historial de este seed mock, se reconstruyeron con criterio):

- **Diego Morales, Sofía Romano, Lucía Fernández, Gabriel Núñez**: reusan DNI/legajo/cargo/dedicación/
  materia de su entrada en `DOCENTES_EXISTENTES` (`catalogos.ts`) — mismo criterio que ya se usaba
  para Laura/Valeria/Pablo, así el pedido y el catálogo describen al mismo docente.
- **Martín Acosta, Florencia Cabrera, Hernán Vidal**: no están en el catálogo (igual que Mariano
  Tévez ya no lo estaba) — DNI/legajo nuevos, sin colisión con los existentes.
- **Sofía Romano pasa a `novedad: "Baja"`** (en vez de repetir `Sin novedad`/`Cambio` como los demás)
  — el seed anterior no tenía ningún ejemplo de Baja; se aprovecha la restauración para cubrir esa
  novedad también, ya que su ausencia era una laguna real de cobertura del seed.
- Todos los que quedan en `Cambio` o `Baja` llevan `legajo` (BR-018) y, cuando aplica, una
  `dedicacionSolicitada` con índice estrictamente menor a la actual (D-7 de `rediseno-form-pedido-designaciones`)
  — el seed no pasa por `validarPedido` (escribe directo al store), pero mantenerlo internamente
  consistente con las reglas de dominio evita datos de ejemplo contradictorios.

**Por qué no se revierte también el filtro Legajo/genérico ni BR-018**: el pedido del cliente fue
específicamente "volver a añadir los pedidos", no deshacer las otras rondas — Legajo, `FiltrosLista` y
BR-018 se quedan.

### D-15: "Guardar pedido" deja de validar — la validación completa solo gatea Enviar

Desde D-4, `handleGuardar` en `PedidoForm.tsx` corría `validarPedido` tanto para "Guardar pedido" como
para "Guardar y enviar"/"Guardar y reenviar" — un pedido a medio completar no se podía guardar como
borrador si, por ejemplo, faltaba un adjunto o el tipo de baja. El cliente pidió explícitamente lo
contrario: "se debe de poder guardar los datos actuales siempre, lo que no se puede es enviarse sin
cumplir con los campos obligatorios". Se separa `handleGuardar`: si `opciones?.enviar` es falsy,
guarda directo (`onGuardar(datos)`, sin `validarPedido`, sin bloqueo, limpia errores previos en
pantalla); si es `true`, corre `validarPedido` igual que antes y bloquea si hay errores. La UI
(footer del form) no cambia — sigue siendo el mismo botón "Guardar pedido" (`type="submit"`) y el
mismo "Guardar y enviar"/"Guardar y reenviar" (`onClick` con `{ enviar: true }`); solo cambia qué pasa
puertas adentro de `handleGuardar`.

**No hay nada que migrar en el dominio**: `pedidosApi.ts` (`crearPedido`/`editarPedido`) nunca corrió
`validarPedido` — siempre fue una validación exclusiva de la UI del form (`PedidoForm.tsx`). No hace
falta tocar `api/`, `maquinaEstados.ts` ni el store.

**Por qué no se agrega una BR nueva**: esto es una decisión de interacción (cuándo se aplica una
validación ya existente), no una regla de negocio nueva con su propio criterio verificable — las
reglas que sí exige el sistema (BR-001..004, BR-018) no cambian, solo el momento en que se evalúan.

## Risks / Trade-offs

- ~~**[Riesgo] Menos estados representados en el seed compartido**~~ — **moot tras D-14**: el seed
  volvió a 11 entradas, el riesgo ya no aplica.
- **[Riesgo] Extender la firma de `onGuardar`** (`(datos, opciones?) => void`) es un cambio de
  contrato de `PedidoForm` → mitigado: es un parámetro opcional, `PedidoForm.test.tsx` que no lo pasa
  sigue funcionando sin cambios.
- **[Trade-off] Sin "Cancelar" de dominio, un borrador no deseado queda dando vueltas en la lista
  para siempre** → parcialmente resuelto en la segunda ronda: "Eliminar" (D-7) cubre el caso de
  borrador no deseado. Un `devuelto` no deseado sigue sin forma de descartarse (fuera de alcance, ver
  D-7); el cliente no lo pidió para este change.
- **[Riesgo] `puedeEliminarPedido` excluye `devuelto` mientras `puedeEditarPedido` lo incluye** — dos
  predicados con superficies distintas sobre el mismo pedido → mitigado: la asimetría está documentada
  en D-7 y verificada con tests explícitos (`TablaMisPedidos`/`pedidosApi.test.ts`) que prueban que un
  `devuelto` tiene Editar pero no la X roja.

## Migration Plan

1. `api/pedidosSeed.ts`: reducir `SEMILLAS` de "Ingeniería de Software" a 4.
2. `components/PedidoForm.tsx` + `pages/PedidoFormPage.tsx`: botón "Guardar y enviar"/"Guardar y
   reenviar", extensión de `onGuardar`.
3. `components/filtrosMisPedidos.ts` (nuevo, o inline en la page): estado del filtro + función de
   filtrado, con `normalizarTexto` local.
4. `pages/MisPedidosPage.tsx`: nuevo bloque de filtro (estilo Usuarios), quita el `Select` de estado y
   el buscador combinado, ya no pasa `onEnviar`/`onCancelar`.
5. `components/TablaMisPedidos.tsx`: sin "Prof.", header "TIPO", fila clickeable, botón Editar
   inline, sin columna de kebab.
6. Eliminar `components/MenuAccionesPedido.tsx` (sin otros consumidores).
7. CSS (`pages/misPedidos.css`): filtro estilo Usuarios, fila clickeable, botón Editar inline.
8. Tests: reescribir `MisPedidosPage.test.tsx`; extender `PedidoForm.test.tsx` (Guardar y enviar);
   actualizar `flujoAprobacion.test.tsx` si el flujo de reenvío cambia de kebab a form.
9. Specs: aplicar el delta a `pedidos-designacion`.

**Segunda ronda (amendment post-implementación):**

10. `api/maquinaEstados.ts`: predicado `puedeEliminarPedido` (D-7).
11. `api/pedidosStore.ts` + `api/pedidosApi.ts`: `eliminar`/`eliminarPedido`.
12. `hooks/useAccionesPedido.ts`: `useEliminarPedido`.
13. `components/ModalEliminarPedido.tsx` (nuevo, D-8) + `components/lucide.tsx`: ícono `IconoX`.
14. `components/TablaMisPedidos.tsx`: botones Ver/Editar reformateados (D-6), X roja condicional.
15. `pages/MisPedidosPage.tsx`: wiring de Eliminar + modal; filtro Docente angosto (CSS).
16. `pages/DetallePedidoPage.tsx`: botón Eliminar (condicional) junto a Volver; Volver pasa a
    `navigate(-1)` (delta en `rediseno-revision-solo-grilla`, no en este change).
17. Tests: `pedidosApi.test.ts` (eliminar), `MisPedidosPage.test.tsx` (Ver/X roja/eliminar),
    `flujoAprobacion.test.tsx` (Volver por origen, eliminar desde el detalle).
18. Specs: ADDED "Eliminar un pedido en borrador" + delta de la Lista en `pedidos-designacion`.

**Tercera ronda (amendment post-implementación):**

19. `types.ts`: `DocentePedido.legajo?` + `DocenteExistente.legajo` (D-10).
20. `api/catalogos.ts` + `api/pedidosSeed.ts`: poblar `legajo` en el catálogo y en las semillas.
21. `components/PedidoForm.tsx`: propagar `legajo` al seleccionar un docente existente.
22. `components/TablaMisPedidos.tsx` + `pages/misPedidos.css`: columna LEGAJO.
23. `shared/ui/FiltrosLista.tsx` (nuevo, D-9) + `shared/ui/FiltrosLista.css` (nuevo): componente
    genérico; `components/filtrosMisPedidos.ts` suma `legajo` al estado + al filtrado.
24. `pages/MisPedidosPage.tsx`: reemplaza el bloque de filtro propio por `<FiltrosLista>` config-driven
    (fijos: Docente/N°; opcionales: Legajo/Tipo/Estado).
25. Tests: `MisPedidosPage.test.tsx` (columna Legajo, filtro Legajo); `shared/ui/FiltrosLista.test.tsx`
    (nuevo — cobertura del componente genérico en sí, independiente de Mis pedidos).
26. Specs: delta de la Lista en `pedidos-designacion` (columna + filtro Legajo).

**Cuarta ronda (amendment post-implementación):**

27. `pedidoValidacion.ts`: legajo obligatorio en Baja/Cambio (D-11, BR-018).
28. Tests: `pedidoValidacion.test.ts` (`bajaExigeLegajo`, `cambioExigeLegajo`, casos positivos y de
    Alta exceptuada); `datosBase()` gana `legajo` por defecto.
29. `docs/business-rules/designaciones.md`: registrar BR-018 (statement/rationale/ejemplos/roles) +
    fila en el mapping a tests; `pnpm generate-indexes`. Specs: delta de "Adjuntos y justificación
    obligatorios por novedad" (MODIFIED, mismo título que la spec base) en `pedidos-designacion`.

**Quinta ronda (amendment post-implementación):**

30. `api/pedidosStore.ts`: bump `adoc.mock.pedidos.v2` → `.v3` (D-12) — sin esto, el legajo poblado en
    la tercera ronda no se ve en navegadores con datos ya persistidos.
31. `pages/DetallePedidoPage.tsx`: botón "Editar" junto a Volver, condicional a `puedeEditarPedido`
    (D-13).
32. Tests: `flujoAprobacion.test.tsx` — 3 casos nuevos (Editar en borrador propio navega a la edición,
    Editar en devuelto propio, Editar ausente en un pedido en revisión); ruta stub
    `/designaciones/pedidos/:id/editar` agregada a `renderDetalle`.
33. Specs: delta del requirement "Detalle del pedido role-aware…" en
    `rediseno-revision-solo-grilla/specs/aprobacion-pedidos-designacion/spec.md` (botón Editar,
    convive con Eliminar en borrador, D-13).

**Sexta ronda (amendment post-implementación):**

34. `api/pedidosSeed.ts`: revertir D-5 (D-14) — `SEMILLAS` de "Ingeniería de Software" vuelve de 4 a
    11 entradas (Diego Morales, Sofía Romano, Martín Acosta, Lucía Fernández, Florencia Cabrera, Hernán
    Vidal, Gabriel Núñez restaurados).
35. `api/pedidosStore.ts`: bump `adoc.mock.pedidos.v3` → `.v4`.
36. `pages/MisPedidosPage.test.tsx`: quitar la aserción de conteo total de "Editar" (brittle al crecer
    el seed; las aserciones por fila ya cubrían la regla).

**Séptima ronda (amendment post-implementación):**

37. `components/PedidoForm.tsx`: `handleGuardar` separa el camino de "Guardar pedido" (siempre guarda,
    sin `validarPedido`) del de "Guardar y enviar"/"Guardar y reenviar" (valida, bloquea si hay
    errores) (D-15).
38. Tests en `PedidoForm.test.tsx`: reescrita "validación" (nuevo: "'Guardar pedido' siempre guarda,
    aunque falten campos obligatorios"); "tipificación de la baja" retargeteada a "Guardar y enviar"
    (antes probaba que "Guardar pedido" bloqueaba, ya no es cierto); renombrado el test de "Guardar y
    enviar" que comparaba con "Guardar pedido".
39. Specs: requirement "Enviar y reenviar desde el form de pedido" en `pedidos-designacion` — nuevo
    scenario "Guardar pedido siempre guarda…"; el scenario "Guardar y enviar respeta la misma
    validación que Guardar" pasa a "Guardar y enviar bloquea si faltan campos obligatorios" (ya no
    compara con Guardar, que no valida).

**Rollback**: revertir el PR restaura el menú kebab, el filtro combinado, las 11 semillas, y (segunda
ronda) quita Eliminar/Ver y el formato de botones de Usuarios; (tercera ronda) restaura el filtro
inline propio de "Mis pedidos" y quita la columna/filtro Legajo; (cuarta ronda) quita la validación
BR-018 y su registro en `docs/business-rules/`; (quinta ronda) revierte la clave de `localStorage` a
`.v2` y quita el botón Editar del detalle; (sexta ronda) vuelve a reducir el seed a 4 y revierte la
clave a `.v3`; (séptima ronda) "Guardar pedido" vuelve a validar igual que "Guardar y enviar"; sin
migraciones de datos (prototipo mock).

## Open Questions

- Ninguna bloqueante.
