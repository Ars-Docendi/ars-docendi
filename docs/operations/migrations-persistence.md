# Migraciones, persistencia y recuperación por ambiente

Este procedimiento implementa el cambio `mejorar-migraciones-sql-y-persistencia-ambientes`.
No certifica activación en servidores ni autoriza un corte productivo. Producción
permanece exclusivamente en Debian/principal; staging y pr-N en Proxmox/secundaria.
No incluye reaper, separación de privilegios ni upgrades del historial alpha retirado.

## Deploy incremental y exclusión

`infra/scripts/spin-up.sh <prod|staging|pr-N>`:

1. Valida inputs y reserva `arsdocendi-lock-<ambiente>` en el daemon Docker.
   El nombre único es una reserva atómica compartida por deploy, teardown,
   backup, seed y reset, incluso si los runners no comparten `/tmp`.
2. Revalida que un PR siga abierto por la API de GitHub, bajo ese lock.
   En ejecución manual, verificar el PR antes de operar; en CI se exige el token.
3. Aprovisiona sólo lo faltante. Una base recién creada registra
   `public.bootstrap_metadata` con procedencia `provision-db/v1`.
4. Consulta estado y genera preview **desde la misma imagen candidata**, sin
   listener y sin binds. Guarda estado, SQL y manifiesto en el volumen privado.
5. Con pendientes, detiene el backend (escritor conocido) antes del backup. Para
   una base existente obtiene un conjunto PostgreSQL + objetos; para una nueva
   registra ausencia de estado previo. Sin pendientes no inventa backup ni migra.
   Los operadores deben detener también cualquier escritor externo conocido.
6. Reconfigura SeaweedFS sin eliminar bucket/objetos. Los PRs detienen el backend
   también cuando no hay pendientes, antes de rotar su credencial por ejecución.
7. Reconsulta estado y compara con el observado antes de `--migrate`. Un historial
   cambiado/incompatible aborta; nunca ejecuta ciegamente el SQL del preview.
8. Inicializa seed no productivo únicamente si corresponde, publica y verifica
   referencias por SHA, cuatro pings, identidad/conectividad DB con la cuenta de
   app en transacción read-only y cero pendientes.
9. Guarda recibo privado ligado a ambiente/release/historial/backup y aplica
   retención sólo a snapshots de despliegues exitosos.

Backup/migración/seed fallidos no publican. Un smoke fallido detiene el candidato;
no produce éxito ni reinicia automáticamente la versión vieja sobre un schema
posiblemente actualizado. El snapshot y preview quedan para recuperación manual.
Los contextos no tienen una transacción global ni rollback automático.

Los workflows conservan refs de acciones fijadas por SHA, gates de maintainer,
Environments y hosts anteriores. Deploy y teardown de PR usan el mismo grupo
`pr-env-N` con `cancel-in-progress: false`. GitHub no garantiza FIFO ni conservar
todas las ejecuciones pendientes; el lock del daemon evita intercalado/manuales.

### Lock abandonado

Una interrupción externa no libera automáticamente una reserva abandonada:
falla cerrado. Antes de eliminar **ese** contenedor de lock, un operador debe
confirmar que ningún job/proceso ni escritor siga activo, registrar el incidente
y conservar los respaldos. No hay reaper ni TTL que interrumpa una migración.
`LOCK_PREFIX` sólo sirve para aislar ensayos desechables; en operación se conserva
el default común. Los hijos heredan una referencia de propietario verificable,
no adquieren ni liberan por error el lock del padre.

## Contrato CLI de la imagen

```text
dotnet ArsDocendi.Host.dll --estado-migraciones
dotnet ArsDocendi.Host.dll --script-migraciones -
dotnet ArsDocendi.Host.dll --script-migraciones <directorio>
dotnet ArsDocendi.Host.dll --migrate
```

Estado stdout es exclusivamente JSON:

```json
{
  "baseDatos": "arsdocendi_staging",
  "contextos": [
    {
      "contexto": "Identity",
      "disponibles": ["ID-reconocido"],
      "aplicadas": [],
      "pendientes": ["ID-reconocido"]
    }
  ],
  "pendientes": 1,
  "compatible": true
}
```

El ejemplo ilustra el formato, **no** el inventario real. El wrapper valida el
nombre determinístico de base, prefijo continuo aplicado y suma de pendientes.
Una incompatibilidad devuelve exit no cero sin writes. `--script-migraciones -`
emite un tar binario stdout con SQL separados y `manifiesto.json`; logs sólo
stderr. Esa variante evita binds cuando daemon/runner no comparten filesystem.
El directorio existe como interfaz de operador; el deploy consume el tar.
Preview representa intervalos pendientes del historial, no schema diff ni
prueba de ausencia de drift manual. Se revalida justo antes de aplicar.

