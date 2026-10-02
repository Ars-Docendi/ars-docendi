# Proposal

## Why

Producción necesita un hostname público profesional en el dominio raíz y una instalación separada de los ambientes de prueba. El alcance anterior de despliegue duplicado y failover se sustituye por producción exclusiva en la PC Debian y staging/previews exclusivos en la VM Proxmox, evitando el coste y la complejidad de balanceadores.

## What Changes

- Publicar producción únicamente en `https://<DOMINIO>` mediante el túnel de Debian; conservar `prod` como nombre interno de Compose, base y GitHub Environment.
- Mantener `staging.<DOMINIO>` y `pr-N.<DOMINIO>` en Proxmox, usando su túnel existente y wildcard, sin alta de hostname por PR.
- Dirigir `deploy-prod` sólo al runner confiable `principal`, `deploy-staging` sólo al confiable `secundaria`, y previews/teardown sólo a Proxmox. Eliminar la matriz de deploy dual; conservar imágenes etiquetadas por SHA, filtros documentales y gates existentes.
- Mantener PostgreSQL, SeaweedFS y ClamAV locales e independientes en cada host; no sincronizar datos ni sesiones.
- **BREAKING (URL y ubicación):** retirar la publicación de `prod.<DOMINIO>` y la instalación productiva de Proxmox sólo después de validar Debian. El wildcard no deberá seguir sirviendo la aplicación bajo ese hostname antiguo.
- Excluir Cloudflare Load Balancing, Workers de conmutación DNS, monitores de failover y cualquier recuperación automática entre hosts. Una caída de Debian deja producción indisponible.
- Conservar el retiro ya ejecutado del servicio de limpieza periódica no utilizado; los previews se eliminan al cerrar el PR o mediante teardown manual.
- Cloudflare y runners los configura el operador. El repositorio proporciona código, documentación y criterios de verificación, no da por realizadas las configuraciones externas.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `plataforma-ambientes-efimeros`: dominio raíz para producción en Debian, subdominios no productivos en Proxmox, routing y datos locales por host, retirada de URL productiva anterior.
- `pipeline-deploy-ci`: un destino por ambiente, runners seleccionados por ubicación y secretos de GitHub sin despliegue duplicado.

## Impact

- `.github/workflows/deploy-prod.yml`, `deploy-staging.yml`, `pr-env-deploy.yml`, `pr-env-teardown.yml`, `infra/scripts/spin-up.sh`, plantillas de ingress y docs de arquitectura/provisioning/operación.
- Sin cambios en schemas, APIs de negocio, contratos cross-module ni grafo de dependencias. Revisar URLs absolutas y callbacks de autenticación existentes si corresponden al hostname anterior.
- Determinar antes del corte si los datos actuales de `prod` en Proxmox deben trasladarse a Debian mediante un backup verificable de PostgreSQL y objetos, o si se aprueba iniciar vacío. Este cambio no autoriza pérdida ni borrado implícitos de datos.
- Rollback: conservar el servicio y los datos anteriores hasta aceptar el corte; restaurar el DNS/ingress y workflow productivo anteriores si Debian falla. No usar scripts destructivos de previews para retirar producción.
- Se conserva el identificador histórico `deploy-dual-con-failover-temporal` para trazabilidad, aunque el nombre ya no describe el alcance vigente. La revisión no aplica ni revierte por sí misma los archivos de implementación previos.
