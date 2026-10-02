# Cloudflare Tunnel — producción y ambientes de prueba separados

La PC Debian principal y la VM Proxmox secundaria ejecutan **túneles distintos**.
Cada túnel llega sólo al Traefik de su propio host por la red Docker `traefik`;
PostgreSQL, SeaweedFS y administración permanecen fuera del ingreso público.
Producción vive únicamente en Debian; staging y previews, únicamente en Proxmox.
No hay Load Balancing, Worker DNS, replicación ni failover. Una caída de Debian
hace que producción quede indisponible.

| Host                    | Plantilla versionada   | Hostnames admitidos                                        |
| ----------------------- | ---------------------- | ---------------------------------------------------------- |
| PC Debian (principal)   | `config-principal.yml` | `<DOMINIO>`                                                |
| VM Proxmox (secundaria) | `config.yml`           | `staging.<DOMINIO>`, `*.<DOMINIO>`; antiguo prod bloqueado |

El nombre histórico del cambio `deploy-dual-con-failover-temporal` no describe
el alcance revisado. **El operador configura Cloudflare y los conectores**;
estas instrucciones no certifican el corte ni una configuración externa activa.

## Gestión local o remota

Identificar el modo del túnel existente en Proxmox antes de modificarlo:

- **Localmente gestionado:** YAML y JSON de credenciales. `cloudflared` no
  interpola `DOMINIO` ni `TUNNEL_ID` dentro de YAML. El operador materializa en
  **cada host**, fuera de Git, `/etc/cloudflared/config.yml` a partir de su
  plantilla, sustituyendo `example.net` y el UUID correspondiente. El campo
  `credentials-file` debe coincidir con `/etc/cloudflared/credentials.json`
  dentro del contenedor. Montar ambos archivos protegidos, nunca la plantilla
  con `REEMPLAZAR_*`. Ver el comando de arranque en [infra/README.md](../README.md#4-cloudflare-tunnel--cloudflared).
- **Remotamente gestionado:** configurar en el panel las rutas equivalentes y
  ejecutar el conector con su token de servicio, protegido fuera del repo.
  Los YAML versionados son referencia: no se aplican automáticamente al túnel
  remoto. Verificar allí el rechazo exacto del hostname antiguo y su prioridad.

No versionar JSON de credenciales, tokens ni archivos generados con valores
reales. Las credenciales del daemon son de servicio, no secretos de aplicación
ni sustitutos de los scopes de GitHub Actions.

## Rutas objetivo

El orden es significativo: la primera regla coincidente gana.

**Debian (`config-principal.yml`):**

1. `<DOMINIO>` → `http://traefik:80`.
2. Catchall → `http_status:404`.

**Proxmox (`config.yml`):**

1. `prod.<DOMINIO>` → `http_status:404`, **antes del wildcard**.
2. `staging.<DOMINIO>` → `http://traefik:80`.
3. `*.<DOMINIO>` → `http://traefik:80` para previews existentes.
4. Catchall → `http_status:404`.

La PC no admite staging ni previews. El wildcard de la VM no crea ambientes:
Traefik sólo sirve los contenedores autorizados que existen allí. Retirar el
CNAME antiguo de `prod` no basta: el wildcard también captura ese hostname.
No añadir una redirección permanente a la raíz sin autorización adicional.

## DNS objetivo (operador)

| Registro CNAME con proxy | Destino                              | Función                       |
| ------------------------ | ------------------------------------ | ----------------------------- |
| `@`                      | `<UUID_PRINCIPAL>.cfargotunnel.com`  | producción en el dominio raíz |
| `*`                      | `<UUID_SECUNDARIA>.cfargotunnel.com` | previews en Proxmox           |
| `staging` (opcional)     | `<UUID_SECUNDARIA>.cfargotunnel.com` | staging explícito en Proxmox  |

Usar el soporte de flattening del apex de Cloudflare; no publicar IP de los
hosts ni crear balanceadores. Reutilizar el UUID, conector y wildcard actuales
de Proxmox cuando estén operativos. Debian necesita otro UUID. No crear un
registro DNS por PR. Conservar y verificar la política Access institucional
para staging/previews; comprobar el acceso autorizado sin abrirlos al público
como solución a un smoke check bloqueado.

## Verificación antes del corte

Para túneles locales, ejecutar sobre la configuración **materializada** de
cada host (no sobre secretos ni sobre un YAML de otro host):

```bash
cloudflared tunnel --config /etc/cloudflared/config.yml ingress validate
cloudflared tunnel --config /etc/cloudflared/config.yml ingress rule https://example.net
cloudflared tunnel --config /etc/cloudflared/config.yml ingress rule https://staging.example.net
cloudflared tunnel --config /etc/cloudflared/config.yml ingress rule https://pr-321.example.net
cloudflared tunnel --config /etc/cloudflared/config.yml ingress rule https://prod.example.net
```

Sustituir `example.net` por el dominio real antes de probar. La raíz debe llegar
al Traefik de Debian y al catchall 404 de Proxmox; staging y el preview deben
llegar sólo al Traefik de Proxmox. El hostname antiguo `prod` debe recibir 404
en ambos. La regla de bloqueo de Proxmox se activa durante el corte aprobado,
no mientras se depende aún de la URL productiva anterior.

En modo remoto, comprobar las rutas del panel y probar cada conector: los
comandos sobre YAML no verifican ese modo. La selección local de reglas no
prueba DNS ni conectividad. Verificar además frontend y `GET
/api/designaciones/ping` públicos, login real y carga/descarga autenticada de
adjuntos. Un ping 200 no demuestra salud de PostgreSQL, SeaweedFS o ClamAV.

## Corte y reversión

1. Registrar DNS, rutas, UUID y workflow productivo anteriores. Preparar Debian
   sin alterar el wildcard existente de Proxmox ni publicar dos prod con
   escrituras independientes.
2. El operador decide explícitamente **transferir datos** mediante backup/restore
   conjunto verificable de PostgreSQL y objetos, o **iniciar vacío** con
   aprobación registrada. No inferir descarte ni ejecutar scripts destructivos
   de previews contra prod. Ver [storage-runbook.md](../../docs/operations/storage-runbook.md#corte-de-producción-de-proxmox-a-debian).
3. Acordar ventana de mantenimiento y detener/controlar escrituras del prod
   anterior para un corte coherente. Verificar los callbacks SSO, allowed
   origins y enlaces absolutos reales, sin inventar URLs de callback.
4. El operador activa apex → Debian, valida producción y, aceptado el corte,
   aplica el bloqueo exacto `prod.<DOMINIO>` en Proxmox y retira su runtime
   productivo. **Conservar base, objetos y backups anteriores hasta autorización
   explícita**, además de comprobar staging y un ciclo preview/teardown.
5. Si falla, restaurar DNS/ingress y workflow productivo anteriores de forma
   coordinada. Si Debian ya recibió escrituras, resolver su recuperación antes
   de volver a Proxmox: no descartar cambios ni presentar el rollback como
   failover automático. No borrar túneles o datos durante la reversión.

El retiro futuro de Proxmox requiere decidir el destino de staging/previews y
el tratamiento de sus datos antes de apagar túnel/runners/VM. No los traslada
implícitamente a Debian; producción es independiente de esos recursos.
