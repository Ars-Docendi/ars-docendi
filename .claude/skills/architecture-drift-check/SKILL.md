---
name: architecture-drift-check
description: Pass read-only que compara docs/architecture/dependency-graph.md + module-anatomy.md contra el código real. Detecta módulos no documentados, ciclos, referencias cruzadas inválidas, edges no registrados.
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

Inspeccionar todos los `.csproj` y extraer ProjectReferences:

```bash
grep -rE "ProjectReference" backend/src/Modules.*/*.csproj
```

Construir grafo + detectar ciclos con DFS.

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

### 5. Edges no registrados

Listar todos los `ProjectReference` cross-module en el código.

Cruzar contra el "Edge registry" de `docs/architecture/dependency-graph.md`.

Cada edge en código no listado en docs → **drift**.

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
