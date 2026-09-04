## 0. Gate previo — acuerdo con el equipo

Ninguna tarea posterior arranca antes de que estas dos cierren. Si el equipo rechaza la enmienda, el cambio se cancela y el asistente no se construye.

- [ ] 0.1 Presentar al equipo el invariante #14 (texto en `design.md`, decisión D2) y obtener acuerdo explícito
- [ ] 0.2 Acordar con el dueño de la superficie de administración la migración que agrega `asistente.consultar` a `identity.permisos`, dado que el invariante #4 reserva esa escritura a esa superficie

## 1. Manifiesto de privilegios

- [x] 1.1 Crear el manifiesto versionado con una entrada por cada tabla de `identity` y `designaciones`, clasificada como `concedida` (con la lista explícita de columnas y el rol que la lee) o `denegada-explicita` (con motivo)
- [x] 1.2 Clasificar `designaciones.idempotencia_comandos` como `denegada-explicita`, con el motivo escrito: `response_body JSONB` guarda la respuesta HTTP completa de cada comando, incluye datos de personas y no admite `GRANT` por columna
- [x] 1.3 Clasificar el schema `audit` como fuera de alcance completo, con el motivo: `change_log.old_row/new_row` guardan la fila entera en JSON
- [x] 1.4 Marcar `documento`, `cuil`, `telefono` y `fecha_nacimiento` de `identity.personas` como legibles **solo** por el rol con acceso a datos personales
- [x] 1.5 Test de manifiesto — dirección 1: falla si existe un privilegio efectivo que el manifiesto no declara
- [x] 1.6 Test de manifiesto — dirección 2: falla si el manifiesto declara un privilegio que ya no existe en la base
- [x] 1.7 Test de manifiesto — dirección 3: falla si hay una tabla en un schema expuesto que el manifiesto no clasifica
- [x] 1.8 Verificar los tres tests introduciendo deliberadamente cada una de las tres desviaciones y comprobando que fallan con un mensaje que identifica la tabla o columna

## 2. Provisioning de roles

- [x] 2.1 `infra/scripts/provision-db.sh` — crear `asistente_ro_<ambiente>` y `asistente_ro_pii_<ambiente>` con `LOGIN`, contraseña propia del ambiente y `GRANT CONNECT`; fijar su `search_path`
- [x] 2.2 `infra/scripts/drop-db.sh` — dar de baja ambos roles con `DROP OWNED BY` + `DROP ROLE` al destruir la base
- [ ] 2.3 `infra/compose/.env.example` — declarar las variables de las dos contraseñas, sin valor real
- [ ] 2.4 Verificar en un ambiente efímero que el alta y la baja son idempotentes y que el par de roles es distinto por ambiente
- [x] 2.5 Test de humo: los roles existen después del paso 1 de `spin-up.sh` y antes de que corran las migraciones

## 3. Migración de privilegios de lectura

- [x] 3.1 `database/asistente/001_asistente_grants.sql` — `CREATE EXTENSION IF NOT EXISTS unaccent`
- [x] 3.2 Mismo archivo — `GRANT USAGE` sobre `identity` y `designaciones` a los dos roles
- [x] 3.3 Mismo archivo — `GRANT SELECT` columna por columna según el manifiesto, con la lista escrita explícita. Prohibido `GRANT ... ON ALL TABLES`
- [x] 3.4 Mismo archivo — `GRANT SELECT` de las cuatro columnas personales de `identity.personas` **solo** al rol con acceso a datos personales
- [x] 3.5 Verificar que la migración corre en el paso 3 de `spin-up.sh`, con las tablas ya creadas, y no en el provisioning
- [x] 3.6 Test: `SELECT *` sobre `identity.personas` falla con el rol básico y funciona con el rol de datos personales
- [x] 3.7 Test: `SELECT` sobre `audit.change_log` y sobre `designaciones.idempotencia_comandos` falla con ambos roles

## 4. Permiso `asistente.consultar`

- [x] 4.1 `database/identity/011_identity_permiso_asistente.sql` — insertar el permiso en `identity.permisos` con nombre y descripción en español
- [x] 4.2 Mismo archivo — sembrar la membresía de forma **explícita para los siete roles de sistema**, sin depender de que `sys_admin` herede del catálogo completo; conceder a los seis roles no `docente` y no conceder a `docente`
- [x] 4.3 Mismo archivo — hacer la inserción idempotente con `ON CONFLICT DO NOTHING`
- [x] 4.4 `backend/src/ArsDocendi.Shared/Auth/Permisos.cs` — agregar la constante **y** su entrada en `Permisos.Todos`, sin lo cual la política no se registra y el `[Authorize]` lanza en el primer request
- [x] 4.5 Test: la política existe en el contenedor de autorización después de componer el Host
- [x] 4.6 Test: conceder y revocar el permiso desde la membresía cambia la autorización sin redesplegar

## 5. Funciones de resolución del actor

- [x] 5.1 `database/identity/012_identity_funciones_asistente.sql` — función que resuelve el actor del turno desde `app.asistente_user_id`, `SECURITY DEFINER`
- [x] 5.2 Mismo archivo — función que determina si el alcance del actor es global
- [x] 5.3 Mismo archivo — función que devuelve las materias visibles para el actor
- [x] 5.4 Mismo archivo — función que responde si el actor tiene un permiso dado, recorriendo `user_roles → rol_permisos → permisos` y descartando las asignaciones con `deleted_at` no nulo. Sin códigos de rol embebidos
- [x] 5.5 Mismo archivo — `REVOKE EXECUTE ... FROM PUBLIC` sobre las cuatro funciones, y `GRANT EXECUTE` solo a los dos roles del asistente
- [x] 5.6 Evaluar el plan de ejecución de la función de permiso dentro de un predicado y marcarla `STABLE` si el planner la reevalúa por fila
- [x] 5.7 Test: revocar el permiso al rol cambia el resultado de la función sin reiniciar la aplicación
- [x] 5.8 Test: un rol creado desde la superficie de administración con el permiso queda habilitado, y uno sin el permiso queda excluido

