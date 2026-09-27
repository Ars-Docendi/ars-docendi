---
status: draft
owner: "Administración del sistema"
feature: "openspec/specs/administracion-sistema/spec.md"
last_updated: 2026-09-26
---

# Design spec: Sistema (sección unificada)

> Reescrito para `sistema-seccion-unificada` (ARS-152/153/154/155/156/157/158/159/160) a
> partir del `design.md` del change y del Claude Design `Sistema.dc.html`. El contenido de
> estados, copy y flujo de abajo sigue las decisiones D1–D13 de
> `openspec/changes/sistema-seccion-unificada/design.md`, que las fija con precisión de texto.
> Esta revisión SÍ tuvo acceso al canvas `Sistema.dc.html`, usado para el layout, el spacing y
> los valores de color de referencia; la sección «Tokens de color» de abajo cierra el mapeo de
> cada valor oklch del canvas a un token existente de `@ars-docendi/ui`, que es lo que la
> implementación (tasks.md 4.4) terminó usando.

## Resumen

Tres pantallas hoy separadas —Dashboard del sistema, Registros de auditoría y Uso del
asistente— se unifican en una sola sección «Sistema» (`/sistema`) con tres tabs
permission-gated: **Estado**, **Asistente** y **Auditoría**. Cada tab conserva su propio
permiso; ninguno nuevo se crea ni se amplía. Prioriza diagnóstico rápido («¿está todo
funcionando y qué cambió últimamente?»), trazabilidad comprensible de un feed de auditoría que
ahora cruza dos fuentes, privacidad, y consistencia con la interfaz institucional existente.

## Roles que ven esta surface

- [x] Administrativos (según el permiso de cada tab: ninguno de los tres es exclusivo de un rol nombrado)

El acceso efectivo se controla por `sistema.estado.ver`, `asistente.administrar` y
`auditoria.ver`, uno por tab; la sección entera se ve con cualquiera de los tres, y cada tab se
muestra u oculta con su propio permiso — nunca por rol. `/auditoria` y
`/asistente/administracion` dejan de ser páginas propias y redirigen a `/sistema#auditoria` y
`/sistema#asistente` respectivamente, dentro del mismo `RequirePermission`.

## Flujo principal

1. La persona abre `/sistema`. El header dice «Sistema», con la bajada «Estado de los
   servicios, uso del asistente y registros de auditoría.» y un botón secundario
   «Actualizar» que refetchea sólo la tab activa (con estado «Actualizando…» y deshabilitado
   mientras corre).
2. Ve las tabs Estado · Asistente · Auditoría, cada una con un punto de estado junto al nombre
   (sano / con problemas / en mantenimiento / pendiente). La tab inicial es la del hash de la
   URL si es válido y permitido; si no, la primera tab permitida en ese orden. Cambiar de tab
   agrega una entrada de historial (Atrás vuelve a la tab anterior).
3. En **Estado**, ve el banner resumen («todo disponible» / «N no disponibles» / «todo
   disponible, asistente en mantenimiento»), «Última comprobación», y las tarjetas agrupadas
   «Módulos» (Aulas, Tareas, Designaciones, Portal, Asistente) e «Infraestructura»
   (PostgreSQL). Debajo, sólo si tiene `auditoria.ver`, ve «Cambios recientes»: los últimos
   cuatro eventos de auditoría.
4. Clickear un cambio reciente abre la tab Auditoría con el período «Todo» y el detalle de ese
   evento ya expandido. Clickear «Ver uso →» en la tarjeta del Asistente (sólo con
   `asistente.administrar`) abre la tab Asistente.
5. En **Asistente**, ve el panel de uso rediseñado 1:1 con la referencia «Uso del asistente»
   (Claude Design), embebido sin su propio encabezado de página — el encabezado de la sección
   ya lo tiene: período en pastillas (no un `<select>`), un banner de mantenimiento, cuatro
   KPIs organizacionales del período + el tope mensual, y el detalle por usuario/rol en dos
   tabs con buscador, columnas ordenables, y el cupo diario editado directo en la fila (nunca
   escribiendo un código de rol ni un UUID).
6. En **Auditoría**, busca por texto libre, período (Hoy / 7 días / 30 días / Todo, default 7
   días), chips de Acción y de Módulo, y filtros adicionales colapsados (Desde, Hasta, Tabla,
   Clave de fila). La lista agrupa por día; abrir una fila muestra el detalle en un panel
   lateral, no en un modal — la lista se angosta, nada la tapa.

