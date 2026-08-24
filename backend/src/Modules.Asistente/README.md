# Modules.Asistente

Módulo del **asistente conversacional**: responde preguntas en lenguaje natural
sobre datos que ya viven en la base del sistema, en modo solo lectura.

Definición funcional y arquitectura objetivo en
[docs/product/designs/asistente-conversacional-definicion.md](../../../docs/product/designs/asistente-conversacional-definicion.md).
Change de planning: `openspec/changes/asistente-fundaciones/`.

## Estado

**Scaffold.** El módulo existe, el Host lo registra y solo expone su smoke test.
No tiene servicios, `DbContext` ni migraciones.

Lo que falta y dónde va:

| Qué                                  | Dónde             |
| ------------------------------------ | ----------------- |
| Motor de consulta y validador de SQL | `Application/`    |
| Cliente del proveedor de LLM         | `Infrastructure/` |
| Cadenas de conexión tipadas          | `Infrastructure/` |

No hay carpeta `Domain/`, a diferencia de los otros módulos: el asistente no
tiene entidades propias — lee las de otros schemas y orquesta. Si algún día
aparece un agregado que le pertenezca, se crea entonces.

## Endpoints

- `GET /api/asistente/ping` — smoke test, `[AllowAnonymous]`. No toca la base ni
  ningún servicio externo: tiene que poder distinguir «el módulo está cargado» de
  «la base responde».

## Proveedor del modelo

`IProveedorDeModelo` (en `Application/`) es la interfaz propia detrás de la cual
vive el proveedor de LLM. Hoy la única implementación es `ProveedorSimulado`:
determinista, sin red, y **el default de todos los ambientes**.

Usar un proveedor real exige ponerlo explícitamente en `Asistente:Proveedor`. El
motivo no es estilístico: los ambientes efímeros de PR no pueden tener clave real,
porque su workflow hace checkout del head del pull request y ejecuta un script que
viene de ese mismo PR, en un job con los secrets del environment.

La respuesta simulada se identifica como tal en la bandera `EsSimulada` **y** en el
texto. Un proveedor de mentira que devolviera algo verosímil sería peor que uno que
falla: la métrica del asistente es corrección con abstención.

## Reintento y techo de llamadas

Dos cotas explícitas, y explícitas porque **se multiplican**:

| Cota                               | Default | Dónde                        |
| ---------------------------------- | ------- | ---------------------------- |
| Llamadas al modelo **por turno**   | 4       | `ContadorDeLlamadasDelTurno` |
| Intentos de transporte por llamada | 3       | `ReintentoDeTransporte`      |

Peor caso de un turno: `4 × 3 = 12` requests HTTP. El número se puede decir en voz
alta justamente porque las dos cotas están escritas.

El techo de llamadas es **global del turno, no por capa**. Repartido por capa, cada
una respeta su límite y el total se multiplica igual — que es el modo de falla del
que este requisito nace. Lo aplica un decorador sobre `IProveedorDeModelo`, así que
ninguna capa puede saltearlo sin dejar de usar el proveedor.

El reintento de transporte va como `DelegatingHandler` del cliente HTTP, con
backoff exponencial y **jitter completo**, y honra `retry-after` cuando viene. No
reintenta ningún `400` —incluido el del límite de gasto: reintentar un rechazo por
presupuesto agotado gasta presupuesto que ya no hay— ni `401`/`403`, porque una
credencial no se arregla esperando.

Un reintento de transporte ocurre **dentro** de una llamada y no consume cupo del
turno: para eso tiene su propio máximo de intentos.

## Conexiones

El módulo registra `CadenaSoloLectura` y `CadenaSoloLecturaPii`, derivadas de la
`CadenaDuena` con los roles y contraseñas de la sección `Asistente`. Todavía nadie
las consume: llegan con el carril SQL. Ver
[data-model.md → Cadenas tipadas](../../../docs/architecture/data-model.md).

## Dependencias

Solo `ArsDocendi.Shared`. No referencia ningún otro módulo ni su propio
`.Contracts` (que nace vacío: ver
[Modules.Asistente.Contracts/README.md](../Modules.Asistente.Contracts/README.md)).

Tampoco declara EF Core, Npgsql ni MediatR: llegan cuando haya código que los use.

## Schema PostgreSQL

Ninguno propio. El asistente **lee** los schemas de otros módulos a través de dos
roles de solo lectura, con privilegios enumerados columna por columna.

El DDL vive en `database/asistente/*.sql` y se embebe como recurso de **este**
assembly, igual que `database/designaciones/*.sql` en su módulo.
`MigradorAsistente` lo ejecuta en el arranque `--migrate`, **último** de todos los
migradores: los `GRANT` necesitan que las tablas de `identity` y `designaciones`
ya existan.

No usa EF Core. El módulo no tiene entidades ni schema propio, así que no hay nada
que versionar con un historial de migraciones; el script es idempotente por
construcción y re-ejecutarlo converge.

`database/asistente/manifiesto-privilegios.json` es la fuente de verdad de qué se
concede. Un test lo compara contra los privilegios efectivos de la base en tres
direcciones: si alguien agrega una tabla o cambia un `GRANT` sin tocar el
manifiesto, el CI falla.
