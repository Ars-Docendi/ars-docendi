---
name: refactor-asistente
description: Ejecutar un renglón del refactor del módulo Asistente (backend/src/Modules.Asistente, frontend/src/features/asistente y su suite). Usar cuando el usuario pide refactorizar, reorganizar, achicar, modularizar o "limpiar" el Asistente, o cuando nombra un renglón de la cola (A1, C3, E4…). NO usar para features nuevas (/add-feature) ni para bugs reportados en uso (/fix-bug).
argument-hint: [A1..G3 | --siguiente | --estado]
---

# Refactor del Asistente

Una invocación = **un renglón** = una rama = un PR. El orden no es cosmético:
**seguridad primero (bloque A), después verdad documental (B), después la suite (C),
después la frontera (D-E), y el tamaño al final.** Los tres agujeros de seguridad no
esperan a nada: sus tests rojos viven en clases puras sin base — `ValidadorDeSqlTests`
corre 51 casos en **52 ms** hoy, sin Docker.

Antes de tocar nada, leé [modulo.md](modulo.md). Es el mapa del módulo y la lista de
decisiones ya tomadas por escrito. Sin eso vas a proponer un remedio que ya fue rechazado.

## Cuándo NO usar

- Capability nueva o comportamiento visible nuevo → `/add-feature` con su change OpenSpec.
- Algo roto reportado en uso → `/fix-bug`.
- "Achicá el Asistente" sin renglón elegido → correr `--estado` y elegir **uno**.

## Lo primero que le decís al usuario

Tres cifras medidas sobre el árbol. No se re-litigan y hay que decirlas antes de tocar código:

- El módulo **no tiene 50.000 líneas**: son **5.670 de código backend** (más 4.862 de
  comentario) y ~2.100 de frontend. Los ~50k que se perciben incluyen 16.465 de tests,
  3.018 del núcleo del evaluador, 840 de cassettes y ~8.840 de 19 changes sin archivar.
- La cola entera mueve **entre −400 y −700 líneas netas: el 1%**. Varios renglones son
  LOC **positivo a propósito**.
- Lo que hace que el módulo _se sienta_ más chico es **B1** (el README dice que está a
  medio hacer y está terminado), **B2** (archivar los 19 changes) y **E1** (subdividir la
  carpeta plana de 47 archivos de `Application/`).

**Reducir líneas no es el objetivo y no es métrica de éxito de ningún PR.** En
`Application/` el ratio comentario:código es **1,02:1** — hay más comentario que código, y
esos comentarios citan el bug real que cada decisión evita (el `"set_config"` entrecomillado
que devolvía 26 filas en vez de 138; el «Vosdame 3 materias» del `user-select`; la fecha de
referencia que hacía divergir dos tests). **Borrar comentarios no es refactor: es destruir
el registro de una decisión.** Si un PR baja el conteo borrando prosa, está mal.

## Step 0 — Resolver la base (una vez por sesión)

```bash
git ls-tree develop --name-only backend/src/Modules.Asistente
```

Si sale **vacío** —hoy sale vacío—, el módulo **no está en `develop`**: vive entero en
`feature/asistente-conversacional`, con 230 commits sin mergear. Entonces:

