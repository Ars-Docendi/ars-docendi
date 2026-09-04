## Why

El sistema va a incorporar un **asistente conversacional de consulta**: el usuario pregunta en español y el asistente responde con datos que ya están en la base y que su rol tiene derecho a ver. La definición completa —objetivo, casos de uso, requisitos y arquitectura— vive en [`docs/product/designs/asistente-conversacional-definicion.md`](../../../docs/product/designs/asistente-conversacional-definicion.md).

Ese asistente ejecuta **SQL generada por un modelo en tiempo de ejecución**. Es una diferencia de naturaleza con el resto del sistema: en `Controller → Service → Repository` la consulta la escribimos nosotros y el conjunto de consultas posibles es finito y conocido en compilación; acá el conjunto es infinito y desconocido hasta el runtime. No hay dónde poner el `if` de autorización.

Este cambio **no construye el asistente**. Construye el sustrato de seguridad que lo hace posible y el esqueleto del módulo, y **no incluye ninguna superficie de usuario ni ninguna llamada a un proveedor de LLM**. El orden es deliberado: si el sustrato no cierra, el asistente no se escribe.

Hoy el sistema no tiene nada de esa capa. Un barrido sobre los 19 archivos `.sql` de `database/` buscando `ROW LEVEL SECURITY|CREATE POLICY|GRANT |REVOKE |CREATE EXTENSION` devuelve **un solo hit**, y es `CREATE EXTENSION IF NOT EXISTS btree_gist`. Cero policies, cero grants, cero roles acotados.

## What Changes

- Nuevo rol de PostgreSQL de solo lectura por ambiente, `asistente_ro_<ambiente>`, sin ningún `GRANT` de mutación, creado en el provisioning (`infra/scripts/provision-db.sh`) y destruido con la base.
- Segundo rol `asistente_ro_pii_<ambiente>`, idéntico salvo por el acceso a las columnas personales de `identity.personas`.
- **Manifiesto de privilegios versionado**, deny by default: toda tabla de los schemas expuestos figura como `concedida` (con la lista explícita de columnas) o `denegada-explicita` (con motivo). Ningún `GRANT ... ON ALL TABLES`.
- Test de manifiesto que falla en **tres** direcciones: privilegio efectivo que no está en el manifiesto · privilegio declarado en el manifiesto que ya no existe · tabla presente en un schema expuesto que no está clasificada.
- `GRANT USAGE` sobre los schemas, `GRANT SELECT` columna por columna y `CREATE EXTENSION unaccent` en migraciones del módulo, no en el provisioning: en el paso 1 de `spin-up.sh` la base está vacía y un `GRANT ... ON ALL TABLES` **otorga cero y no falla**.
- Nuevo permiso persistido `asistente.consultar` en `identity.permisos`, con siembra explícita para los siete roles de sistema y su entrada en `Permisos.Todos`.
- Cuatro funciones `SECURITY DEFINER` en el schema `identity` y **policies RLS** sobre `designaciones.pedidos`, `designaciones.designaciones`, `designaciones.pedido_historial` y `designaciones.pedido_adjuntos`, con el permiso de dominio **conjuntado dentro del predicado**.
- Nuevos proyectos `Modules.Asistente` y `Modules.Asistente.Contracts`, registrados por el Host con `AddAsistenteModule()`, exponiendo `GET /api/asistente/ping` con `[AllowAnonymous]`.
- Tipos envoltorio distintos por cadena de conexión (`CadenaDuena`, `CadenaSoloLectura`, `CadenaSoloLecturaPii`) para que pedir la equivocada no compile.
- Nuevo invariante de arquitectura (#14) en `CLAUDE.md` y `docs/architecture/`, con filas en el registro de edges y los diagramas actualizados.

## Capabilities

### New Capabilities

- `asistente-rol-solo-lectura`: dos roles de PostgreSQL por ambiente sin privilegios de mutación, con `GRANT SELECT` enumerado columna por columna, y tipos envoltorio que impiden confundir cadenas de conexión.
- `asistente-manifiesto-privilegios`: manifiesto versionado deny-by-default de todo privilegio del asistente, con un test que lo verifica contra los privilegios efectivos en las tres direcciones.
- `asistente-permiso-consulta`: permiso `asistente.consultar` persistido, sembrado explícitamente para los siete roles de sistema, administrable desde `/membresia-roles` sin migración.
- `asistente-alcance-por-actor`: funciones `SECURITY DEFINER` y policies RLS que acotan cada consulta al alcance del actor **y** exigen su permiso de dominio, leído en vivo.
- `asistente-modulo-base`: módulo `Modules.Asistente` registrado por el Host, con su endpoint `ping` y los tests de arquitectura que verifican sus fronteras.

### Modified Capabilities

- `persistencia-identity`: se agrega un permiso al catálogo cerrado y cuatro funciones `SECURITY DEFINER` al schema `identity`.
- `persistencia-designaciones`: cuatro tablas pasan a tener Row Level Security habilitada, con policies dirigidas exclusivamente a los roles del asistente.
- `plataforma-ambientes-efimeros`: el provisioning crea y destruye dos roles adicionales por ambiente.

## Impact

- `infra/scripts/provision-db.sh` y `infra/scripts/drop-db.sh` — alta y baja de los dos roles por ambiente.
- `database/identity/011_identity_permiso_asistente.sql` — permiso nuevo y su siembra.
- `database/identity/012_identity_funciones_asistente.sql` — las cuatro funciones `SECURITY DEFINER`.
- `database/designaciones/009_designaciones_rls_asistente.sql` — `ENABLE ROW LEVEL SECURITY` y las cuatro policies.
- `database/asistente/001_asistente_grants.sql` — `GRANT USAGE`, `GRANT SELECT` por columna y `CREATE EXTENSION unaccent`.
- `backend/src/ArsDocendi.Shared/Auth/Permisos.cs` — nueva constante y su entrada en `Permisos.Todos`.
- `backend/src/Modules.Asistente/` y `backend/src/Modules.Asistente.Contracts/` — proyectos nuevos.
- `backend/src/ArsDocendi.Host/Program.cs` — `AddAsistenteModule()`.
- `backend/tests/ArsDocendi.IntegrationTests/` — tests de manifiesto, de alcance por actor y de arquitectura del módulo nuevo.
- `CLAUDE.md`, `docs/architecture/dependency-graph.md`, `docs/architecture/data-model.md`, `docs/architecture/diagrams/` — invariante #14, edges y diagramas.
- **Grafo de dependencias**: se agrega el nodo `Modules.Asistente`, que en este cambio depende únicamente de `ArsDocendi.Shared`. No se agregan edges hacia otros módulos todavía: el carril que consume `Modules.<X>.Contracts` llega en un cambio posterior. No se introducen ciclos.

## Rollback

El cambio es aditivo y reversible por partes:

- Las policies RLS se revierten con `DISABLE ROW LEVEL SECURITY` y `DROP POLICY`. Mientras existen, **no afectan al backend**: son `FOR SELECT ... TO asistente_ro*` y la aplicación conecta como el rol dueño, que no está sujeto a ellas (no se usa `FORCE ROW LEVEL SECURITY`).
- Los roles se eliminan con `DROP OWNED BY` + `DROP ROLE` desde el provisioning.
- El permiso se revoca quitando sus filas de `identity.rol_permisos`; el catálogo puede conservar el código sin efecto.
- `Modules.Asistente` se desregistra quitando `AddAsistenteModule()` del Host: ningún otro módulo lo referencia.
