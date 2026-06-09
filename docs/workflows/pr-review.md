# Workflow: PR review

Review estructurado de un Pull Request. Output: inline comments en GitHub + un summary.

## Cuándo usar

- PR abierto en GitHub que necesita revisión antes de merge.
- Complemento del review humano (no reemplazo si hay equipo).

## Pre-requisitos

- Acceso a `gh` CLI autenticado.
- Conocimiento básico del módulo afectado.

## Ejes del review (en orden)

1. **Correctness** — ¿El cambio hace lo que dice la spec / plan / título del PR?
2. **Regressions** — ¿Rompe funcionalidad existente? ¿Tests existentes cubren el área?
3. **Security** — ¿Autorización por rol? ¿Sin secrets? ¿Validación de inputs?
4. **Error handling** — ¿Maneja errores razonables? ¿Mensajes accionables vs stacktrace al usuario?
5. **Tests** — ¿Tiene tests significativos? ¿Cubre BR-\* aplicables? ¿Red-green si es bug?
6. **Maintainability** — ¿Respeta golden-principles? ¿No introduce god classes? ¿Cumple layer rules?
7. **Documentación** — ¿Spec/plan/BR-\* / dependency-graph actualizados si corresponde?

## Steps

### 1. Read state del PR

```bash
gh pr view <NUMBER> --json title,body,files,additions,deletions,changedFiles,baseRefName,headRefName,labels
gh pr diff <NUMBER>
```

### 2. Buscar artefactos relacionados

- Si el PR es de feature: buscar el change OpenSpec asociado: `openspec/changes/<id>/` (proposal/specs/tasks).
- Si el PR es de bug: buscar el change creado para el bug escalado en `openspec/changes/<id>/`.
- Leer estos artefactos para entender el INTENT antes del CODE.

### 3. Aplicar los 7 ejes

Por cada eje, identificar issues (si los hay) con ubicación exacta:

| Eje         | Issues encontradas | Severidad                   |
| ----------- | ------------------ | --------------------------- |
| Correctness | ...                | blocker/high/medium/low/nit |
| Regressions | ...                | ...                         |
| ...         | ...                | ...                         |

### 4. Postear inline comments

Por cada issue encontrada, comentario inline en el archivo/línea con:

- **Severidad** prefijo: `[blocker]`, `[high]`, `[medium]`, `[low]`, `[nit]`
- **Qué** está mal.
- **Por qué** importa (relacionar al eje + golden-principle si aplica).
- **Sugerencia** de fix concreta.

```bash
gh pr comment <NUMBER> --body "[high] ..." -F /tmp/inline-suggestion.diff
```

(O usar `gh api` para true inline comments con `path`, `line`, `body`.)

### 5. Summary final

Un solo comentario top-level con:

- **Estado**: approve / request-changes / comment
- **Resumen** del review (1-2 párrafos).
- **Tabla** de issues por severidad.
- **Recomendación** explícita (mergear / pedir cambios / discutir).

```bash
gh pr review <NUMBER> --comment --body-file /tmp/review-summary.md
# o --approve / --request-changes
```

## Reglas

- **Inline > top-level** para issues concretas (más fácil de actuar).
- **Severidad explícita** siempre (evita ambigüedad).
- **No drive-by suggestions** que cambian scope ("ya que estás, refactoreá esto") — abrir issue separado.
- **Validar contra spec/plan**, no contra preferencias personales.
- Si el PR no tiene spec/plan y debería tener, eso ES un [high] issue (proceso roto).

## Anti-patterns

- "LGTM" sin review real.
- Review solo de código sin revisar tests.
- Severidad inflada para forzar discusión.
- Comentarios sin sugerencia accionable.
