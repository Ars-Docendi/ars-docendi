# Technical debt tracker

Brechas conocidas, atajos intencionales y follow-ups. Linkear a planes y specs cuando sea posible. Toda deuda con dueño + fecha.

| ID     | Área        | Severidad | Resumen                                                                                     | Remediación                                                                                  | Owner | Abierto    |
| ------ | ----------- | --------- | ------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- | ----- | ---------- |
| TD-001 | _(ejemplo)_ | low       | _(una línea)_                                                                               | _(acción)_                                                                                   | —     | YYYY-MM-DD |
| TD-002 | infra       | low       | Skills `/check-deploy`, `/debug-production`, `/infra-logs-monitor` retiradas (infra es TBD) | Recrear cuando `docs/architecture/infrastructure.md` deje de ser TBD (recuperar de git hist) | —     | 2026-06-12 |
| TD-003 | tests       | low       | Skills `/security-audit` y `/test-gap-monitor` retiradas (casi no hay código que escanear)  | Recrear cuando haya deps/auth real (security) y cobertura BR-\* (test-gap) — desde git hist  | —     | 2026-06-12 |

**Severidad:** `low` | `medium` | `high` | `blocker`

**Áreas frecuentes:** backend, frontend, infra, security, tests, docs, compliance.

## Reglas

- NO usar este archivo para esconder **stub UX**; eso falla evaluation o se arregla.
- Cerrar items moviendo a la sección "Resuelto" con fecha de cierre.
- Items `blocker` no pueden quedar abiertos al cerrar un PR a `main`.

## Resuelto

- _(ninguno)_
