---
name: modify-module
description: Modificar un módulo .NET existente. Énfasis en análisis de impacto sobre Modules.X.Contracts si cambia la API pública. Identifica consumidores cross-module y escala a spec si el cambio es breaking.
argument-hint: [<NombreModulo>]
---

# Modify module

Modificar un módulo existente. Énfasis en análisis de **impacto sobre Contracts** si cambia la API pública.

## Cuándo usar

- Agregar funcionalidad nueva dentro de un módulo existente.
- Cambiar implementación interna sin tocar Contracts.
- Modificar/extender Contracts (caso crítico — afecta consumidores).

## Pre-requisitos

- Identificar qué `Modules.<X>` se modifica.
- Leer `docs/architecture/domains/<x>.md` y `docs/architecture/dependency-graph.md`.

## Steps

### 1. Análisis de impacto

**¿La modificación toca `Modules.<X>.Contracts`?**

- **Sí**: cambio público — afecta consumidores. Continuar al step 2.
- **No**: cambio interno — saltar al step 3.

### 2. Si cambia Contracts: análisis cross-module

1. **Buscar consumidores**:
   ```bash
   grep -r "Modules.<X>.Contracts" backend/src/ --include="*.cs"
   ```
2. **Por cada consumidor** identificado:
   - ¿El cambio es **aditivo** (nuevo método/DTO/property) o **breaking** (rename, remove, type change)?
   - Si breaking: necesitas spec — escalar creando un change con `/opsx:propose` e implementarlo con `/add-feature`.
   - Si aditivo: documentar pero puede seguir adelante.
3. **Actualizar `dependency-graph.md`** si aparecen edges nuevos.

### 3. Implementación

- Si Contracts cambia: hacerlo **primero** (contract-first).
- Implementar internos respetando layer rules (Controller → Service → Repository).
- Mantener `Internal/` como privado (no exportar).

### 4. Tests

- Si cambia BR-\* aplicables: actualizar tests correspondientes.
- Si agrega capacidad: agregar tests para los casos cubiertos.
- Correr `dotnet test backend/ArsDocendi.slnx` — todos verdes.

### 5. Documentación

Actualizar en el mismo PR:

- `docs/architecture/domains/<x>.md` — si cambia bounded context, dependencies, o API pública.
- `docs/architecture/api-contracts.md` — si cambia endpoints.
- `docs/architecture/data-model.md` — si cambia schema.
- `docs/architecture/dependency-graph.md` — si cambian edges cross-module.
- `docs/business-rules/<x>.md` — si afecta BR-\*.

### 6. PR

Branch `feature/modify-<x>-<short-description>`. Ver [open-pr.md](../../../docs/workflows/open-pr.md). En el body listar explícitamente:

- Cambios en Contracts (sí/no, y cuáles).
- Consumidores afectados (lista de módulos).
- Migrations introducidas (sí/no).
- Breaking changes (sí/no).

## Reglas

- **Contract-first**: si toca Contracts, modificar Contracts antes que internals.
- **Breaking change → spec obligatoria**: escalar a `/opsx:propose` + `/add-feature`.
- Mantener `Internal/` privado — sin exportarlo.
- **Sin cambios secretos**: el PR del Modify debe documentar el delta en `docs/`.

## Arguments

`$ARGUMENTS` — nombre del módulo a modificar (e.g. `Designaciones`).