## Variables y scopes (sin secretos nuevos)

| Scope                                        | Nombres                                                                                              |
| -------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| Repository variables                         | `REGISTRO`, `DOMINIO`                                                                                |
| Repository secrets                           | `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, `SEAWEEDFS_ROOT_ACCESS_KEY`, `SEAWEEDFS_ROOT_SECRET_KEY` |
| Environment `prod` secret                    | `APP_DB_PASSWORD_PROD`                                                                               |
| Environment `staging` secret                 | `APP_DB_PASSWORD_STAGING`                                                                            |
| Environment `pr-preview` secret              | `APP_DB_PASSWORD_PREVIEW`                                                                            |
| Vars opcionales de cada Environment anterior | `BACKUP_VOLUME_PREFIX`, `BACKUP_RETENTION_DAYS`                                                      |

`BACKUP_VOLUME_PREFIX=arsdocendi-backups` produce un volumen por ambiente, por
host: `arsdocendi-backups-prod`, `arsdocendi-backups-staging`,
`arsdocendi-backups-pr-N`. Prefix admite un nombre Docker sin barras.
`BACKUP_RETENTION_DAYS=7` es un entero positivo; vacío usa el default,
0/negativo/texto se rechazan antes de parar o aprovisionar. No son secrets.
Cambiar el prefix no migra respaldos existentes: inventariar ambos volúmenes.

`APP_DB_USER` se deriva por ambiente y `APP_DB_PASSWORD` se inyecta desde su
secret existente. Credenciales app SeaweedFS prod/staging se derivan de raíz;
las PR se generan en runtime, se enmascaran y rotan en mantenimiento sin purge.
No crear un inventario de secretos adicional ni volver a `MINIO_ROOT_*`.
Una rotación de raíz exige mantenimiento coordinado en ambos hosts y clientes;
no basta con cambiar sólo GitHub. No inspeccionar variables secretas de un
contenedor como mecanismo de descubrimiento.

Requisitos del runner: Bash, Docker/Compose, Python 3.12+, OpenSSL, tar, curl.
PostgreSQL/AWS/wget se ejecutan en contenedores. No requiere psql instalado ni
un filesystem compartido con el daemon.

## Seed único

`seed.sh` exige procedencia reconocida de creación, ambiente coincidente y marca
completa. No considera autorización un simple `SEED_BASE_CREATED=true` ni la
existencia de `seed_metadata`. Antes de la primera tentativa guarda una huella
de las tablas; después de una falla sólo reintenta si la huella no cambió.
Una base poblada sin autorización o modificada tras una tentativa fallida se
rechaza sin sobrescribir. No se adopta automáticamente una base antigua.

Las fixtures conservan su transacción/advisory lock; se transmiten por stdin.
Los bytes sintéticos se cargan antes y el dataset, metadata de adjuntos,
`seed_metadata.inicializacion_completada=sintetico/v1` y estado bootstrap
`completado` se confirman en una única transacción. Si falla SQL no hay marca
completa ni filas parciales. Los objetos subidos en una tentativa fallida quedan
retenidos para retry; no se publican automáticamente.

Un redeploy o una versión distinta de `SEED_SQL` no ejecuta reseed: conserva las
ediciones y objetos. Seed prod/copia de bases/SGA en deploy se rechazan.
`seed-local.sh`, invocado por `scripts/setup.sh`, autoriza sólo una base local
vacía antes de migrar y confirma sintético + SGA juntos. SGA se provee mediante
`SEED_SGA_FILE`, queda privado, no se inspecciona ni versiona, y su ausencia no
marca éxito parcial. Una base local vieja poblada sin marca requiere decisión
explícita; no se pisa para completar setup.

Reset de staging/PR es una operación separada y destructiva:

```bash
RESET_AUTHORIZED=pr-123 infra/scripts/reset.sh pr-123
# Teardown explícito/al cierre: conserva backups, no reconstruye.
infra/scripts/teardown.sh pr-123
```

Reset y teardown rechazan prod. Un evento de cierre perdido exige teardown
manual después de verificar el PR; no existe limpieza periódica.

## Backups privados y retención

```bash
# Sólo con escritores detenidos y credenciales inyectadas por el canal seguro.
snapshot="$(infra/scripts/backup-storage.sh staging)"
infra/scripts/backup-volume.sh staging list
infra/scripts/backup-volume.sh staging verify "$snapshot"
```

`backup-storage.sh` reutiliza formato `arsdocendi-storage-backup/v1`:
`postgres.dump`, bytes `objects/`, manifiesto (claves/tamaño/ETag/content type/
metadata/hash) y checksums. Dump y bytes van directamente por stdio al volumen,
no se almacenan en el workspace. Sólo metadata transitoria privada se procesa
con Python. Stdout devuelve exclusivamente el ID; diagnósticos van a stderr.
La exportación opcional a directorio se limita a datos no productivos.

Un snapshot empieza `incomplete`, y sólo pasa a `complete` después de obtener y
verificar todo el conjunto. Los scripts rechazan reescribir snapshots completos;
son inmutables operacionalmente, **no** WORM frente a un administrador Docker.
Un snapshot completo no implica deploy exitoso: sin `deploys/ID.success` queda
`retained-manual`. Fallas (completas o incompletas) no se eliminan por retención.
Sólo se eliminan snapshots completos con deploy exitoso y antigüedad mayor al
plazo, dentro del volumen del ambiente solicitado. Teardown/reset nunca borran
ese volumen. Preview/recibos privados tampoco se publican como artifacts de PR.

Un administrador Docker puede leer el volumen; no es aislamiento frente a ese
administrador ni cifrado automático. Restringir acceso al host/daemon, controlar
capacidad y cifrar cualquier exportación externa según política institucional.
La limpieza de fallidos requiere inventario y autorización explícita; nunca
`docker volume prune` indiscriminado. El volumen local no sustituye una copia
institucional fuera del host, un SLA acordado ni un drill periódico.

## Recovery aislado (sin restore directo de prod)

```bash
# Ejemplo sintético, mismo host; rol app existente, destino nuevo sin ingress.
RECOVERY_AUTHORIZED=recovery-ensayo \
  infra/scripts/restore-storage.sh recovery-ensayo "volume:staging:$snapshot"
