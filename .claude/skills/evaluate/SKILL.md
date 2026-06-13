---
name: evaluate
description: Evaluación read-only del trabajo completado contra spec + grading-criteria + golden-principles. Termina actualizando docs/quality/scorecard.md con la composite score. NO modifica código.
argument-hint: [<feature-kebab>]
---

# Evaluate

Evaluación **read-only** del trabajo completado contra spec + grading-criteria + golden-principles. Termina con actualización del `scorecard.md`.

## Cuándo usar

- Después de implementar una feature o módulo significativo.
- Antes de pasar a defensa de TFI (auto-evaluación previa).
- Periódico (cada N features) para tracking de calidad en el tiempo.

## Pre-requisitos

- Change OpenSpec de la feature: `openspec/changes/<id>/` (o archivado en `openspec/changes/archive/`).
- Código mergeado o branch implementada.

## Steps

### 1. Recopilar artefactos

- Spec con acceptance criteria explícitas.
- Plan con definition of done.
- BR-\* aplicables en `docs/business-rules/`.
- PR(s) involucrados.

### 2. Evaluar por criterio (ver `docs/quality/grading-criteria.md`)

Para cada criterio, asignar score 1-5 con justificación breve.

#### Funcionalidad / Compliance reglamentario (30%)

- ¿Todas las acceptance criteria del spec se cumplen?
- ¿Las BR-\* aplicables tienen tests verdes?
- ¿Los flujos por rol funcionan?

#### Calidad de código (25%)

- ¿Respeta layers (Controller → Service → Repository)?
- ¿Respeta DAG (no referencias a `Internal/` de otros módulos)?
- ¿Tests significativos, sin god classes?

#### Diseño / UX (20%)

- ¿Sigue `design-principles.md`?
- ¿Estados explícitos (loading, error, empty, success)?
- ¿Cohesión visual con el resto del sistema?

#### Originalidad y craft (15%)

- ¿Decisiones deliberadas vs templating genérico?

#### Documentación (10%)

- ¿Spec actualizada?
- ¿BR-\* completas con citas reglamentarias?
- ¿`dependency-graph.md` / `api-contracts.md` / `data-model.md` sincronizados con código?

### 3. Calcular composite

`0.30 × Func + 0.25 × Code + 0.20 × UX + 0.15 × Orig + 0.10 × Doc`

### 4. Check de pass threshold

- **Ningún criterio < 3**
- **Funcionalidad ≥ 4** para releases al cliente
- Si falla: documentar en el plan + en `tech-debt.md`, NO mergear a `main`.

### 5. Actualizar scorecard

Agregar fila a [scorecard.md](../../../docs/quality/scorecard.md):

```markdown
| YYYY-MM-DD | <feature-name> | 5 | 4 | 4 | 3 | 4 | 4.3 | Notas sobre lo evaluado |
```

### 6. Reportar findings al equipo

- Score compuesto.
- Anti-patterns identificados que ameritan actualizar `golden-principles.md`.
- Items para `tech-debt.md`.
- Recomendaciones de follow-up.

## Anti-patterns en la evaluación

- Score inflado por compromiso emocional ("le pusimos mucho trabajo").
- Saltar BR-\* sin verificar test mapping.
- Evaluar sin haber visto el flujo en runtime (mínimo spot-check, no solo lectura de código).

## Hard rules

- **Read-only**: NO modificar código de aplicación.
- Mínimo spot-check en runtime (no solo lectura de código).

## Arguments

`$ARGUMENTS` — kebab-slug de la feature a evaluar.
