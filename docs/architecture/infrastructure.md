# Infrastructure

Plan operacional del sistema. **Estado**: skeleton. Muchas secciones marcadas TBD hasta que se definan los detalles con la universidad.

## Ambientes

| Ambiente         | Propósito                 | Estado                                                            |
| ---------------- | ------------------------- | ----------------------------------------------------------------- |
| Desarrollo local | Trabajo diario del equipo | Activo (docker-compose para Postgres, backend + frontend locales) |
| Staging          | Validación antes de prod  | TBD — depende de disponibilidad de segunda VM                     |
| Producción       | Uso real del cliente      | TBD — VMs ofrecidas por UNLaM (especificaciones pendientes)       |

## Topología de producción (planeada, TBD)

```
         [Internet]
              │
              │ HTTPS (443)
              ▼
   ┌──────────────────────┐
   │  Reverse Proxy       │   nginx + certbot (Let's Encrypt)
   │  (VM ofrecida UNLaM) │
   └──────────────────────┘
              │
              │ HTTP (interno)
              ▼
   ┌──────────────────────┐         ┌──────────────────────┐
   │  Backend             │ ◄─────► │  PostgreSQL          │
   │  ArsDocendi.Host     │  TCP    │  (misma VM o aparte) │
   │  (Kestrel via systemd)│         │  (TBD)              │
   └──────────────────────┘         └──────────────────────┘
              ▲
              │ (estáticos servidos por nginx)
   ┌──────────────────────┐
   │  Frontend dist/      │
   │  (assets compilados) │
   └──────────────────────┘
```

## Deployment process (TBD)

A definir con UNLaM. Opciones tentativas:

- **Manual SSH + pull**: el más simple. CI/CD construye artefacto, sysadmin lo descarga vía SSH y reinicia.
- **GitHub Actions → SSH**: workflow que tras merge a `main` se conecta por SSH a la VM, hace `git pull`, build, restart de systemd unit.
- **Docker en VM**: si la VM tiene Docker, deploy con `docker compose up -d` desde imágenes pre-buildeadas.

Cuando se defina, actualizar este documento y crear `docs/workflows/deploy.md` con runbook paso a paso.

## Secrets management

- **Local development**: `dotnet user-secrets` para backend (almacena fuera del repo). Frontend usa `.env.local` (gitignored).
- **Producción (planeado)**: variables de entorno en la VM, leídas vía `ASPNETCORE_*` y `appsettings.Production.json` (no versionado). **Nunca** secretos en `appsettings.json` versionado.
- **Connection strings**: en env vars (`ConnectionStrings__ArsDocendi=...`).
- **Azure AD config**: tenant ID + client ID son públicos (van en `appsettings.json`); client secret en env var.

## Backup strategy

**A definir SLA con UNLaM**. Recomendación mínima:

- **PostgreSQL dumps** automatizados diarios (cron + `pg_dump`).
- **Encriptados** antes de salir de la VM (GPG con clave del equipo).
- **Retención**: 7 dailies + 4 weeklies + 6 monthlies.
- **Destino**: TBD — almacenamiento en la propia universidad o cloud según política.
- **Restore drill**: probar restore mensual en ambiente de staging cuando exista.

## Reverse proxy y TLS

**Planeado**: nginx + Let's Encrypt (certbot).

- TLS termination en nginx.
- `proxy_pass` a Kestrel en `127.0.0.1:5000`.
- Static files (frontend `dist/`) servidos directo por nginx.
- HSTS habilitado.
- Redirección HTTP → HTTPS forzosa.

Config sample en `infra/nginx/ars-docendi.conf` (placeholder, ver Fase 8).

## Hardening checklist (VM)

Cuando se reciba la VM, completar:

- [ ] Firewall: solo abrir 22 (SSH), 80 (HTTP redirect), 443 (HTTPS).
- [ ] SSH: deshabilitar password auth, solo SSH keys. Cambiar puerto default si la universidad lo permite.
- [ ] `fail2ban` configurado para SSH + nginx.
- [ ] `unattended-upgrades` para security patches automáticos.
- [ ] Postgres: bind a `127.0.0.1` solo (no expuesto público).
- [ ] Usuario no-root con sudo para deploy; servicio corre como usuario dedicado sin shell.
- [ ] Logs: rotation con `logrotate` para evitar llenar el disco.
- [ ] Healthcheck externo: configurar un uptime monitor (UptimeRobot o similar) apuntando a `/api/designaciones/ping`.

## Monitoring (mínimo viable)

**Logs**:

- Backend: structured logging con **Serilog** → stdout → capturado por `journalctl` (systemd).
- Acceso a logs vía SSH + `journalctl -u ars-docendi`.
- Rotación + retención: definir cuánto se guarda según espacio en disco.

**Métricas**:

- Sin APM cloud (no aplica al ambiente). Métricas básicas via `/api/*/ping` endpoints.
- Opcionalmente: Prometheus + Grafana en otra VM si se quiere dashboard. **Decisión TBD**.

**Alertas**:

- Mínimo: uptime monitor externo notifica si `/api/*/ping` deja de responder.
- Avanzado: TBD.

## Runbooks operacionales

A crear cuando se concrete deploy:

- `docs/workflows/deploy.md` — proceso de release
- `docs/workflows/troubleshooting.md` — diagnóstico común (servicio caído, DB no conecta, certificado expirado, disco lleno)
- `docs/workflows/restore-from-backup.md` — procedimiento de restore validado
