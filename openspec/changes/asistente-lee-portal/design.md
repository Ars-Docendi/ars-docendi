## Context

El asistente lee schemas ajenos con dos roles de PostgreSQL y `GRANT` por columna (invariante #14). Este cambio suma un tercer schema, `portal`, y con él la primera categoría de dato **autodeclarado y regido por normativa de protección de datos personales** que el asistente toca.

Tres hechos condicionaron todo el diseño, y los tres se verificaron antes de escribir SQL:

1. **`portal.ver` no sirve como gate.** Lo tienen los siete roles y significa «acceder al portal propio».
2. **Las tablas están casi vacías.** El design spec del portal declara que «el problema del Departamento es que los docentes no cargan nada».
3. **No hay camino confiable de persona a carrera.** En el padrón sintético, 13 de 17 personas no tienen designación vigente y 8 de 17 no tienen cuenta.

## Goals / Non-Goals

**Goals:** que un actor autorizado pueda buscar docentes por perfil profesional; que ningún actor vea más de lo que le corresponde; que el vacío nunca se presente como un hecho; que la decisión de habilitar sea de Secretaría y quede registrada.

**Non-Goals:** el contacto personal · el archivo del CV · los proyectos · un nivel intermedio de acceso estadístico anónimo (no existe: ver D5) · tocar la API REST del portal · tocar el frontend · sumar entidades de portal al detector de ambigüedad.

## Decisions

### D1 — El predicado es una disyunción, y el ámbito queda afuera

**Opción elegida:** `es mi propio perfil OR tengo portal.ver_trayectoria_ajena`. No se usa `asistente_es_global()`.

**Por qué:** el patrón de designaciones conjuga permiso Y ámbito porque allá el dato ES de una materia. Portal está archivado por persona, y el ámbito no dice nada sobre un dato de persona: que alguien coordine una carrera no dice si puede leer dónde estudió un docente.

**Descartado — puente por designación vigente:** ataría la RLS de portal al schema de designaciones y dejaría invisible a todo docente entre períodos, que en el padrón sintético son 13 de 17. **Descartado — puente por rol asignado:** 8 de 17 personas no tienen cuenta.

**Consecuencia asumida y explícita:** un Coordinador con el permiso ve **todo el padrón**, no sólo su carrera. Es más de lo que un lector esperaría de «alcance de carrera», y por eso está acá y en `BR-portal-002`.

> **Enmendada por `asistente-portal-por-ambito`.** El ámbito entra al predicado: `es mi perfil OR (tengo el permiso AND la persona está en mi ámbito)`.
>
> El argumento original —«el ámbito no dice nada sobre un dato de persona»— era correcto **a falta de una definición del cliente**, y la consecuencia se asumió justamente por eso. Secretaría después definió el alcance que quiere: el jefe de cátedra ve a los designados en sus materias, el coordinador a los de su carrera, y Secretaría, Administración y Decanato a todos. Con esa definición sobre la mesa, «un Coordinador ve todo el padrón» dejó de ser una consecuencia asumible y pasó a ser una diferencia con lo pedido.
>
> Lo que **no** cambia: la primera rama sigue sin pedir nada. El ámbito acota lo ajeno, nunca lo propio.
>
> Se escribe la enmienda en vez de argumentar que el texto «ya lo permitía» —decía lo contrario, y con todas las letras—, por el mismo motivo que el invariante #14: una regla reinterpretada deja de restringir a nadie.

### D2 — El permiso nace concedido a nadie, y eso es un default y no una frontera

Conceder es una acción de `/membresia-roles`: treinta segundos, sin migración. La RLS **impone** el permiso; no decide quién lo tiene.

Por eso la regla de control real es documental —`BR-portal-002`— y no técnica. El sistema no puede impedir una concesión indebida; sólo puede dejarla registrada. Fingir lo contrario sería el peor resultado: creer que hay una barrera donde hay un default.

### D3 — Lo que no se puede mostrar se deniega, no se enmascara

El enmascarador identifica la columna por `(OID, attnum)`, y **toda expresión reporta OID 0**, que se trata como pública: un `upper(descripcion)` esquiva la máscara. Para texto libre autodeclarado la máscara no alcanza, y la única frontera que el motor impone es no conceder.

Por eso `experiencias.descripcion` no se concede, y por eso todo lo concedido de portal se clasifica `publica`: **lo peligroso se sacó, no se disfrazó**. Enmascarar el nivel de un título o el término de una habilidad haría imposible la respuesta que el catálogo promete.

### D4 — El alcance de tablas se derivó de las preguntas, no del esquema

Seis de diez. Las cuatro preguntas del catálogo se contestan con `perfiles`, `habilidades`, `docente_habilidades`, `educaciones`, `certificaciones` y `experiencias`; ninguna necesita `cvs`, `proyectos` ni `proyecto_documentos`.

El prefijo del prompt viaja en **cada llamada, para siempre**: una tabla expuesta que nadie consulta es costo permanente. Medido: 14 → 20 tablas y 103 → 136 columnas.

### D5 — Contar y enumerar no se pueden separar, y se declara

`ValidadorDeSql` es lista negra de funciones y palabras clave: no analiza la forma del `SELECT`. «¿Cuántos saben Python?» y «¿quiénes?» atraviesan el mismo validador, permiso y policy.

**No existe el recorte intermedio.** Se registra como limitación declarada (`BR-portal-006`) para que nadie prometa lo contrario en una conversación con el cliente.

### D6 — El vacío se declara, porque tiene dos causas y ninguna es «no hay»

«Ningún docente sabe Python» y «nadie cargó sus habilidades» son la misma consulta con el mismo cero. Se suma el denominador **además** del aviso de alcance, no en su lugar: los dos límites pueden ser ciertos a la vez y quien pregunta necesita los dos para saber qué hacer — pedir acceso, o pedirle a la gente que cargue.

Con cero filas el texto lo arma el código; con filas lo arma el modelo y el límite viaja como regla del prompt, que es lo único que puede restringir una narración.

### D7 — Cada policy nombra al actor, incluso las hijas

Podrían apoyarse en que la RLS del padre se aplique dentro de su propia subconsulta, que es lo que hace `designaciones/009`. Acá se escribe el predicado completo igual: una policy cuya única protección es el comportamiento de otra no se puede leer sola, y el día que alguien toque la del padre se lleva puestas cinco.

**Y toda referencia a la fila externa va calificada.** La policy de `habilidades` se escribió como `dh.habilidad_id = id` y Postgres lo resolvió contra `pf.id` de la subconsulta —`perfiles` también tiene `id`—: el predicado quedó siempre falso y nadie veía nada, ni con el permiso. No hay error ni warning; la consulta es válida y significa otra cosa. Lo agarraron los tests de comportamiento, no una revisión del SQL.

## Risks / Trade-offs

- **`portal.habilidades.usos` es un canal que la RLS no cierra.** Es un contador agregado sobre todo el padrón y una policy por fila no puede acotarlo: la fila es una sola y su número ya vio todo. Se resuelve no concediendo la columna.
- **El catálogo de capacidades se cachea por rol de conexión**, no por actor. Se compensó haciendo que la presentación dependa del permiso, leído en vivo por actor.
- **La medición se pagó tres veces.** La primera corrida reveló que el fixture no concedía el permiso, así que los ítems daban verde sin consultar el padrón; la segunda, que el `COMMENT` de `termino_norm` no decía que la normalización es a mayúsculas, lo que hacía fallar toda pregunta por habilidad en silencio. Las dos eran defectos de la instrumentación y ninguna se podía encontrar sin medir.
- **Abrir portal costó dos ítems** de designaciones —`cap-004` y `rob-011`, los dos pasando a abstención— y ganó uno. Se puede afirmar con esa precisión gracias a la línea de base congelada antes del cambio.

## Migration Plan

`identity/015` y `016` (permiso y función) · `portal/002` (FK), `003` (RLS) y `004` (comentarios) · `asistente/001` (grants, **último**: conceder antes de que la RLS exista abriría el padrón en la ventana entre un merge y el otro). El orden lo garantiza el registro de módulos en `Program.cs`.

## Open Questions

- La finalidad escrita de Secretaría (ARS-90) es precondición de conceder el permiso, no de mergear el código.
- Las citas de la Ley 25.326 en `docs/business-rules/portal.md` están **pendientes de verificación** contra el texto vigente.
- El invariante #14 fue ratificado el 2026-09-08 (tarea 0.1 de `asistente-fundaciones`).
