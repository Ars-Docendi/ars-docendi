## ADDED Requirements

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

El reverse proxy interno (Traefik) SHALL descubrir y rutear ambientes mediante el provider de Docker, leyendo **labels de los contenedores**. Dar de alta un ambiente nuevo MUST NOT requerir cambios en la configuración estática de Traefik ni en la configuración de Cloudflare. El routing a cada ambiente SHALL resolverse por el `Host` header del request contra la regla declarada en los labels del contenedor frontend del ambiente.

#### Scenario: Alta de ambiente sin tocar config del proxy

- **WHEN** se levanta un ambiente nuevo `pr-200` cuyo contenedor frontend declara la label de Host `pr-200.example.net`
- **THEN** Traefik empieza a rutear `pr-200.example.net` al contenedor sin reiniciar Traefik ni editar su config estática
- **AND** no se modifica ninguna configuración en Cloudflare

#### Scenario: Routing por Host header

- **WHEN** llega un request con `Host: staging.example.net`
- **THEN** Traefik lo entrega al contenedor frontend del Compose project `staging`
- **AND** un request con `Host: prod.example.net` se entrega al contenedor de `prod`

### Requirement: Ingreso público vía Cloudflare Tunnel wildcard

El acceso público SHALL hacerse exclusivamente a través de un Cloudflare Tunnel con un **único ingress wildcard** (`*.example.net → cloudflared → Traefik`). El sistema MUST NOT crear ni borrar un public hostname de Cloudflare por cada PR; todos los ambientes (`prod`, `staging`, `pr-N`) SHALL ser alcanzables a través del mismo wildcard. Cloudflare SHALL terminar TLS; el tráfico entre cloudflared y Traefik, y entre Traefik y los contenedores, SHALL ser HTTP interno. Traefik SHALL configurarse asumiendo terminación de TLS aguas arriba (confiar en los headers de forwarding apropiados y no intentar emitir certificados propios para estos hostnames).

#### Scenario: Nuevo PR alcanzable sin cambios en Cloudflare

- **WHEN** se levanta `pr-321` y su contenedor declara el Host `pr-321.example.net`
- **THEN** `https://pr-321.example.net` es alcanzable públicamente a través del túnel existente
- **AND** no se agregó ni modificó ningún public hostname en la configuración de Cloudflare

#### Scenario: TLS terminado en Cloudflare

- **WHEN** un usuario navega a `https://prod.example.net`
- **THEN** Cloudflare termina TLS y reenvía la petición por el túnel a Traefik en HTTP interno
- **AND** Traefik no intenta gestionar certificados para ese hostname

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

Una única instancia de PostgreSQL en su propio contenedor SHALL ser compartida por `prod`, `staging` y todos los `pr-N`, pero cada ambiente SHALL recibir su propia base/schema lógicamente aislada, identificada de forma determinística por el ambiente. El connection string de cada ambiente SHALL inyectarse en runtime apuntando a su base/schema. Crear un ambiente nuevo SHALL aprovisionar su base/schema; destruirlo SHALL eliminarla.

#### Scenario: Cada ambiente con su base aislada

- **WHEN** existen `staging`, `pr-100` y `pr-101`
- **THEN** cada uno opera contra su propia base/schema dentro de la misma instancia Postgres
- **AND** escrituras en `pr-100` no son visibles desde `pr-101` ni desde `staging`

#### Scenario: Datos no productivos en ambientes no-prod

- **WHEN** se aprovisiona la base de un ambiente `staging` o `pr-N`
- **THEN** se siembra con datos sintéticos o un snapshot anonimizado
- **AND** nunca se carga una copia de datos productivos reales

### Requirement: Templating de Compose por ambiente

La definición de los servicios SHALL expresarse como una **base de Compose** parametrizada por un mecanismo de override/templating que, por ambiente, fije al menos: el hostname público, el tag de la imagen, el nombre del ambiente y el connection string de base de datos. El mismo template SHALL producir `prod`, `staging` y cualquier `pr-N` cambiando únicamente esos parámetros, sin duplicar la definición de los servicios por ambiente.

#### Scenario: Mismo template, parámetros distintos

- **WHEN** se materializa el ambiente `pr-150` con tag de imagen `sha-abc123` y hostname `pr-150.example.net`
- **THEN** se usa la misma definición base de servicios que prod y staging
- **AND** solo difieren hostname, tag de imagen, nombre de ambiente y connection string

### Requirement: Reaper de ambientes huérfanos

La plataforma SHALL incluir un reaper (script invocado por un systemd timer o cron) que elimine los ambientes `pr-N` cuya antigüedad supere un umbral configurable de N días, incluyendo sus contenedores y su base/schema. El reaper MUST NOT tocar los ambientes `prod` ni `staging`. Su objetivo es garantizar que un webhook de cierre de PR perdido no deje ambientes colgados indefinidamente.

#### Scenario: Reaper borra ambiente vencido

- **WHEN** un ambiente `pr-77` lleva más de N días activo y el reaper corre
- **THEN** el reaper destruye los contenedores de `pr-77` y elimina su base/schema
- **AND** registra la acción en logs estructurados

#### Scenario: Reaper preserva prod y staging

- **WHEN** el reaper corre con `prod` y `staging` activos desde hace más de N días
- **THEN** no toca ni `prod` ni `staging`
- **AND** solo considera ambientes con prefijo `pr-`

### Requirement: Tooling de operación manual

El repositorio SHALL proveer scripts helper y/o un `Makefile` para levantar y destruir ambientes manualmente de forma idempotente, más un `.env.example` que documente todas las variables requeridas. Los scripts MUST NOT contener secretos; las credenciales SHALL leerse de variables de entorno en runtime.

#### Scenario: Spin-up y teardown manual idempotente

- **WHEN** un operador ejecuta el helper de spin-up para `pr-90` y luego el de teardown
- **THEN** el ambiente se crea (contenedores + base) y luego se destruye por completo (contenedores + base)
- **AND** re-ejecutar el teardown sobre un ambiente ya destruido no falla
