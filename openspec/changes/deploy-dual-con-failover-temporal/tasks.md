# Tasks

## 1. Producción en dominio raíz y destinos exclusivos

- [x] 1.1 Adaptar `spin-up.sh` para `HOST_PUBLICO=<DOMINIO>` sólo en prod y conservar subdominios para staging/pr-N; agregar pruebas de las tres variantes y verificar que proyecto, base, bucket y Environment prod no se renombraron.
- [x] 1.2 Eliminar matrices duales de `deploy-prod.yml` y `deploy-staging.yml`, dirigiéndolos exclusivamente a principal y secundaria respectivamente; verificar con actionlint y pruebas de routing que cada ambiente tiene un único destino, SHA y scopes GitHub correctos.
- [ ] 1.3 Conservar previews y teardown sólo en Proxmox con gates originales; verificar labels y scripts confiables con pruebas/actionlint y observar un PR de ensayo con teardown sin afectar producción.
- [x] 1.4 Actualizar `docs/operations/github-pr-deploy.md` y ejemplos de runners para el nuevo reparto; verificar consistencia de todas las etiquetas y scopes con los workflows revisados.

## 2. Túneles y DNS sin conmutación

- [x] 2.1 Adaptar `config-principal.yml` al dominio raíz únicamente y `config.yml` a staging/wildcard con rechazo exacto de `prod.<DOMINIO>` antes del wildcard; verificar reglas de raíz, staging, preview y antiguo prod con `cloudflared ingress rule` o pruebas equivalentes.
- [x] 2.2 Actualizar `infra/cloudflared/README.md`, `infra/README.md` y `infra/traefik/README.md`: apex a Debian, wildcard/staging a Proxmox, reutilización del túnel actual y gestión local/remota; verificar ausencia de instrucciones de LB/Worker/failover requeridos y coherencia con las nuevas plantillas.
- [ ] 2.3 Operador configura túnel/DNS raíz de Debian, conserva staging/previews en Proxmox y verifica política Access pertinente; registrar evidencia de resolución/ingress y peticiones frontend/API por hostname, sin secretos. No afirmar configuración externa por validar plantillas.

## 3. Corte productivo seguro

- [ ] 3.1 Documentar y verificar checklist de servicios por host, puertos privados, secretos de GitHub y callbacks SSO aplicables; probar login, escrituras y carga/descarga de adjuntos en Debian sin imprimir credenciales, y staging/previews sólo en Proxmox.
- [ ] 3.2 Antes del corte, obtener decisión explícita sobre transferencia o descarte de datos del prod anterior; si hay transferencia, verificar backup/restore conjunto de PostgreSQL y objetos. No ejecutar borrados ni asumir producción vacía.
- [ ] 3.3 Operador registra runners por ubicación y valida persistencia del label secundaria tras nuevo registro efímero; comprobar runners online en ambas máquinas y un deploy real por ambiente con su SHA.
- [ ] 3.4 Tras aceptar producción en dominio raíz, retirar exposición/runtime prod de Proxmox preservando datos hasta autorización; verificar que `prod.<DOMINIO>` no sirve la app por wildcard y que staging/previews siguen operativos. Documentar rollback sin pérdida implícita de escrituras.

## 4. Documentación, validación y retiro posterior

- [x] 4.1 Actualizar README, `docs/architecture/infrastructure.md`, `docs/operations/storage-runbook.md` y ejemplos pertinentes al reparto definitivo del cambio, sin replicación/failover ni producción duplicada; verificar links y consistencia con scripts/ingress.
- [x] 4.2 Conservar el retiro del servicio de limpieza periódica obsoleto; sin script/unidades ni referencias en código/documentación vigente, manteniendo teardown al cierre de PR.
- [x] 4.3 Adaptar o incorporar tests de infraestructura al nuevo alcance y ejecutar actionlint, formato, `git diff --check` y OpenSpec estricto; registrar resultados del nuevo comportamiento y no reutilizar las pruebas del diseño dual como evidencia de éste.
- [x] 4.4 Documentar retirada futura de Proxmox sin trasladar implícitamente staging/previews a Debian; verificar que producción queda independiente y que cualquier dato de la VM se trata antes de apagarla.

## Estado de la revisión de alcance

Se mantienen el nombre histórico del cambio y los cambios ajenos del working tree. Tras la aprobación de apply se adaptaron workflows, scripts, ingress y documentación al nuevo alcance; esto no aplica la configuración del panel ni modifica los servicios en ejecución.

Las tareas cerradas se verificaron para el alcance revisado. Las comprobaciones previas de matriz dual y rutas `prod`/`staging` en ambos hosts no se usaron como evidencia del nuevo reparto.

## Evidencia de implementación local

- `node --test infra/tests/*.test.mjs`: 13 pruebas aprobadas; helper de hostname ejecutado con Bash y spin-up real ejecutado en un sandbox con Docker/provisioning simulados (sin servicios ni credenciales reales).
- Pruebas RED→GREEN: hostname raíz, integración de spin-up con el helper, selección exclusiva de runners y rutas de ambos ingress fallaron con el comportamiento anterior y pasan después de la adaptación.
- `actionlint v1.7.12` en todos los workflows, `bash -n` de scripts afectados, `pnpm format:check`, `git diff --check` y OpenSpec estricto: correctos.
- `cloudflared tunnel ingress rule` sobre las plantillas comprobó las cuatro variantes por host: Debian sirve sólo raíz; Proxmox sirve staging/previews y selecciona 404 para raíz y antiguo prod. Es validación de reglas, no tráfico externo.
- CI ejecuta la suite de infraestructura junto al formato. No se dispararon deployments remotos ni se modificaron runners, DNS, túneles activos o datos.

## Responsables y gates

- El operador asume Cloudflare y runners GitHub: 2.3 y 3.3. La documentación no sustituye evidencia de activación.
- La decisión sobre datos existentes y URLs de autenticación se resuelve antes del corte; no bloquea redactar/validar esta propuesta ni autoriza operaciones destructivas.
- Observaciones de la inspección anterior (no verificadas de nuevo en esta revisión): PostgreSQL de Proxmox publicaba 5432 en todas las interfaces; existían prod/staging/previews en esa VM y runners etiquetados secundaria. La ausencia de SeaweedFS prod en Proxmox ya no obliga a aprovisionarlo como respaldo: el destino productivo nuevo es Debian.
- El cambio no incluye balanceador, Worker DNS, sincronización de datos ni failover automático. No archivar ni declarar operativo hasta cerrar pruebas y tareas externas.
