# Workflow: Test gap monitor

Pass **read-only** que identifica tests faltantes y produce un reporte (JSON o markdown). NO escribe tests — solo descubre.

## Cuándo usar

- Periódico (mensual, post-release).
- Antes de defensa de TFI (auditoría de cobertura).
- Después de mergear un módulo grande para verificar que la cobertura no quedó en deuda.

## Output esperado

Reporte estructurado de gaps:

```json
{
  "scan_date": "YYYY-MM-DD",
  "modules": {
    "designaciones": {
      "endpoints_without_smoke_test": ["POST /api/designaciones/aprobar"],
      "br_without_test": ["BR-designaciones-003"],
      "services_without_unit_tests": ["DesignacionesService.NotificarRechazo"],
      "frontend_features_without_render_tests": ["features/designaciones/components/ProyectoCard"]
    },
    "...": "..."
  },
  "priority": [
    { "module": "designaciones", "gap": "BR-designaciones-003 sin test", "severity": "high" },
    { "module": "portal", "gap": "endpoint /api/portal/docentes sin smoke", "severity": "medium" }
  ]
}
```

## Steps

### 1. Inventariar lo que existe

#### Endpoints HTTP

```bash
grep -r "\[Http" backend/src/Modules.*/Controllers/ --include="*.cs" | grep -oP "\[Http\w+(\(\"[^\"]+\")?\]"
```

#### Tests existentes (backend)

```bash
find backend/tests -name "*.cs" | xargs grep -l "\[Fact\]\|\[Theory\]"
```

#### BR-\* existentes

```bash
grep -rn "^### BR-" docs/business-rules/
```

#### Mapping BR ↔ tests

Leer la sección "Test mapping" de cada `docs/business-rules/<modulo>.md`.

### 2. Identificar gaps

Por cada módulo:

1. **Endpoints sin smoke test**: endpoints listados que no tienen un test que los invoque.
2. **BR-\* sin test**: BRs sin entry en Test mapping.
3. **Services sin tests unitarios**: métodos públicos de `*Service.cs` sin test que los cubra.
4. **Frontend features sin render tests**: componentes principales sin test (TBD — frontend no tiene runner configurado todavía, marcar como "test runner pendiente").

### 3. Priorizar gaps

| Tipo de gap                   | Severidad default        |
| ----------------------------- | ------------------------ |
| BR-\* sin test                | high (es compliance)     |
| Endpoint mutativo sin test    | high                     |
| Endpoint de lectura sin smoke | medium                   |
| Service interno sin test      | medium                   |
| Frontend sin render test      | low (hasta tener runner) |

Ajustar según contexto del módulo.

### 4. Generar reporte

Escribir a `artifacts/test-gaps-YYYY-MM-DD.md` o JSON. **NO commitear** este archivo (es output efímero, se ignora).

### 5. Action items para `/add-tests`

- Para BR-\* sin test: crear issues o anotar en `docs/plans/backlog.md`.
- Si hay budget de tiempo: agarrar los high-severity y aplicar `/add-tests` workflow.

## Reglas

- **Read-only**: este workflow NO escribe tests, solo identifica gaps.
- **No coverage por coverage**: priorizar gaps con impacto real (BR-\*, endpoints mutativos).
- **Reporte efímero**: el archivo de output no se versiona.
