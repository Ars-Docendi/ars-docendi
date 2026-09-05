---
name: architecture-drift-check
description: Pass read-only que compara backend/manifiesto-de-aristas.json + docs/architecture/module-anatomy.md contra el código real. Detecta módulos no documentados, ciclos, referencias cruzadas inválidas, aristas no registradas.
argument-hint: [<modulo opcional>]
---

# Architecture drift check

## Cuándo usar

- Periódico (mensual, post-release).
- Antes de cualquier refactor grande.
- Antes de defensa TFI.

## Detecciones

### 1. Módulos no documentados

- Listar todos los `backend/src/Modules.*/` directorios.
- Cruzar contra `docs/architecture/domains/` archivos.
- Cada módulo sin doc correspondiente → **drift**.

### 2. Ciclos en dependencias

**Ya lo verifica un test.** `AciclicidadDelGrafoTests` construye el grafo desde los `ProjectReference` de todo `backend/src` —no sólo los `Modules.*`— y detecta ciclos con DFS, enumerando los proyectos que forman cada uno.

```bash
~/.dotnet/dotnet test backend/ArsDocendi.slnx --filter "FullyQualifiedName~AciclicidadDelGrafoTests"
```

En rojo, el mensaje ya nombra el ciclo: no hace falta reconstruirlo a mano. No rehacer el `grep`: un barrido artesanal sobre `backend/src/Modules.*/*.csproj` se saltea los proyectos que no son módulos, que es exactamente donde apareció la desviación que motivó el manifiesto.

### 3. Referencias cruzadas inválidas

Cualquier referencia desde `Modules.<X>/*.csproj` a `Modules.<Y>/*.csproj` (no a `Modules.<Y>.Contracts/`) → **violación** del principio cross-module-via-Contracts.

```bash
grep -E "Include=\".*Modules\.[A-Z][a-zA-Z]+/\"" backend/src/Modules.*/*.csproj | grep -v ".Contracts"
```

### 4. Imports de Internal/ cross-module

```bash
grep -rE "using ArsDocendi.Modules.[A-Z][a-zA-Z]+\.Internal" backend/src/ --include="*.cs" | grep -v "Modules.<MismoModulo>"
```

Cualquier match desde un módulo distinto → **violación**.

### 5. Aristas no registradas

**Ya lo verifica un test.** El registro es `backend/manifiesto-de-aristas.json`, no una tabla markdown: `ManifiestoDeAristasTests` lo cruza contra los `ProjectReference` reales en tres direcciones —arista en el código sin fila, fila sin arista en el código, y proyecto de `backend/src` sin clasificar— y una excepción a un invariante sin `ticket` o sin motivo también sale en rojo.

```bash
~/.dotnet/dotnet test backend/ArsDocendi.slnx --filter "FullyQualifiedName~ManifiestoDeAristasTests"
```

`docs/architecture/dependency-graph.md` ya **no** tiene tabla de aristas: si alguien la reintroduce, eso mismo es drift. El diagrama Mermaid del documento es un dibujo de orientación declarado no normativo (TD-018) — desincronizado es deuda anotada, no un blocker.

### 6. Endpoints sin documentar

Listar todos los `[Http*]` attrs en controllers.

Cruzar contra la sección "Endpoints por módulo" de `docs/architecture/api-contracts.md`.

Endpoints no documentados → **drift** (low/medium).

## Output

Reporte en `artifacts/arch-drift-YYYY-MM-DD.md` con:

- Drifts encontrados con severidad.
- Path + línea.
- Acción recomendada (actualizar docs vs arreglar código).

## Hard rules

- **Read-only**.
- Drifts críticos (ciclos, referencias inválidas, leak de Internal): `blocker` — el equipo debe arreglar ANTES del próximo merge.
- Drifts documentales: pueden ser `medium`, arreglar en próxima PR de docs.

## Arguments

`$ARGUMENTS` — opcional, scope a un módulo.
