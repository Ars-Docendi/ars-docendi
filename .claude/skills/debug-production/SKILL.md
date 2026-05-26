---
name: debug-production
description: Investigar un issue reportado en producción. Correlaciona síntoma con logs (via SSH + journalctl/Serilog), recent changes (git log/PRs mergeados), deployment history. Output: análisis estructurado con causa probable. Read-only.
argument-hint: [<descripción del síntoma>]
---

# Debug production

## Cuándo usar

- Usuario reporta que algo no funciona en producción.
- Alerta de uptime monitor disparada.
- Comportamiento extraño sin error visible.

## Pre-requisitos

- Acceso SSH a la VM de producción (cuando se concrete deploy).
- `gh` CLI para inspeccionar deploys recientes.
- Síntoma claro del usuario (mensaje de error, paso reproducible, hora aproximada).

## Flujo

### 1. Reproducir si es posible

- Intentar reproducir en local con datos similares.
- Si reproducible local → escalar a `/fix-bug`.
- Si NO reproducible local → seguir investigando producción.

### 2. Inspeccionar deploys recientes

```bash
gh pr list --base develop --state merged --limit 10
git log --oneline -20 main
```

¿Hubo deploy reciente? ¿En la ventana del síntoma?

### 3. Inspeccionar logs en VM

Via SSH:

```bash
ssh <user>@<vm-host>
sudo journalctl -u ars-docendi --since "2 hours ago" | grep -iE "error|warn|exception|fail"
```

O si los logs van a un archivo:

```bash
tail -n 1000 /var/log/ars-docendi/app.log | grep -iE "error|warn|exception"
```

Filtrar por:

- Timestamp cercano al reporte.
- Endpoint mencionado en el síntoma.
- Usuario afectado (si se conoce el rol/email).

### 4. Correlacionar

| Síntoma     | Log relevante | Recent change | Causa probable |
| ----------- | ------------- | ------------- | -------------- |
| _(síntoma)_ | _(snippet)_   | _(PR #X)_     | _(hipótesis)_  |

### 5. Verificar dependencias externas

- Azure AD: ¿hubo downtime? (check Microsoft status page)
- API Guaraní: ¿está respondiendo?
- PostgreSQL: ¿está corriendo? ¿conexiones llenas?

```bash
sudo systemctl status postgresql
sudo -u postgres psql -d arsdocendi -c "SELECT count(*) FROM pg_stat_activity;"
```

### 6. Reporte

Generar análisis estructurado:

- **Síntoma**: lo que el usuario reportó.
- **Reproducibilidad**: local? prod?
- **Logs relevantes**: snippets con timestamp.
- **Recent changes**: PRs sospechosos.
- **Causa probable**: hipótesis con evidencia.
- **Next steps**: `/fix-bug` para causa X, o investigación adicional.

### 7. Si la causa es clara: escalar a `/fix-bug`

Si no: documentar findings y pedir más data al usuario / a otro miembro del equipo.

## Hard rules

- **Read-only**: NO modificar estado de producción durante la investigación.
- Logs con PII: **no compartir snippets crudos** en chats públicos — redactar.
- Si hay riesgo de impacto extendido (DB down, error rate alto): notificar al equipo INMEDIATAMENTE en paralelo a investigar.
- Si requiere acción urgente (restart, rollback): coordinar con el equipo ANTES de ejecutar.

## Arguments

`$ARGUMENTS` — descripción del síntoma (mensaje de error, comportamiento esperado vs real, rol del usuario, hora aproximada).
