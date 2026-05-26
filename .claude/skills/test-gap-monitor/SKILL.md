---
name: test-gap-monitor
description: Pass read-only que identifica tests faltantes (BR-* sin test, endpoints sin smoke, services sin cobertura). Emite reporte estructurado para que /add-tests los tome.
argument-hint: [<modulo opcional>]
---

# Test gap monitor

**Source of truth:** [docs/workflows/test-gap-monitor.md](../../../docs/workflows/test-gap-monitor.md). Leerlo.

## Cuándo usar

- Periódico (mensual).
- Antes de defensa TFI (auditoría de cobertura).
- Post-merge de módulo grande.

## Tipos de gap

| Tipo                           | Detección                                       | Severidad default      |
| ------------------------------ | ----------------------------------------------- | ---------------------- |
| BR-\* sin test en Test mapping | Leer `docs/business-rules/<modulo>.md`          | high (compliance)      |
| Endpoint mutativo sin test     | grep `[HttpPost\|HttpPut\|HttpDelete]` vs tests | high                   |
| Endpoint lectura sin smoke     | grep `[HttpGet]` vs tests                       | medium                 |
| Service interno sin tests      | grep `*Service.cs` vs tests                     | medium                 |
| Frontend sin render test       | grep `features/*.tsx` vs tests                  | low (runner pendiente) |

## Flujo

1. Inventariar lo que existe (endpoints, BR-\*, services).
2. Inventariar tests existentes (`backend/tests/`).
3. Cruzar — identificar gaps.
4. Priorizar por severidad.
5. Generar reporte en `artifacts/test-gaps-YYYY-MM-DD.md` (NO versionar).
6. Action items para `/add-tests`.

## Reglas

- **Read-only**.
- **No coverage por coverage**: priorizar BR-\* y endpoints críticos.
- Reporte efímero (no versionar).

## Arguments

`$ARGUMENTS` — opcional, scope a un módulo.