## Layout / IA

- La sección reutiliza `PageHeader`, el contenedor y la escala tipográfica del shell
  institucional; no introduce un sistema visual paralelo.
- Tabs de `@ars-docendi/ui`: `role="tablist"`, cada botón `role="tab"` con
  `aria-selected`/`aria-controls`, tabIndex rotante y flechas izquierda/derecha (la librería no
  ofrece Home/End; con tres tabs se acepta esa limitación en vez de bifurcar el componente).
  Cada `TabItem.label` compone el nombre de la tab con un punto de estado y texto visualmente
  oculto (p. ej. «, sin problemas», «, en mantenimiento»). Sólo el panel activo se monta, EXCEPTO
  que las consultas de salud de Estado corren en cualquier tab si la sesión tiene
  `sistema.estado.ver` — el punto de esa tab necesita el dato siempre.
- **Estado**: banner de una línea arriba, luego dos grupos de tarjetas en grilla (3 columnas en
  desktop) — «Módulos» primero, «Infraestructura» después. Cada tarjeta: nombre, pill de
  estado, tiempo de respuesta o nota, y un botón «Reintentar» sólo en la tarjeta fallida (nunca
  un reintento global que reintente lo que ya está bien). Debajo, «Cambios recientes» como una
  lista angosta de máximo 4 filas con un link «Ver todo en Auditoría →».
- **Asistente**: el `PanelAdministracionAsistente` (sin `PageHeader` propio) ocupa todo el
  ancho de la tab, con un `<h2>«Uso del asistente»` propio (nunca un segundo `<h1>`: la página
  ya tiene el suyo) + copete, y a la derecha el período como grupo de botones presionados
  («Hoy» / «7 días» / «30 días», mismo patrón y mismas etiquetas que el período de Auditoría)
  más «Exportar CSV» (descarga client-side de las filas ya cargadas de la vista activa, sin
  pedir nada nuevo al backend). `BannerDeMantenimiento` es un banner compacto de una fila
  (punto + «Asistente disponible» / ícono + «Asistente en mantenimiento», ámbar) con
  «Activar mantenimiento» abriendo un panel de razón obligatoria debajo, y «Desactivar
  mantenimiento» de un solo click. Debajo, `KpisDeUso` (Sesiones, Llamadas, Costo estimado
  con el prefijo «US$» más chico que el número, Latencia p95) + `TopeOrganizacionalCard`
  («Tope de {mes}», el gasto del mes como número principal y el tope al lado, barra
  segmentada con marcas al 50 %/75 %, lápiz-ícono para editar) en una fila. Por último,
  `PanelDeUso` en una sola tarjeta: `Tabs` «Por usuario» / «Por rol» (con badge de cantidad) a
  la izquierda y un selector de métrica «Sesiones | Costo | Tokens | Latencia» a la derecha;
  la tabla tiene una sola columna de detalle —la métrica elegida, con una barra proporcional
  al máximo de la vista— en vez de columnas siempre visibles, ordenada sola y descendente por
  esa métrica; el cupo diario se sigue editando inline por fila (`EditorDeCupoEnFila`: lápiz →
  stepper → Guardar/Cancelar, con confirmación al bajar un valor ya conocido). No hay columna
  de acceso on/off ni gráfico de tendencia por día: la referencia los pide, pero requieren
  datos que el backend todavía no expone (ver ARS-156, tasks.md §12.8 y §13).
- **Auditoría**: panel de filtros arriba (búsqueda + período como grupo de botones + chips de
  Acción/Módulo + «Más filtros» colapsable con badge de cuántos están activos), luego una
  grilla de dos columnas `minmax(0,1fr) 380px` cuando el detalle está abierto — la lista angosta
  en la columna izquierda, el panel de detalle fijo a la derecha; sin detalle abierto, la lista
  ocupa el ancho completo. La lista agrupa por día («Hoy · viernes 26 de septiembre», «Ayer
  · …», o la fecha completa) con columnas Hora | Cambio (resumen + actor + módulo) | Acción.
  El detalle es un `<aside>`, no un `Drawer`: no es un overlay modal.
