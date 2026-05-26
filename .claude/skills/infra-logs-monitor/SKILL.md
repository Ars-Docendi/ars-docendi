---
name: infra-logs-monitor
description: Pass read-only que compara logs reales de producción contra lo documentado en docs/architecture/infrastructure.md. Detecta drift entre infra esperada y real (errores no documentados, endpoints sin logging, métricas que faltan).
argument-hint: [<env: staging | prod>]
---

# Infra logs monitor

## Cuándo usar

- Periódico (semanal post-deploy).
- Antes de defensa TFI (auditoría operacional).
- Cuando se sospecha que `infrastructure.md` está desactualizado.

## Pre-requisitos

- Acceso SSH a VM(s) o logs accesibles vía otro medio.
- `docs/architecture/infrastructure.md` con sección de logs/monitoring (aunque sea TBD).

## Detecciones

### 1. Errores no documentados

```bash
ssh <user>@<vm>
sudo journalctl -u ars-docendi --since "7 days ago" | grep -iE "error|exception" | sort -u | head -50
```

Cruzar contra `infrastructure.md` sección "Errores conocidos". Cada error frequente no documentado → drift.

### 2. Endpoints sin logging

Si Serilog está configurado: cada request debería loggearse en INFO con request-id, endpoint, status, latencia.

```bash
sudo journalctl -u ars-docendi --since "1 hour ago" | grep "HTTP" | awk '{print $X}' | sort -u
```

Cruzar contra lista de endpoints documentados en `docs/architecture/api-contracts.md`. Endpoints definidos en código pero sin aparecer en logs → posible drift de logging.

### 3. Métricas que faltan

Si hay Prometheus / similar: chequear que las métricas declaradas en `infrastructure.md` están emitiéndose.

### 4. Backup status

```bash
ls -lhrt /backups/postgres/ | tail -10
```

Confirmar que hay dumps recientes según política de backup documentada.

### 5. Disk + memory

```bash
df -h
free -h
sudo systemctl status ars-docendi --no-pager | head -10
```

Cruzar contra umbrales documentados (si existen).

### 6. Certificado TLS

```bash
echo | openssl s_client -servername $DOMAIN -connect $DOMAIN:443 2>/dev/null | openssl x509 -noout -dates
```

¿Vence pronto? ¿Está siendo renovado por certbot?

## Output

Reporte estructurado en `artifacts/infra-logs-YYYY-MM-DD.md`:

- **Drifts encontrados**: lista con severidad.
- **Health snapshot**: estado actual de servicios.
- **Action items**: actualizar docs / agregar logging / monitoring.

## Hard rules

- **Read-only**: NO restart, NO config changes, NO mutaciones de prod.
- Logs con PII: redactar al exportar a reporte.
- Findings críticos (disk full, cert expirado < 7 días, errores high rate): notificar al equipo INMEDIATAMENTE.

## Arguments

`$ARGUMENTS` — env target (`staging`, `prod`). Default: `prod`.
