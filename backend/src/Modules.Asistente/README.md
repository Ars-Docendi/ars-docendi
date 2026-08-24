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
