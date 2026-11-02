# Design

## Context

Ver `proposal.md` para el alcance revisado. La VM Proxmox existente aloja staging y previews, runners confiable/efímero y el túnel wildcard. Hay una instalación `prod` anterior allí; la PC Debian es el destino previsto de producción. El árbol contiene una implementación parcial del alcance anterior (matrices duales y documentación de LB) que deberá adaptarse: este documento no afirma que el corte nuevo ya esté operativo.

## Goals / Non-Goals

**Goals:** producción en el dominio raíz de Debian; staging y previews únicamente en Proxmox; secretos de aplicación desde GitHub; rutas estables, separación de recursos y plan de corte reversible.

**Non-Goals:** despliegues de producción/staging duplicados, Cloudflare Load Balancing, Workers de failover, replicación de datos/sesiones, failover automático, renombrar `prod` internamente, modificar APIs de negocio. El retiro futuro de la VM implica perder staging/previews salvo decisión adicional; no convierte la PC automáticamente en su nuevo destino.

## Decisions

### 1. Un host por clase de ambiente

| Ambiente  | Host       | Runner de deploy                                 | Hostname            |
| --------- | ---------- | ------------------------------------------------ | ------------------- |
| `prod`    | PC Debian  | `self-hosted, arsdocendi, confiable, principal`  | `<DOMINIO>`         |
| `staging` | VM Proxmox | `self-hosted, arsdocendi, confiable, secundaria` | `staging.<DOMINIO>` |
| `pr-N`    | VM Proxmox | `self-hosted, arsdocendi, efimero, secundaria`   | `pr-N.<DOMINIO>`    |

Teardown de PR: confiable `secundaria`. El registro persistente de los efímeros conserva esa etiqueta, no sólo el ID que esté online. El operador configura runners y Cloudflare; sólo se registran metadatos, nunca valores de secretos en docs.

Cada host mantiene redes Docker y datos locales. Debian requiere PostgreSQL, SeaweedFS prod y ClamAV para producción. Proxmox conserva PostgreSQL, SeaweedFS no-prod y ClamAV para staging/previews. Los aliases internos pueden repetirse; no se comparten volúmenes entre hosts.

### 2. Construcción por SHA y un solo deploy por workflow

Conservar la separación build/push y deploy de cada workflow permanente, pero eliminar su matriz de ubicaciones. `main` actualiza exclusivamente Debian; `develop`, exclusivamente Proxmox. Build publica una pareja de imágenes por SHA, deploy las obtiene y verifica su referencia en contenedores locales. Mantener filtros doc-only, serialización por ambiente y permisos mínimos. No se necesita `fail-fast` ni comparar el mismo SHA entre dos instalaciones productivas.