- Los botones de actualización/reintento son siempre secundarios o `ghost`; nunca reciben
  énfasis primario. La acción primaria de la tab Auditoría es implícita en la búsqueda (no hay
  un botón «Buscar» aparte: cada cambio de filtro dispara la consulta).
- En pantallas estrechas, filtros fluyen a una columna, las tarjetas de Estado pasan a una
  columna, y la grilla de dos columnas de Auditoría colapsa a una — el detalle se abre debajo
  de la lista en vez de al costado. Ningún dato esencial se oculta silenciosamente.

## Estados a diseñar

| Estado                                              | Descripción                                                                                                                                                                                                                                      | Cuándo se muestra                                                                 |
| --------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------- |
| Loading                                             | Mensaje de estado anunciado; en refetch se conserva el contenido previo (nunca se vacía la pantalla) y la acción que disparó el refetch queda «Actualizando…»/deshabilitada                                                                      | Carga inicial, «Actualizar», «Reintentar»                                         |
| Empty (Auditoría)                                   | «No hay registros para estos filtros.», sin paginación                                                                                                                                                                                           | Auditoría con filtros sin coincidencias                                           |
| Empty (Cambios recientes)                           | «Todavía no hay cambios registrados.»                                                                                                                                                                                                            | Sin eventos de auditoría en absoluto                                              |
| Error (Auditoría)                                   | Estado distinto del vacío: mensaje accionable, sin excepción interna                                                                                                                                                                             | Fallo de la consulta (network, 5xx)                                               |
| Partial (Auditoría)                                 | `InlineAlert`: «No se pudieron cargar los registros del asistente. Se muestran los demás.» — la lista y la paginación siguen operables sobre lo que sí llegó                                                                                     | `parcial: true` en la respuesta (el rastro del asistente falló o alcanzó su cupo) |
| Success                                             | Banner + tarjetas independientes (Estado), lista agrupada por día con detalle (Auditoría), panel embebido (Asistente)                                                                                                                            | Respuesta válida                                                                  |
| Banner: todo disponible                             | Variante «éxito»: sin nombres, sin conteo                                                                                                                                                                                                        | Los cuatro pings + PostgreSQL responden y el asistente no está en mantenimiento   |
| Banner: N no disponibles                            | Variante «alerta»: nombra los componentes caídos (singular si es uno, plural con «y el resto…» si son más); esta variante **tiene prioridad** sobre el aviso de mantenimiento — un componente caído importa más que un mantenimiento planificado | Al menos un ping o PostgreSQL no respondió u ORIGINÓ error                        |
| Banner: todo disponible, asistente en mantenimiento | Variante intermedia: todo lo demás sano, nota de mantenimiento visible                                                                                                                                                                           | Ping del asistente OK, `mantenimientoAsistente: "activo"`, nada más caído         |
| Tarjeta: sin respuesta                              | «No respondió en 5 s»                                                                                                                                                                                                                            | Timeout del ping                                                                  |
| Tarjeta: respondió con error                        | «Respondió con error» (nunca «No respondió en 5 s», que sería falso)                                                                                                                                                                             | El ping devolvió una respuesta, pero no un timeout ni `status: "ok"`              |
| Tarjeta del asistente: mantenimiento sin comprobar  | «Estado de mantenimiento sin comprobar»; NUNCA contribuye al banner                                                                                                                                                                              | `mantenimientoAsistente: "desconocido"`                                           |
| Detalle: campo enmascarado                          | Ícono de candado + «Enmascarado por política», antes/después nunca se muestran                                                                                                                                                                   | El campo no está en la lista blanca de valores seguros                            |
| Detalle: campo sin valor previo                     | «—» en el lado «antes»                                                                                                                                                                                                                           | Un `Alta` (INSERT) o un override sin cupo previo                                  |
| Awaiting approval                                   | No aplica: esta sección es enteramente de consulta, sin flujo de aprobación                                                                                                                                                                      | Nunca                                                                             |

## Decisiones de diseño

- Un permiso, una tab: nunca inferir acceso por rol ni combinar los tres permisos en una
  condición «alguno de los tres implica ver todo». Ocultar una tab no reemplaza la
  autorización del endpoint — cada uno sigue devolviendo 403 por su cuenta (design.md, riesgo
  «ocultar tabs podría confundirse con autorizar»).
- Actor en orden natural «Nombre Apellido» (no «Apellido, Nombre»), con las variantes «Actor no
  identificado» y «Proceso automático» exactamente en los casos que define design.md D6: la
  segunda exige la AUSENCIA total de contexto de solicitud, no sólo un actor nulo.