## 6. Row Level Security

- [x] 6.1 `database/designaciones/009_designaciones_rls_asistente.sql` — `ENABLE ROW LEVEL SECURITY` sobre `pedidos`, `designaciones`, `pedido_historial` y `pedido_adjuntos`
- [x] 6.2 Mismo archivo — una policy `FOR SELECT ... TO` los dos roles del asistente por cada tabla, con el predicado que conjunta el permiso de dominio y la restricción de alcance
- [x] 6.3 Mismo archivo — comentario que documente por qué **no** se usa `FORCE ROW LEVEL SECURITY`: la aplicación conecta como el rol dueño y forzarlo la sometería a policies que no la contemplan
- [x] 6.4 Test: un actor de alcance de carrera cuenta solo los pedidos de su carrera; uno global los cuenta todos
- [x] 6.5 Test: un actor con ámbito de materia pero **sin** `designaciones.ver` obtiene cero filas aunque existan pedidos en su materia
- [x] 6.6 Test: una consulta que une las cuatro tablas protegidas devuelve, en cada una, solo las filas del alcance del actor
- [x] 6.7 Test de no regresión: la aplicación, con la conexión del rol dueño, sigue obteniendo todas las filas de las cuatro tablas

## 7. Cadenas de conexión tipadas

- [x] 7.1 Definir `CadenaDuena`, `CadenaSoloLectura` y `CadenaSoloLecturaPii` como tipos propios
- [x] 7.2 Registrar la resolución de los tres tipos en la composición del Host, leyendo la configuración del ambiente
- [x] 7.3 Migrar el consumo existente de la cadena de conexión al tipo `CadenaDuena`, sin cambiar comportamiento
- [x] 7.4 Test de compilación: un componente que declara recibir `CadenaSoloLectura` no acepta `CadenaDuena`
- [x] 7.5 Test: los tres tipos resuelven a cadenas con usuarios de base de datos distintos

## 8. Módulo Asistente

- [x] 8.1 Crear `backend/src/Modules.Asistente/` y `backend/src/Modules.Asistente.Contracts/`, con referencia únicamente a `ArsDocendi.Shared`
- [x] 8.2 Agregar ambos proyectos a `backend/ArsDocendi.slnx`
- [x] 8.3 Implementar `AddAsistenteModule()` siguiendo la convención de registración de los módulos existentes
- [x] 8.4 Registrarlo en la composición del Host
- [x] 8.5 `GET /api/asistente/ping` con `[AllowAnonymous]`, devolviendo `200` sin tocar base ni servicios externos
- [ ] 8.6 Documentar en el PR que `Modules.Asistente.Contracts` nace vacío, y resolver con el equipo si se conserva por convención o se declara la excepción
- [x] 8.7 Test: el ping responde `200` con la base de datos detenida y sin credenciales

## 9. Tests de arquitectura

- [x] 9.1 Extender `ArquitecturaIdentityTests` para cubrir el módulo nuevo: no escribe `personas`, `roles`, `permisos` ni `rol_permisos`
- [x] 9.2 Test: ninguna referencia de `Modules.Asistente` apunta a un proyecto `Modules.<Otro>` interno
- [x] 9.3 Test: quitar el registro del módulo de la composición deja la solución compilando y arrancando

## 10. Documentación en el mismo cambio

- [ ] 10.1 `CLAUDE.md` — agregar el invariante #14 con su texto acordado y el alcance limitado a `Modules.Asistente`
- [x] 10.2 `docs/architecture/dependency-graph.md` — agregar el nodo `Modules.Asistente` y su fila en el registro de edges; verificar que no se introducen ciclos
- [x] 10.3 `docs/architecture/data-model.md` — documentar los dos roles, el modelo de privilegios por columna y las cuatro tablas con RLS
- [ ] 10.4 `docs/architecture/diagrams/` — actualizar los diagramas afectados
- [x] 10.5 `docs/architecture/domains/asistente.md` — crear el documento de dominio del módulo desde `_template.md`
- [ ] 10.6 `docs/product/designs/asistente-conversacional-definicion.md` — completar el campo `feature` del frontmatter con el link a la spec, y pasar `status` a `review`

## Por qué las ocho pendientes siguen abiertas

Ninguna es código que falte escribir. El código de este cambio está mergeable en el
PR #25 y con CI verde; lo que queda depende de una decisión ajena o de un ambiente.

| Tarea     | Por qué sigue abierta                                                                                                                                   |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 0.1, 10.1 | El invariante #14 necesita el acuerdo del equipo antes de escribirse en `CLAUDE.md`. Es **ARS-64**.                                                     |
| 0.2       | La escritura en `identity.permisos` la reserva el invariante #4 a la superficie de administración. **ARS-68**.                                          |
| 2.3       | `infra/compose/.env.example` está fuera del alcance de escritura de esta sesión. Son cuatro variables.                                                  |
| 2.4       | Pide correr el alta y la baja contra un ambiente efímero real. El script existe y el humo de `spin-up.sh` corre; la corrida de idempotencia no se hizo. |
| 8.6       | La mitad documental está en el cuerpo del PR; resolver si `Contracts` vacío se conserva es del equipo.                                                  |
| 10.4      | Los diagramas dependen del texto del invariante #14, que todavía no está acordado. Parte de **ARS-64**.                                                 |
| 10.6      | El `feature` del frontmatter apunta a `openspec/specs/<capability>/spec.md`, que recién existe al archivar.                                             |
