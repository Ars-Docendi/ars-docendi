## Purpose

Plataforma de ambientes para la aplicación web (frontend + backend): producción en el dominio raíz de Debian y staging/previews pr-N en subdominios de Proxmox. Define su routing por labels de contenedor vía Traefik, dos Cloudflare Tunnels independientes, PostgreSQL y SeaweedFS privados por host con aislamiento por ambiente, el templating de Compose y el tooling de operación manual. Mantiene datos y administración fuera del alcance público, sin replicación ni failover automático.

## Requirements

### Requirement: Modelo de ambientes

La plataforma SHALL soportar tres clases de ambiente para la aplicación web (frontend + backend): **prod** (asociado a la rama `main`), **staging** (asociado a la rama `develop`) y **pr-N** efímeros (uno por pull request abierto, identificados por el número de PR). Cada ambiente SHALL ser un Compose project independiente, nombrado de forma determinística a partir de su identificador (`prod`, `staging`, `pr-<N>`), de modo que `docker compose -p <id>` opere sobre un único ambiente sin afectar a los demás.

#### Scenario: Ambiente prod aislado de pr-N

- **WHEN** existe un ambiente `prod` corriendo y se levanta un ambiente `pr-123`
- **THEN** ambos corren como Compose projects separados (`-p prod` y `-p pr-123`)
- **AND** detener o destruir `pr-123` no afecta a los contenedores de `prod`

#### Scenario: Identificador determinístico por PR

- **WHEN** se solicita el ambiente del pull request número 123
- **THEN** el Compose project es `pr-123` y su hostname público es `pr-123.example.net`
- **AND** reabrir o re-deployar el mismo PR reutiliza el mismo identificador sin crear duplicados

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

### Requirement: Exposición de un único origin por ambiente

El túnel SHALL exponer **un solo origin por ambiente**: el frontend. Si la API debe ser pública, SHALL servirse bajo el path `/api` del mismo hostname y ser proxeada internamente hacia el backend del ambiente. Cualquier otro servicio del ambiente (backend directo, herramientas de administración) MUST NOT ser alcanzable a través del hostname público salvo bajo `/api`.

#### Scenario: API bajo /api del mismo host

- **WHEN** un cliente hace `GET https://pr-123.example.net/api/designaciones/ping`
- **THEN** Traefik enruta el request al backend del ambiente `pr-123` internamente
- **AND** la misma request a `https://pr-123.example.net/` sirve el frontend del ambiente

### Requirement: Fronteras de red — datos y administración nunca expuestos

Las bases de datos y los puertos de administración (incluido el dashboard de Traefik, el socket de Docker y cualquier puerto de management) MUST NOT exponerse a través del Cloudflare Tunnel bajo ninguna circunstancia. Estos servicios SHALL ser alcanzables únicamente por la red interna del host o por la red de administración fuera de banda (Tailscale), nunca por el wildcard público.

#### Scenario: Postgres no alcanzable por el túnel

- **WHEN** se intenta acceder a la base de datos desde fuera, a través de cualquier hostname `*.example.net`
- **THEN** el acceso es rechazado: el puerto de Postgres no está publicado en el ingress del túnel
- **AND** Postgres solo acepta conexiones desde la red interna de los contenedores de ambientes

#### Scenario: Dashboard de administración no público

- **WHEN** se intenta abrir el dashboard de Traefik o cualquier puerto de admin vía un hostname público
- **THEN** no hay ingress que lo exponga y el acceso falla
- **AND** el dashboard solo es accesible por la red de administración interna

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

### Requirement: Templating de Compose por ambiente

La definición de los servicios SHALL expresarse como una **base de Compose** parametrizada por un mecanismo de override/templating que, por ambiente, fije al menos: el hostname público, el tag de la imagen, el nombre del ambiente y el connection string de base de datos. El mismo template SHALL producir `prod`, `staging` y cualquier `pr-N` cambiando únicamente esos parámetros, sin duplicar la definición de los servicios por ambiente.

#### Scenario: Mismo template, parámetros distintos

- **WHEN** se materializa el ambiente `pr-150` con tag de imagen `sha-abc123` y hostname `pr-150.example.net`
- **THEN** se usa la misma definición base de servicios que prod y staging
- **AND** solo difieren hostname, tag de imagen, nombre de ambiente y connection string

### Requirement: Tooling de operación manual

El repositorio SHALL proveer scripts helper y/o un `Makefile` para levantar y destruir ambientes manualmente de forma idempotente, más un `.env.example` que documente todas las variables requeridas. Los scripts MUST NOT contener secretos; las credenciales SHALL leerse de variables de entorno en runtime.

#### Scenario: Spin-up y teardown manual idempotente

- **WHEN** un operador ejecuta el helper de spin-up para `pr-90` y luego el de teardown
- **THEN** el ambiente se crea (contenedores + base) y luego se destruye por completo (contenedores + base)
- **AND** re-ejecutar el teardown sobre un ambiente ya destruido no falla

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
