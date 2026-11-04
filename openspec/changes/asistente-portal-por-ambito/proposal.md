## Why

Secretaría definió el alcance que quiere para el perfil profesional, y no es el que se construyó:

- **Jefe de cátedra:** los docentes designados en sus materias.
- **Coordinador de carrera:** los docentes de su carrera.
- **Secretaría, Administración y Decanato:** todos.

Lo vigente es otra cosa. La decisión D1 de `asistente-lee-portal` hizo el predicado una **disyunción** —«es mi perfil O tengo el permiso»— y dejó el ámbito afuera, con la consecuencia escrita: _«un Coordinador con el permiso ve todo el padrón, no sólo su carrera»_. Esa consecuencia se asumió a falta de una definición del cliente. Ahora hay definición, y contradice lo asumido.

**Y hay un defecto que hoy ya afirma algo falso.** `PerfilDelActor.AlcanzaTodo` —el booleano que decide si «cero filas» puede narrarse como «no hay»— se calcula como ámbito global **y** `designaciones.ver`. No mira `portal.ver_trayectoria_ajena`. Verificado contra el padrón sintético: el actor de Decanato es global y tiene `designaciones.ver`, así que `AlcanzaTodo` da verdadero; la RLS de portal le devuelve un solo perfil, el suyo; y a «¿qué docentes saben Python?» —que tres personas declararon— el asistente responde **«no encontré ningún registro»**. Es la violación de `BR-asistente-003` sobre el dominio que esa regla no cubría.

El código ya lo había anticipado, en `ConsultorDeAlcance`: _«es UN permiso porque hoy hay UN dominio con policies. Cuando haya un segundo —portal es el candidato inmediato— esto deja de ser un booleano»_. Portal llegó y el booleano no se movió. Acotar por ámbito lo agrava, porque el alcance de portal deja de depender sólo del permiso.

## What Changes

- **La RLS de portal conjuga ámbito.** El predicado pasa a ser: es mi perfil, **o** tengo el permiso **y** la persona está dentro de mi ámbito. Los roles globales alcanzan a todos, así que para Secretaría, Administración y Decanato no cambia nada.
- **Función nueva `identity.asistente_personas_visibles()`**, con la misma forma que `asistente_materias_visibles()`: global ve a todos; el resto ve a quienes tienen **designación vigente** en una materia visible.
- **Un docente sin designación vigente no es visible para roles no globales.** Es la lectura literal de «sus profesores asignados» y una decisión tomada, no un efecto colateral: se registra porque tiene filo —un docente entre períodos desaparece de la vista de su jefe— y porque el padrón sintético lo vuelve visible (11 de 15 personas hoy).
- **`AlcanzaTodo` deja de ser un booleano del turno y pasa a depender de qué dominios tocó la consulta.** Una consulta que toca portal exige además el permiso de portal y el alcance de portal; una que toca designaciones sigue exigiendo lo suyo. Sin esto, el cambio de arriba haría que un jefe de cátedra recibiera «no hay» sobre datos que existen y no alcanza.

## Capabilities

### Modified Capabilities

- `portal-visibilidad-asistente`: el permiso deja de alcanzar solo; la persona además tiene que estar dentro del ámbito del actor.
- `asistente-alcance-por-actor`: «cero filas significa que no hay» pasa a evaluarse por dominio consultado, y no con un único booleano del turno.

## Impact

- `database/identity/`: función `asistente_personas_visibles`. `database/portal/`: las policies de las seis tablas.
- `Modules.Asistente`: `PerfilDelActor`, `ConsultorDeAlcance`, `CarrilSql`, `PoliticaDeAbstencion`.
- **Documentos que dejan de ser ciertos y hay que corregir**, no sólo ampliar: D1 de `asistente-lee-portal`, `BR-portal-002`, y el material del gate `docs/product/consulta-secretaria-portal-asistente.md`, que hoy **advierte a Secretaría** que un Coordinador vería todo el padrón. Esa advertencia deja de aplicar y la pregunta del gate cambia de forma.
- **Cassettes**: no cambia el prefijo, pero sí qué filas ve cada actor, así que las referencias del eje de capacidad hay que revisarlas.
