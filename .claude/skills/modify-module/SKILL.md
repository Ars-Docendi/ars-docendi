---
name: modify-module
description: Modificar un módulo .NET existente. Énfasis en análisis de impacto sobre Modules.X.Contracts si cambia la API pública. Identifica consumidores cross-module y escala a spec si el cambio es breaking.
argument-hint: [<NombreModulo>]
---

# Modify module

**Source of truth:** [docs/workflows/modify-module.md](../../../docs/workflows/modify-module.md). Leerlo.

## Cuándo usar

- Agregar funcionalidad nueva dentro de un módulo existente.
- Cambiar implementación interna sin tocar Contracts.
- Modificar/extender Contracts (caso crítico — afecta consumidores).

## Flujo de decisión

### ¿Toca `Modules.<X>.Contracts`?

- **No** → cambio interno, continuar implementación.
- **Sí** → análisis cross-module:
  1. `grep -r "Modules.<X>.Contracts" backend/src/ --include="*.cs"` para encontrar consumidores.
  2. Clasificar el cambio:
     - **Aditivo** (nuevo método/DTO/property opcional): documentar, OK seguir.
     - **Breaking** (rename, remove, type change): escalar a `/plan-feature` o `/add-feature`.

## Reglas

- **Contract-first**: si toca Contracts, modificar Contracts antes que internals.
- Mantener `Internal/` privado — sin exportarlo.
- Actualizar `dependency-graph.md` si aparecen edges nuevos cross-module.
- Actualizar `domains/<x>.md` si cambia bounded context o API pública.
- Tests: si cambia BR-\* aplicables, actualizar tests correspondientes.

## PR body checklist

En el body del PR, listar explícitamente:

- Cambios en Contracts (sí/no, cuáles).
- Consumidores afectados.
- Migrations introducidas.
- Breaking changes (sí/no).

## Arguments

`$ARGUMENTS` — nombre del módulo a modificar (e.g. `Designaciones`).
