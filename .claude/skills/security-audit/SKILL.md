---
name: security-audit
description: Read-only security pass del proyecto. Escanea deps vulnerables, secrets hardcoded, auth boundaries, exposición de endpoints, hardening de Azure AD config. Emite findings JSON/markdown, NO escribe código.
argument-hint: [<modulo opcional>]
---

# Security audit

## Cuándo usar

- Periódico (mensual, post-deploy).
- Antes de defensa de TFI.
- Antes de release significativa al cliente.
- Cuando aparecen CVEs nuevos en deps relevantes.

## Ejes del audit

### 1. Dependencias vulnerables

**Backend (.NET)**:

```bash
dotnet list backend/ArsDocendi.slnx package --vulnerable --include-transitive
dotnet list backend/ArsDocendi.slnx package --outdated
```

**Frontend (pnpm)**:

```bash
pnpm --filter frontend audit --audit-level=moderate
pnpm --filter frontend outdated
```

### 2. Secrets hardcoded

Buscar credenciales / connection strings / keys en código:

```bash
grep -rE "(password|secret|api[_-]?key|connection[_-]?string)" backend/src/ --include="*.cs" -i
grep -rE "appsettings(.Development|.Production|.Local)?\.json" backend/src/
```

Confirmar que:

- `appsettings.Development.json` NO está versionado (`.gitignore`).
- `appsettings.json` versionado NO tiene secrets reales.
- Connection strings vienen de env vars o `dotnet user-secrets`.

### 3. Auth boundaries

- Cada controller con acción mutativa tiene `[Authorize(Roles = ...)]`.
- No endpoints públicos no documentados (chequear contra `docs/architecture/api-contracts.md`).
- Endpoints `/api/*/ping` son los únicos `[AllowAnonymous]`.

### 4. Azure AD config hygiene

- Tenant ID + Client ID pueden vivir en `appsettings.json` versionado.
- Client Secret SOLO en env var o `user-secrets`.
- Validación de token: issuer + audience correctos.
- Roles claim mapping consistente con `docs/architecture/api-contracts.md`.

### 5. Cross-module / DAG

Confirmar que las dependencias respetan `docs/architecture/dependency-graph.md`:

- Sin referencias a `Modules.<X>.csproj` que no sean del Host.
- Sin imports de `Internal/` de otros módulos.

### 6. Logs sin PII

```bash
grep -rE "_logger\.(Log|Info|Warn|Error)" backend/src/Modules.Portal/ --include="*.cs"
```

Confirmar que no se loggean nombres, DNIs, emails sin masking.

## Output

Reporte estructurado (markdown o JSON) en `artifacts/security-audit-YYYY-MM-DD.md` con:

- Findings por eje + severidad (`blocker`, `high`, `medium`, `low`).
- Path / línea / detalle.
- Recomendación.
- Si hay findings high+: anotar en `docs/quality/tech-debt.md` con severidad y owner.

## Hard rules

- **Read-only**: NO escribir código en este pass.
- Findings high+ con secrets reales: notificar al equipo INMEDIATAMENTE (no esperar a end-of-audit).
- Reporte efímero: el `.md` de findings no se versiona (es snapshot).

## Arguments

`$ARGUMENTS` — opcional, scope a un módulo específico.