- Acción como Alta / Cambio / Eliminación; un `UPDATE` nunca se traduce como baja.
- El resumen de un evento sólo muestra valores para una `UPDATE` de exactamente un campo
  seguro; todo lo demás es un resumen genérico sin valores — nunca inventar una regla más
  permisiva en el cliente que la que ya aplicó el backend.
- La búsqueda (`q`) es sobre etiquetas (módulo, objeto, campo, tabla, actor), nunca sobre
  valores — el placeholder «Buscar por usuario, objeto o cambio» no debe sugerir que busca
  contenido de campos.
- Los chips de módulo son una lista estática (Todos, Identidad, Designaciones, Portal,
  Asistente) declarada en el frontend, no descubierta en runtime — Aulas y Tareas no tienen
  tablas auditadas y no llevan chip hasta que alguna migración se lo dé.
- Todas las fechas (períodos, agrupación por día, «Hoy»/«Ayer», los timestamps de «Cambios
  recientes») se calculan en `America/Argentina/Buenos_Aires`, sin importar la zona del
  navegador.
- Reutilizar variables/tokens y los estilos de los componentes compartidos (`Tabs`, `Button`,
  `Pagination`); no fijar colores, radios o spacing locales para controles que ya existen en
  `@ars-docendi/ui`.
- El switcher «Datos de ejemplo» del canvas de diseño es sólo una herramienta del propio canvas
  y no se construye.

## Anti-patterns a evitar (específicos de esta feature)

- Un indicador agregado único que oculte cuál componente específico falló.
- Botón primario para «Actualizar» o «Reintentar» — son acciones de rutina, no la acción
  principal de la pantalla.
- Mostrar el detalle de un evento como modal (`Drawer`/diálogo): tiene que angostar la lista,
  nunca taparla.
- Inferir la tab activa o el acceso a una tab por el rol del usuario en vez de por el permiso
  efectivo de la sesión.
- Mostrar JSON de snapshots, datos personales, secretos, UPN/correo, IP o la razón/actor del
  modo mantenimiento a quien sólo tiene `sistema.estado.ver`.
- Dejar que la búsqueda (`q`) alcance un valor de campo, aunque sea de un campo "seguro" — el
  límite es de diseño de producto, no sólo de implementación, y no se relaja localmente.
- Descubrir los chips de módulo en runtime o agregar uno para un schema sin tablas auditadas.

## Tokens de color

Mapeo de cada valor oklch literal del canvas `Sistema.dc.html` al token existente de
`@ars-docendi/ui` más cercano (implementado en `frontend/src/features/sistema/sistema.css`).
La escala de `neutral` y `accent` tiene 8 pasos y cubre el canvas exactamente; `success`,
`warning`, `danger` e `info` sólo tienen 3 pasos (100/500/700), así que un par de valores del
canvas caen en el paso más cercano de esa escala en vez de en un empate exacto — se anota abajo
cuál.

