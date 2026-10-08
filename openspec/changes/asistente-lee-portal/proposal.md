## Why

El asistente contesta hoy sobre `identity` y `designaciones`: quién está designado, en qué materia, en qué estado quedó cada trámite. Lo que **ninguna superficie del sistema contesta** es la pregunta de perfil profesional — «¿qué docentes saben Kubernetes?», «¿quiénes tienen posgrado?», «¿a quién se le vence una certificación este año?» — y ésa es exactamente la que aparece frente a una vacante o una acreditación CONEAU.

Los datos existen: el merge de `database-schema` trajo el Portal Docente con diez tablas donde el docente carga su formación, su experiencia, sus certificaciones y sus habilidades. Pero los dieciocho endpoints del portal son todos sobre el **perfil propio**, y el filtro de `/docentes` cruza apellido, documento, materia, cargo, rol y estado: ninguna dimensión de portal. Hoy esas preguntas se contestan abriendo perfiles de a uno, y en la práctica nadie lo hace.

Ese «nadie lo hace» es el corazón del cambio y no un detalle. **El asistente no agrega datos: le saca la fricción al acceso**, y la fricción era parte de la protección. Consultar cruzado lo que se recogió para que cada uno mantenga su ficha es una finalidad distinta bajo la Ley 25.326, así que la decisión de habilitarlo no es de ingeniería.

Este cambio construye la máquina completa y **la deja apagada**: el permiso que abre el padrón nace concedido a nadie. Encenderla es una acción de administración de Secretaría, registrada, no un despliegue.

> **Sobre el orden.** Este change se escribió DESPUÉS de la implementación, lo que se aparta del invariante #5. Se registra así en vez de fingir la cronología: un documento que dice haber precedido al código cuando no lo hizo hace que el proceso deje de significar nada. El relevamiento que lo fundamenta sí fue previo —siete dimensiones más tres revisiones adversariales— y las cinco decisiones de alcance se tomaron antes de escribir SQL.

## What Changes

- **Permiso propio, `portal.ver_trayectoria_ajena`, concedido a nadie.** Prohibido reusar `portal.ver`: lo tienen los siete roles y significa «acceder al portal propio», así que un predicado que preguntara por él sería verdadero siempre.
- **RLS sobre las seis tablas** que el catálogo de preguntas necesita, con un predicado que es una **disyunción y no una conjunción con el ámbito**: es mi propio perfil, O tengo el permiso. Mirar lo propio no es un privilegio, y el permiso es la frontera.
- **`GRANT` columna por columna** sobre esas seis tablas. `portal.contactos` se deniega entera y `cvs`, `proyectos` y `proyecto_documentos` no se conceden: ninguna pregunta del catálogo las pide, y una tabla expuesta que nadie consulta es prefijo de prompt que se paga en cada llamada.
- **`COMMENT ON` de todo lo concedido**, que no es documentación sino la capa que le permite al modelo mapear lenguaje natural a tablas.
- **Declaración de cobertura**: toda respuesta apoyada en portal dice sobre cuántas personas existe el dato.
- **El ámbito deja de ser proxy de «ve todo»** en la política de abstención, porque con un permiso ortogonal al ámbito el proxy miente en las dos direcciones.
- **Catálogo de preguntas escrito**, del que se derivó el alcance de tablas hacia atrás.
- **Medición**: fixture de portal con cardinalidades declaradas, ocho ítems nuevos y una corrida financiada que regrabó el corpus de cassettes.

## Capabilities

### New Capabilities

- `portal-visibilidad-asistente`: quién puede consultar por el asistente el perfil profesional de quién, impuesto por RLS y gobernado por un permiso propio.
- `asistente-cobertura-del-dato`: ninguna respuesta apoyada en un dato autodeclarado presenta el vacío como un hecho.

### Modified Capabilities

- `asistente-alcance-por-actor`: «cero filas significa que no hay filas» pasa a exigir ámbito **y** permiso de dominio, que son ejes independientes.
- `asistente-capacidades`: la presentación anuncia lo que el actor puede ejercer, derivado de sus permisos en vivo.
- `asistente-manifiesto-privilegios`: cuarta dirección de verificación — todo schema de la base tiene que estar clasificado.

## Impact

- `database/identity/015`, `016` · `database/portal/002`, `003`, `004` · `database/asistente/001` y los dos manifiestos.
- `Modules.Asistente`: `PoliticaDeAbstencion`, `PerfilDelActor`, `ConsultorDeAlcance`, `PresentacionPorRol`, `CarrilSql`, `RedactorDeRespuesta`, y `CoberturaDelPortal` + `ConsultorDeCobertura` nuevos.
- `Modules.Portal`: cuatro migraciones EF. **No se toca su API ni su servicio**: la defensa de los endpoints sigue siendo `PersonaActualAsync` y el filtro por dueño del repositorio.
- **El frontend no se toca.** El asistente es agnóstico a qué tablas hay detrás.
- Evaluación: `GeneradorDeFixture`, `capacidad.json`, líneas de base y los 102 cassettes.
