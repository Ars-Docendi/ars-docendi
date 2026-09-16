## Context

Ver `proposal.md` para la motivación y `specs/pipeline-deploy-ci/spec.md` para el contrato. El estado actual tiene un único assembly de tests de integración: un fixture de PostgreSQL crea un contenedor compartido, 15 clases pertenecen a la misma colección xUnit y cada clase crea una base con nombre único, aplica migraciones y la elimina al terminar. La colección serializa esas clases aunque sus bases sean independientes.

El job Backend hace restore, build y test de toda la solución. Su filtro también incluye archivos de pnpm, pero no `database/**`. Los workflows de staging y PR usan listas positivas de paths sin las exclusiones Markdown que ya existen en producción. Los Dockerfiles ya copian manifiestos antes de restaurar y `.dockerignore` excluye documentación, tooling y artefactos; no hay una optimización evidente de contexto para agregar.

## Goals / Non-Goals

**Goals:**

- Reducir el wall-clock de la fase de tests manteniendo todos los casos, sus migraciones y el aislamiento por base.
- Evitar jobs de backend y deploy que no pueden producir un cambio ejecutable.
- Reutilizar paquetes NuGet sin convertir un cache miss en una falla del pipeline.
- Mantener gates de PR, runners efímeros, secretos, tags, reset de bases y referencias SHA existentes.

**Non-Goals:**

- No borrar tests ni crear un subconjunto rápido que reemplace la suite completa.
- No modificar API, módulos, schema de producción ni contratos entre módulos.
- No paralelizar todavía los dos builds Docker ni introducir un cache remoto de imágenes: el runner efímero de PR y el runner persistente de deploy tienen implicancias de capacidad y seguridad que requieren mediciones propias.

## Decisions

### 1. Fixture PostgreSQL a nivel de assembly

Registrar `PostgresFixture` como fixture de assembly de xUnit v3 y quitar la colección única de las clases de integración. Los constructores seguirán recibiendo el fixture compartido y `ClasePostgresAislada` seguirá creando una base con nombre único por clase. La finalización del fixture de assembly ocurrirá después de todas las clases.

Esto permite que las clases que hoy están serializadas por `ColeccionPostgres` formen colecciones independientes y avancen en paralelo, sin arrancar un contenedor por clase. Los tests dentro de una misma clase conservan su comportamiento actual; no se introduce paralelismo interno donde comparten estado.

La creación de bases usa nombres únicos y conexiones sin pooling. Por eso `EliminarBaseAsync` no debe limpiar globalmente los pools de Npgsql mientras otras clases corren; se quitará esa limpieza global y se conservará `DROP DATABASE ... WITH (FORCE)`. Si una versión del proveedor exige limpieza adicional, deberá limitarse a la cadena de la base destruida.

Alternativas descartadas:

- Eliminar o seleccionar menos tests: reduce cobertura y no ataca la serialización que explica el tiempo.
- Un contenedor por clase: aumenta arranques y migraciones, justo el coste que se busca amortizar.
- Separar inmediatamente en varios jobs: multiplica setup y recursos; se reconsiderará sólo si el paralelismo dentro del assembly no alcanza el objetivo.

### 2. Filtros explícitos por tipo de cambio

En `ci.yml`, el filtro Backend incluirá `backend/**`, `database/**` y `global.json`, con exclusiones para Markdown dentro de las áreas de código. Se quitarán `package.json`, `pnpm-workspace.yaml` y `pnpm-lock.yaml` de ese filtro; esos inputs permanecen en Frontend, que sí consume pnpm. No se cambiarán los nombres de jobs ni la lógica de `needs` para no afectar checks existentes.

En staging y PR se agregarán las exclusiones Markdown después de sus patrones positivos, igual que en producción. Se conservarán los eventos, gates y pasos de build, push, reset y spin-up para cambios desplegables. Después del cambio se revisará que los checks requeridos de branch protection no queden permanentemente pendientes cuando el workflow se omite por paths.

### 3. Cache NuGet best-effort

Usar el cache explícito de GitHub Actions sobre el directorio de paquetes NuGet, porque el repositorio no tiene `packages.lock.json` y el cache integrado de `setup-dotnet` depende de lockfiles o de un `cache-dependency-path`. La clave incluirá el sistema operativo, `global.json`, los manifiestos `*.csproj` y los lockfiles NuGet presentes; no incluirá archivos de pnpm.

El paso de restore del cache y su guardado serán tolerantes a miss o indisponibilidad del servicio. `dotnet restore` seguirá siendo la fuente de verdad y validará/redescargará paquetes faltantes. La nueva acción se fijará a un SHA completo, igual que el resto de las acciones externas.

### 4. Medición como condición de adopción

La línea base es la fase de tests Release completa: 148 tests y aproximadamente 3m29s en el entorno de referencia. Se compararán tres ejecuciones equivalentes antes y después, registrando cantidad de tests, fallos y mediana. Si el cambio de fixture no alcanza el objetivo de la spec o introduce flakiness, se revierte esa decisión antes de considerar más paralelismo.

El tiempo de restore se medirá por separado para saber si el cache NuGet aporta valor. El tiempo de build de imágenes se observará, pero el cache remoto Docker queda fuera de este change hasta contar con evidencia de misses en los runners reales y un diseño de scopes que no mezcle artefactos de PR con producción.

## Risks / Trade-offs

- **[Condiciones de carrera en tests]** → Repetir la suite completa varias veces con concurrencia; conservar una base única por clase; investigar cualquier acceso a estado estático o nombres compartidos antes de aceptar el cambio.
- **[Límite de recursos del PostgreSQL de pruebas]** → Mantener un solo contenedor, medir conexiones y limitar la concurrencia del runner sólo si el benchmark muestra saturación.
- **[`DROP DATABASE` mientras otra clase usa el contenedor]** → Usar nombres únicos, conexiones sin pooling y eliminación sólo de la cadena propia; no ejecutar `ClearAllPools` global.
- **[Cache NuGet obsoleto o indisponible]** → Clave con inputs de restore, restore normal como fallback y pasos de cache no bloqueantes.
- **[Workflow omitido deja un check pendiente]** → Verificar branch protection y, si el repositorio lo requiere, separar la decisión de paths de la ejecución para conservar un check concluyente.
- **[Corrección de filtros omite un archivo relevante]** → Probar explícitamente cambios en código, migraciones, dependencias, Markdown y frontend; mantener `global.json` como input del backend.

## Migration Plan

1. Medir y registrar la línea base sin modificar el pipeline.
2. Aplicar el fixture de assembly y ejecutar la suite completa en Release con PostgreSQL.
3. Cambiar filtros y cache NuGet; validar los escenarios de paths y que todas las acciones nuevas estén fijadas por SHA.
4. Actualizar la documentación de infraestructura y ejecutar los checks proporcionales.
5. Si falla la suite o no se alcanza la mejora, revertir primero el fixture a `ICollectionFixture` y las anotaciones de colección; los cambios de filtros y cache pueden revertirse de forma independiente.

## Open Questions

Ninguna: la decisión de no agregar cache remoto Docker deja esa optimización para una medición posterior sin cambiar este contrato.