GitHub repository variables: `DOMINIO`, `REGISTRO`. Repository secrets: `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, `SEAWEEDFS_ROOT_ACCESS_KEY`, `SEAWEEDFS_ROOT_SECRET_KEY`. Environments: `APP_DB_PASSWORD_PROD`, `APP_DB_PASSWORD_STAGING`, `APP_DB_PASSWORD_PREVIEW` en su scope respectivo. Validar que cada instancia inicializada acepte los secretos autorizados sin leerlos ni imprimirlos. No cambiar de scope silenciosamente. Los daemons permanentes de runners y túneles necesitan bootstrap protegido fuera de un job; no es un segundo inventario de contraseñas de aplicación.

### 3. Dominio raíz sin renombrar producción internamente

`spin-up.sh` debe asignar `HOST_PUBLICO=${DOMINIO}` para `prod` y `${ambiente}.${DOMINIO}` para staging/previews. Conservar `docker compose -p prod`, `arsdocendi_prod`, bucket de prod, GitHub Environment `prod` y trigger `main`. Las labels de Traefik consumen el hostname raíz sin rediseñar el template Compose.

Producción publica frontend en `/` y API en `/api` del mismo dominio raíz. Revisar configuraciones reales de callbacks SSO, allowed origins y enlaces absolutos cuando estén disponibles; no suponer que ya usan el dominio raíz ni inventar credenciales o URLs de callback.

### 4. Dos túneles, sin balanceadores ni automatismos DNS

Conservar UUID/rutas del túnel Proxmox cuando sea posible. Debian lleva un UUID diferente y sólo la ruta exacta `<DOMINIO>` hacia su Traefik. Proxmox conserva `staging.<DOMINIO>` y `*.<DOMINIO>` para previews. En modo local materializar los YAML fuera de Git; en modo remoto configurar las rutas equivalentes en el panel con token. No asumir que una plantilla YAML configura automáticamente un túnel remoto.

DNS objetivo (proxy activado): CNAME `@` al UUID Debian; CNAME `*` al UUID Proxmox; CNAME `staging` al UUID Proxmox si se desea hacerlo explícito. Usar soporte de flattening del apex de Cloudflare, no direcciones IP públicas ni Load Balancing. El operador conserva la política Access de staging/previews que corresponda y valida el acceso autorizado.

El wildcard también captura `prod.<DOMINIO>` si no hay una regla más específica. Por eso retirar sólo el CNAME antiguo no basta: en Proxmox añadir una regla explícita `prod.<DOMINIO> -> http_status:404` **antes** del wildcard y, después del corte aceptado, detener la aplicación productiva anterior. No añadir redirección permanente al dominio raíz sin autorización adicional. La PC no admite staging ni previews. No hay conmutación ante caídas de ningún host.

Alternativas descartadas: LB pago, Worker programado que altere DNS y réplicas compartiendo UUID; son innecesarios para separar ambientes y volverían a introducir conmutación no solicitada.

## Risks / Trade-offs

- [Debian caído] → Producción queda indisponible; alertas/smoke checks no implican failover.
- [Datos previos de prod en Proxmox] → Antes del corte el operador decide transferencia por backup/restore verificable o inicio vacío aprobado. No borrar ni crear un rollback que descarte escrituras posteriores.
- [Prod reaparece por wildcard] → Regla de rechazo anterior al wildcard y comprobación desde DNS público y cada ingress; no basta borrar un registro DNS.
- [Scopes GitHub incompatibles con instancias existentes] → Probar cada destino sin exponer valores; cualquier rotación debe ser coordinada.
- [SSO/redirecciones al hostname anterior] → Revisar callbacks/allowed origins reales y login institucional bajo el dominio raíz antes de aceptar el corte.
- [PostgreSQL de Proxmox publicado en todas las interfaces, observado previamente] → Corregir con plan de mantenimiento y backup; no recrear contenedor de datos a ciegas.
- [Evidencia de tests del diseño dual] → Invalidar checklist y adaptar tests/documentación; una validación sintáctica anterior no prueba el nuevo comportamiento.

## Migration Plan

1. Adaptar scripts, workflows, ingress y documentación al nuevo alcance; validar localmente reglas de host y destino de cada ambiente. Conservar revisión humana antes de reanudar implementación.
2. Operador prepara Debian y su runner `principal`, confirma servicio productivo y autenticación/secretos, y mantiene staging/previews en Proxmox. Resolver el tratamiento de datos productivos anteriores antes de modificar DNS.
3. Operador configura la ruta exacta raíz de Debian y su CNAME apex. Mantener wildcard/staging de Proxmox; documentar valores anteriores de DNS/ingress para rollback. Verificar frontend, API, login y carga/descarga de adjuntos en el dominio raíz.
4. Aceptado el corte, bloquear `prod.<DOMINIO>` mediante regla exacta en Proxmox y retirar su runtime productivo sin borrar datos hasta aprobación. Verificar staging y un ciclo completo de preview/teardown intactos.
5. Rollback: restaurar DNS/ingress y deploy previo si corresponde, considerando las escrituras confirmadas en Debian desde el corte. No mantener dos prod públicas simultáneamente como si tuvieran datos sincronizados.
6. Cuando se apruebe retirar la VM, decidir el destino de staging/previews, cerrar sus recursos y quitar wildcard/túnel/runners secundarios; producción en Debian no depende de ellos.

## Open Questions

El tratamiento de los datos productivos existentes y los callbacks SSO requiere decisiones operativas antes del corte. Son gates de activación, no alternativas al diseño; no se infiere permiso para borrarlos o empezar producción vacía.