| Uso                                                               | oklch del canvas                 | Token                                                                 |
| ----------------------------------------------------------------- | -------------------------------- | --------------------------------------------------------------------- |
| Texto primario                                                    | `25% 0.005 75`                   | `--neutral-800` / `--color-text-primary`                              |
| Texto secundario                                                  | `38% 0.006 75`                   | `--neutral-700` / `--color-text-secondary`                            |
| Texto terciario / notas                                           | `52% 0.007 75`                   | `--neutral-600` / `--color-text-tertiary`                             |
| Punto neutral (pestaña pendiente/Auditoría)                       | `68% 0.007 75`                   | `--neutral-500`                                                       |
| Borde de tarjeta/tabla                                            | `83% 0.007 75`                   | `--neutral-400` / `--color-border-default`                            |
| Borde sutil (separadores)                                         | `91% 0.007 75`                   | `--neutral-300` / `--color-border-subtle`                             |
| Fondo del `<main>`, hover de fila                                 | `95% 0.006 75`                   | `--neutral-200` / `--color-bg-canvas`                                 |
| Fondo de encabezado de día, hover de botón                        | `98% 0.004 75`                   | `--neutral-100` / `--color-bg-surface`                                |
| Fondo de tarjetas, tabla, panel                                   | `#fff`                           | `--color-bg-raised`                                                   |
| Pestaña activa, fila seleccionada (borde)                         | `57% 0.115 165`                  | `--accent-500` / `--color-accent`                                     |
| Enlaces «Ver uso →», «Ver todo en Auditoría →», «Limpiar filtros» | `48% 0.1 165`                    | `--accent-600` / `--color-text-link`                                  |
| Hover de esos enlaces                                             | `38% 0.08 165`                   | `--accent-700`                                                        |
| Avatar de actor persona                                           | `86% 0.06 165`                   | `--accent-200`                                                        |
| Badge de «Más filtros», fondo de fila seleccionada                | `94% 0.03 165`                   | `--accent-100` / `--color-accent-subtle`                              |
| Punto/pill «Disponible»                                           | `55% 0.13 150`                   | `--success-500`                                                       |
| Texto de pill «Disponible»                                        | `~40% 0.1 150`                   | `--success-700` (el token es `38%/0.09`, el más cercano)              |
| Fondo de pill «Disponible», chip «Alta»                           | `~94% 0.035 150`                 | `--success-100` (el token es `94%/0.03`, el más cercano)              |
| Punto/pill «No disponible»                                        | `55% 0.18 25`                    | `--danger-500`                                                        |
| Texto de pill «No disponible»                                     | `~45% 0.15 25`                   | `--danger-700` (el token es `40%/0.14`, el más cercano)               |
| Fondo de pill «No disponible», chip «Eliminación»                 | `~95% 0.03 25`                   | `--danger-100` (el token es `94%/0.04`, el más cercano)               |
| Punto/pill «Mantenimiento»                                        | `72% 0.14 75`                    | `--warning-500`                                                       |
| Texto de pill «Mantenimiento»                                     | `48% 0.11 70`                    | `--warning-700` (empate exacto)                                       |
| Fondo de pill «Mantenimiento», banner de mantenimiento            | `95% 0.04 80`                    | `--warning-100` (empate exacto)                                       |
| Fondo/texto del chip «Cambio»                                     | `~94% 0.03 250` / `~42% 0.1 250` | `--info-100` / `--info-700` (el token usa el hue 245, el más cercano) |

**Sin equivalente en la escala de 3 pasos** (a diferencia de `accent`/`neutral`, que tienen 8):
el canvas usa un cuarto tono, sólo para el BORDE de cada tarjeta de componente según su estado
(p. ej. `oklch(80% 0.08 150)` para «Disponible», entre el paso 100 y el 500). No hay ese paso
intermedio en `success`/`warning`/`danger`, así que se agregaron tres variables locales —las
únicas de este feature sin token equivalente— documentadas en un solo lugar,
`.sistema-seccion` en `sistema.css`: `--sistema-borde-disponible` (`80% 0.08 150`),
`--sistema-borde-no-disponible` (`75% 0.12 25`) y `--sistema-borde-mantenimiento`
(`80% 0.1 75`).

## Copy exacto

Todas las cadenas visibles para la persona usuaria, tal como las cita la spec delta
(`openspec/changes/sistema-seccion-unificada/specs/administracion-sistema/spec.md`):

- Header: «Sistema»; bajada «Estado de los servicios, uso del asistente y registros de
  auditoría.»; botón «Actualizar» / «Actualizando…».
- Tabs: «Estado», «Asistente», «Auditoría».
- Banner Estado: «Todos los componentes disponibles» / «N de N responden con normalidad.»;
  «1 componente no disponible» / «N componentes no disponibles» + «El resto funciona con
  normalidad.» (omitido si no queda ninguno disponible); «Todo disponible, asistente en
  mantenimiento» / «Los módulos responden. El asistente no acepta consultas hasta que se
  desactive el mantenimiento.»; «Última comprobación».
- Tarjetas: pills «Disponible» / «No disponible» / «Mantenimiento»; «N ms» + «tiempo de
  respuesta», o «sin respuesta»; notas «Responde normalmente», «Recibe consultas», «Consultas
  pausadas por un administrador», «No respondió en 5 s», «Respondió con error», «Comprobado por
  separado», «Estado de mantenimiento sin comprobar»; botones «Reintentar», «Ver uso →».
- Cambios recientes: título «Cambios recientes»; enlace «Ver todo en Auditoría →»; vacío
  «Todavía no hay cambios registrados.»; horas «Hoy HH:mm» / «Ayer HH:mm» / «d/m HH:mm».