```

El script verifica primero checksums, rechaza base poblada/bucket no vacío/
runtime activo, nunca DROPpea un destino y restaura por streams. Verifica bytes,
tamaño, content type y metadata de objetos. Una falla deja el destino parcial
para diagnóstico, no lo resetea ni publica automáticamente.

Para datos prod: sólo `volume:prod:ID` a `recovery-<id>`,
`RECOVERY_AUTHORIZED` igual al destino y `RECOVERY_HOST_ROLE=principal`. Usar
identidad PostgreSQL app **existente** y credenciales administrativas actuales;
no rotar secretos ni copiar datos a Proxmox, staging o PR públicos. La variable
de rol es una declaración operativa: el operador verifica el host físico antes
de autorizar; no demuestra por sí sola ubicación ni permisos institucionales.
No exponer el destino al wildcard/Traefik.

Después, validar tablas/historial y una release compatible, comprobar referencias
metadata/bytes y descargar muestras con credenciales de recovery restringidas.
No cambiar automáticamente los buckets registrados en PostgreSQL ni reenrutar
tráfico. La adaptación explícita de referencias/configuración, credenciales de
recovery y corte de destino requieren un plan aprobado con escritores detenidos,
backup de cualquier escritura posterior y aceptación independiente. El restore
no autoriza ese corte ni garantiza recuperación de escrituras posteriores al dump.

## Ensayos reproducibles

```bash
node --test infra/tests/*.test.mjs
INFRA_DOCKER_TESTS=1 node --test infra/tests/storage-recovery.integration.mjs infra/tests/lock.integration.mjs
# Requiere construir primero la imagen candidata local:
docker build -t arsdocendi-migraciones:verificacion -f backend/Dockerfile .
INFRA_DOCKER_TESTS=1 node --test infra/tests/migration-candidate.integration.mjs
bash -n infra/scripts/*.sh
```

Los ensayos Docker crean sólo redes/contenedores/volúmenes de nombre aleatorio
registrados por su fixture, PostgreSQL 18 y SeaweedFS reales, sin binds. Cubren
restore de filas/bytes/metadata, rechazo de destino poblado, supervivencia al
cliente/runner, rotación de credencial conservando objeto, fallo de dump,
retención selectiva, seed atómico/ediciones/retry/autorización y exclusión entre
procesos. Las pruebas de orquestación usan fronteras sintéticas de Docker/CLI
para cubrir no-op y fallas. `migration-candidate.integration.mjs` usa la imagen
real y un upgrade creado/compilado por el scaffolder en un checkout desechable:
verifica instalación, tar preview, seed único, backup conjunto, aplicación
incremental, preservación de datos/bytes, no-op y cuatro pings reales. No despliega
ambientes compartidos ni agrega una matriz de upgrades de releases históricas.
