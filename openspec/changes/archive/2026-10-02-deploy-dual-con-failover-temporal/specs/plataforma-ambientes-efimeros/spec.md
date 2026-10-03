# Spec Delta

## MODIFIED Requirements

### Requirement: Routing por labels de contenedor

El reverse proxy interno (Traefik) SHALL descubrir y rutear los ambientes locales mediante labels de los contenedores. Dar de alta un preview nuevo MUST NOT requerir cambios en Traefik ni en Cloudflare. El routing SHALL resolverse por el Host header: producción usa el dominio raíz en Debian, staging y previews usan sus subdominios en Proxmox. La API SHALL conservar la ruta `/api` bajo el hostname del mismo ambiente.

#### Scenario: Alta de ambiente sin tocar config del proxy

- **WHEN** se levanta en Proxmox `pr-200` con label Host `pr-200.example.net`
- **THEN** Traefik lo rutea sin reiniciar ni editar configuración estática
- **AND** no se modifica Cloudflare

#### Scenario: Routing por Host header

- **WHEN** llega una petición con Host `staging.example.net` a Proxmox o Host `example.net` a Debian
- **THEN** cada Traefik entrega la petición a su frontend local de staging o producción respectivamente
- **AND** `/api` se entrega al backend del mismo ambiente

### Requirement: Ingreso público vía Cloudflare Tunnel wildcard

El acceso público SHALL hacerse mediante dos Cloudflare Tunnels de UUID distinto. El dominio raíz `example.net` SHALL dirigir producción únicamente al túnel Debian. `staging.example.net` y el wildcard `*.example.net` SHALL dirigir los ambientes no productivos a Proxmox, sin crear ni borrar un hostname por PR. Cloudflare SHALL terminar TLS; cloudflared, Traefik y contenedores SHALL usar HTTP interno con forwarding apropiado y sin ACME en Traefik. La plataforma MUST NOT requerir Load Balancing, un Worker de conmutación DNS ni failover automático. `prod.example.net` MUST NOT servir la aplicación productiva una vez aceptado el corte, incluso si ese hostname coincide con el wildcard de Proxmox.

#### Scenario: Nuevo PR alcanzable sin cambios en Cloudflare

- **WHEN** se levanta `pr-321` en Proxmox
- **THEN** `https://pr-321.example.net` es alcanzable por el wildcard de su túnel
- **AND** no se agrega ni modifica un hostname público por PR

#### Scenario: TLS terminado en Cloudflare

- **WHEN** un usuario navega a `https://example.net`
- **THEN** Cloudflare termina TLS y entrega la petición al Traefik Debian por su túnel
- **AND** Traefik no gestiona certificados para ese hostname

#### Scenario: Producción sólo en el dominio raíz

- **GIVEN** el corte fue aceptado
- **WHEN** se solicita `https://prod.example.net`
- **THEN** no se entrega el frontend ni API productivos a través del wildcard de Proxmox

#### Scenario: Caída de Debian sin conmutación

- **WHEN** Debian o su túnel productivo deja de responder
- **THEN** producción queda indisponible sin redirigir automáticamente usuarios a Proxmox
- **AND** staging y previews pueden seguir funcionando si Proxmox permanece disponible

### Requirement: Base de datos compartida con aislamiento por ambiente

Cada host SHALL tener PostgreSQL y SeaweedFS privados e independientes. Producción SHALL usar exclusivamente los servicios locales de Debian; staging y `pr-N` SHALL usar los de Proxmox con una base/schema y bucket aislados por ambiente. Los connection strings SHALL inyectarse en runtime y apuntar al host que ejecuta el ambiente. Crear o destruir un preview MUST NOT afectar recursos productivos. Las máquinas MUST NOT sincronizar automáticamente bases, objetos o sesiones.

#### Scenario: Cada ambiente con su base aislada

- **WHEN** existen staging, `pr-100` y `pr-101` en Proxmox
- **THEN** cada uno tiene su base/schema aislado en su PostgreSQL local
- **AND** escrituras de un preview no son visibles en otro ambiente ni en producción Debian

#### Scenario: Datos no productivos en ambientes no-prod

- **WHEN** se aprovisiona staging o un preview
- **THEN** se siembra con datos sintéticos o anonimizados
- **AND** nunca se carga una copia de datos productivos reales

## ADDED Requirements

### Requirement: Distribución de ambientes por host

Producción SHALL ejecutarse sólo en la PC Debian y conservar `prod` como identificador interno de proyecto, base y GitHub Environment. Staging, previews y sus operaciones de teardown SHALL ejecutarse sólo en Proxmox. Retirar la VM en el futuro MUST NOT detener producción ni mover implícitamente staging/previews a Debian.

#### Scenario: Identidad productiva independiente del hostname público

- **WHEN** se despliega producción bajo `example.net`
- **THEN** el proyecto y Environment siguen identificándose como `prod`
- **AND** no se renombra la base `arsdocendi_prod` únicamente por cambiar la URL

#### Scenario: Retiro futuro de Proxmox

- **WHEN** el operador aprueba retirar la VM
- **THEN** producción de Debian continúa sin depender de servicios de Proxmox
- **AND** staging y previews dejan de servirse salvo aprobación de otro destino