- Asistente: título «Uso del asistente»; copete «Consumo, presupuestos, acceso y
  mantenimiento.»; período «Hoy» / «7 días» / «30 días»; botón «Exportar CSV»; banner
  «Asistente disponible» / «Todos los usuarios con permiso pueden consultar.» / «Activar
  mantenimiento»; panel de razón «Razón» (obligatoria) / «Se muestra en el banner de todos los
  usuarios.» / «Cancelar»; banner activo «Asistente en mantenimiento» / «Nadie puede consultar.
  Razón visible: «{razón}»» / «Desactivar mantenimiento»; KPIs «Sesiones» / «Llamadas» / «Costo
  estimado» / «Latencia p95»; tope «Tope de {Mes}» / «de {tope} (estimado)» / «{N} % usado · al
  100 % se bloquean las consultas»; tabs «Por usuario» / «Por rol»; selector de métrica
  «Sesiones» / «Costo» / «Tokens» / «Latencia»; buscador «Buscar usuario o rol»; vacío «No hay
  uso registrado en este período.».
- Auditoría — filtros: placeholder «Buscar por usuario, objeto o cambio»; período «Hoy» / «7
  días» / «30 días» / «Todo»; chips de Acción «Todas» / «Altas» / «Cambios» / «Eliminaciones»;
  chips de Módulo «Todos» / «Identidad» / «Designaciones» / «Portal» / «Asistente»; «Más
  filtros» con badge; campos «Desde» / «Hasta» / «Tabla» (placeholder «p. ej. identity.users») /
  «Clave de fila» (placeholder «p. ej. 1042»); «N registros · solo lectura»; «Limpiar filtros».
- Auditoría — lista: encabezados de columna «Hora» / «Cambio · usuario · módulo» / «Acción»;
  encabezados de día «Hoy · <weekday> <d> de <month>» / «Ayer · …» / «<Weekday> <d> de <month>»;
  acciones «Alta» / «Cambio» / «Eliminación»; actor «Actor no identificado» / «Proceso
  automático»; vacío «No hay registros para estos filtros.»; paginación
  «<first>–<last> de <total>»; aviso parcial «No se pudieron cargar los registros del asistente.
  Se muestran los demás.».
- Panel de detalle: «Cerrar detalle»; secciones «Qué cambió» y «Datos técnicos»; etiquetas
  «Tabla», «Clave de fila», «Solicitud»; «Enmascarado por política»; «—» para valores ausentes.
- Eventos de identidad humanizados (design.md D5, «Humanized identity events»): un valor
  booleano de campo seguro se lee «Sí» / «No», nunca `true`/`false`, en el resumen y en el panel
  de detalle; el objeto nombra al sujeto — «Cuenta de usuario de {nombre}», «Persona {nombre}»,
  «Roles de {nombre}» — resuelto de la identidad actual, nunca del snapshot, y genérico sin
  nombre («Cuenta de usuario», «Persona», «Asignación de rol») cuando el propio evento cambió el
  nombre de la persona o no se la pudo resolver; ejemplo: «Cuenta de usuario de Paula Gómez:
  Activo No → Sí». Un alta o baja de rol lee «Rol {rol} asignado a {nombre}» / «Rol {rol}
  quitado a {nombre}», p. ej. «Rol Docente asignado a Julieta Acosta» / «Rol Docente quitado a
  Julieta Acosta».

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- [`docs/product/designs/administracion-usuarios-docentes-design-spec.md`](administracion-usuarios-docentes-design-spec.md)
- [`docs/product/designs/administracion-roles-permisos-design-spec.md`](administracion-roles-permisos-design-spec.md)
- `openspec/changes/sistema-seccion-unificada/design.md` (D1–D13) — fuente primaria de este spec
- Spec funcional: [`openspec/specs/administracion-sistema/spec.md`](../../../openspec/specs/administracion-sistema/spec.md)

## Open questions de diseño

- Ninguna. Los tokens de color quedaron cerrados en la sección «Tokens de color» de arriba, con
  el mapeo exacto de cada valor oklch del canvas usado en la implementación
  (`frontend/src/features/sistema/sistema.css`); no quedan decisiones de comportamiento
  pendientes para el alcance de este change: D1–D13 de `design.md` cubren cada estado, copy y
  regla de acceso descriptos arriba.