- La base de toda rama de esta cola es **`feature/asistente-conversacional`**, no `develop`.
- **B2 queda bloqueado.** `/opsx:archive` mergea las deltas a `openspec/specs/`, y archivar
  antes del merge deja las specs vigentes declarando código que `develop` no tiene
  (invariante #10: se archiva post-merge).

Hacé **una sola pregunta** y esperá: ¿los PRs se encadenan sobre
`feature/asistente-conversacional`, o se mergea esa rama a `develop` primero? La respuesta
fija la base de todo lo que sigue. No decidas vos.

## Step 1 — Resolver el renglón y su estado

El estado **no vive en checkboxes**. Vive en la precondición de cada renglón: un renglón
cuya precondición ya no matchea **está hecho**. Eso es lo que hace `--estado`.

- `A1`..`G3` → ese renglón. Corré su precondición. Si no matchea, decí "ya está hecho" y **pará**.
- `--siguiente` → el primer renglón de la cola cuya precondición todavía matchea y cuyas
  dependencias ya no matchean.
- `--estado` (o vacío) → corré las precondiciones de la tabla, imprimí el tablero por bloque,
  proponé el siguiente y **pará**.

## Step 2 — Ruta

| El renglón…                                                        | Ruta                        | Qué exige                                                                         |
| ------------------------------------------------------------------ | --------------------------- | --------------------------------------------------------------------------------- |
| Cierra un agujero de seguridad verificado (A1, A2, A3)             | **`/fix-bug`**              | Invariante #9: test rojo primero, después el fix mínimo                           |
| Mueve código sin cambiar comportamiento observable                 | **PR directo**              | Los tests existentes son la aserción. Ningún test existente cambia de expectativa |
| Cambia privilegios, schema, arranque del Host o infra (A3, A5, D3) | **`/opsx:propose` primero** | Invariante #5. Invariante #6: docs en el mismo PR                                 |
| Depende de una decisión del equipo (B3, D2)                        | **Parar**                   | Presentar y esperar. No decidir por el equipo                                     |

**Regla de corte del change**: un change se parte por cuándo puede archivarse, no por
afinidad temática. 19 changes sin archivar es la enfermedad; un change de 12 PRs es cómo se
contrae. **Un movimiento puro de archivos que el compilador verifica no es un change.**

## Step 3 — Rojo primero, cuando la ruta lo pide

Para `/fix-bug`: escribí el test que falla contra el código actual y **velo fallar**.
Si pasa en verde, alguien ya lo arregló: parás y reportás. Un test que nunca estuvo rojo
no prueba nada.

## Step 4 — Ejecutar, acotado

- Solo los archivos que el renglón nombra. **Prohibido el drive-by**: si aparece otro olor,
  va a `docs/quality/tech-debt.md` o como renglón nuevo de la cola; no se arregla acá.
- Código en español (invariante #13), salvo símbolos del framework.
- Criterio de admisión de todo tipo nuevo (Ousterhout): **su interfaz tiene que ser más
  simple que su implementación**. Nada de 45 archivos de 30 líneas.
- Los guides path-scoped se auto-activan (`dotnet-modules-guide`, `react-features-guide`).

## Step 5 — Guardarraíles

Los cuatro comandos, verbatim. Estaban verdes al escribir esto. Si alguno cambia de valor,
el PR está mal: revertir.

| #   | Regla                                                       | Chequeo                                                                                                                                                                                                          |
| --- | ----------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| G1  | El evaluador sigue **fuera** de la solución                 | `! grep -q 'eval/ArsDocendi.Evaluacion' backend/ArsDocendi.slnx`                                                                                                                                                 |
| G2  | `ArsDocendi.Evaluacion.Nucleo` no se muda de `backend/src/` | `grep -q 'src/ArsDocendi.Evaluacion.Nucleo' backend/ArsDocendi.slnx`                                                                                                                                             |
| G3  | Orden de migración: identity → designaciones → portal       | `grep -n MigrateAsync backend/tests/ArsDocendi.IntegrationTests/Infraestructura/PostgresFixture.cs` → tres líneas, EN ESE ORDEN (los números se movieron con G1; lo que se chequea es la secuencia, no la línea) |
| G4  | Sin Polly ni `AddStandardResilienceHandler()`               | `grep -rliE 'polly\|AddStandardResilienceHandler' backend/src backend/tests backend/eval` → vacío                                                                                                                |

`grep` en este entorno es **ugrep**: la alternancia `|` sin `-E` es literal y devuelve 0
siempre. Usá `-E` o escapá `\|`. Un chequeo que "pasa" porque el comando está mal escrito
no es un chequeo. (Por eso G1 va como `! grep -q` y no como `grep -c … → 0`: `grep -c` sale
con código 1 cuando cuenta cero, y revienta cualquier cadena `&&`.)

## Step 6 — Verificación

Todos los comandos **desde la raíz del repo**, nunca desde `backend/`:

```bash
dotnet build backend/ArsDocendi.slnx -c Release
dotnet test --solution backend/ArsDocendi.slnx -c Release
```

**Desde el merge de `sad-ko` (2026-09-07) el `global.json` vive en la RAÍZ**, no en
`backend/`, y pinea el SDK `10.0.201` con `rollForward: latestFeature`. Con un
10.0.x anterior no compila nada desde ningún directorio. Ese `global.json` también
declara `"test": {"runner": "Microsoft.Testing.Platform"}`, y por eso `dotnet test`
ahora exige `--solution`.

`backend/global.json` pinea SDK `10.0.201` y esta máquina tiene `10.0.111`. Como `dotnet`
resuelve `global.json` desde el **cwd** hacia arriba y no hay `global.json` en la raíz, desde
la raíz el build **pasa** y los tests corren. Desde `backend/` falla con `Requested SDK
version: 10.0.201`. Lo que sí está roto por ese pin es el **pre-commit** (`dotnet format`
resuelve `global.json` desde el directorio del proyecto): es un ticket propio, no algo a
parchear adentro de un PR de esta cola.

- Con **C2** mergeado, el ciclo corto es
  `dotnet test --solution backend/ArsDocendi.slnx -- --filter-not-trait "carril=base"`
  (824 casos en 3 s, sin Docker). **El PR igual se cierra con la suite completa.**

  La sintaxis cambió con el runner: `--filter 'carril!=base'` era de VSTest y con
  Microsoft.Testing.Platform devuelve **cero tests en verde**, que es la forma peor
  de equivocarse. Lo que va después de `--` son argumentos del runner, no de
  `dotnet test`.

- Frontend: `pnpm --filter frontend lint`, `pnpm --filter frontend build`,
  `pnpm --filter frontend test:run`. **Nunca `test`**: ese script es `vitest` en watch y
  cuelga la sesión.
- Si tocaste `openspec/`: `pnpm exec openspec validate --all --strict`. **Sin `--all` no
  valida nada y sale con código 1** ("Nothing to validate"). Es lo que corre CI
  (`.github/workflows/ci.yml:76`).
- Si agregaste o editaste un `.md`: `pnpm format`. El job `format` de CI corre siempre,
  sin path filter, y prettier reescribe tablas markdown.

## Step 7 — Docs en el mismo PR (invariante #6)

- Privilegios o schema → `docs/architecture/data-model.md` + `database/asistente/001_asistente_grants.sql`
  - **`database/asistente/manifiesto-privilegios.json`**. Ese JSON lo verifica
    `ManifiestoPrivilegiosTests` contra `information_schema.column_privileges` en tres
    direcciones: tocar el GRANT sin tocar el manifiesto pone la suite en rojo.
- Infra o variables de entorno → `docs/architecture/infrastructure.md`.
- Deuda diferida → `docs/quality/tech-debt.md`. Si el renglón **cierra** una TD, borrá la
  fila; no la dejes tildada.
- Si el renglón invalida un argumento escrito en un comentario, **borrar ese comentario
  falso es parte del renglón**.

## Step 8 — Cerrar

PR contra la base del Step 0, siguiendo [open-pr.md](../../../docs/workflows/open-pr.md).
Título en conventional commit: `fix(asistente):`, `refactor(asistente):`,
`test(asistente):`, `docs(asistente):`. En el body: qué renglón cierra, qué guardarraíles se
verificaron, y el **LOC neto real — positivo si lo es**, que en varios renglones es lo esperado.

## Anti-patterns

Los nueve remedios canónicos ya evaluados y rechazados con evidencia están en
[modulo.md](modulo.md) §4. Si vas a proponer uno, traé información nueva o no lo propongas.
Los cuatro más caros:

- Empezar por el bloque E porque "reorganizar es lo que se pidió". Sin C no hay red.
- Convertir `CapaConversacional` en un pipeline de middlewares con `ContextoDelTurno` mutable.
- Extract-method sobre `AddAsistenteModule` "para achicarlo".
- Contar líneas como métrica de éxito.

## Arguments

`$ARGUMENTS` — `A1`..`G3`, `--siguiente`, o `--estado` (default).
