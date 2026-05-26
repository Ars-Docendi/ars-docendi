---
name: evaluate
description: Evaluación read-only del trabajo completado contra spec + grading-criteria + golden-principles. Termina actualizando docs/quality/scorecard.md con la composite score. NO modifica código.
argument-hint: [<feature-kebab>]
---

# Evaluate

**Source of truth:** [docs/workflows/evaluate.md](../../../docs/workflows/evaluate.md). Leerlo.

## Cuándo usar

- Después de implementar una feature significativa.
- Antes de defensa de TFI (auto-evaluación previa).
- Periódico para tracking de calidad.

## Criterios (ver `docs/quality/grading-criteria.md`)

| Criterio                                 | Peso |
| ---------------------------------------- | ---- |
| Funcionalidad / Compliance reglamentario | 30%  |
| Calidad de código                        | 25%  |
| Diseño / UX                              | 20%  |
| Originalidad y craft                     | 15%  |
| Documentación                            | 10%  |

## Flujo

1. Recopilar spec + plan + BR-\* aplicables + PR(s).
2. Por cada criterio: score 1-5 + justificación.
3. Calcular composite: `0.30 × Func + 0.25 × Code + 0.20 × UX + 0.15 × Orig + 0.10 × Doc`.
4. Check threshold: **ningún criterio < 3**, **Func ≥ 4** para release a cliente.
5. Si falla: anotar en `tech-debt.md`, NO mergear a `main`.
6. Actualizar `docs/quality/scorecard.md` con fila nueva.
7. Reportar findings al equipo (anti-patterns para `golden-principles.md`, items para `tech-debt.md`).

## Hard rules

- **Read-only**: NO modificar código aplicación.
- Mínimo spot-check en runtime (no solo lectura de código).
- Sin score inflado por compromiso emocional.

## Arguments

`$ARGUMENTS` — kebab-slug de la feature a evaluar.
