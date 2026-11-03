# Diseño — el portal se acota por ámbito

## Decisiones

### D1 — El predicado conjuga ámbito, y eso enmienda a D1 de `asistente-lee-portal`

El predicado vigente es una disyunción: `es mi perfil OR tengo el permiso`. Pasa a ser:

```
es mi perfil
OR (tengo portal.ver_trayectoria_ajena AND la persona está en mi ámbito)
```

**La enmienda se escribe, no se reinterpreta.** El texto de D1 dice que el ámbito «no dice nada sobre un dato de persona: que alguien coordine una carrera no dice si puede leer dónde estudió un docente». Ese argumento era correcto **a falta de una definición del cliente**, y ahora hay una: Secretaría dice que el jefe de cátedra ve a los suyos y el coordinador a los de su carrera. La regla vieja se enmienda o se cumple; estirarla no es ninguna de las dos.

**Mirar lo propio sigue sin exigir privilegio.** La primera rama no se toca: el ámbito acota lo ajeno, nunca lo propio.

### D2 — El puente es la designación vigente, y su costo se acepta con el número a la vista

D1 de `asistente-lee-portal` descartó este puente porque «dejaría invisible a todo docente entre períodos». Es cierto, y ahora es la conducta pedida: sin designación vigente, un docente no es «de» ninguna cátedra.

**El número está medido, no estimado:** en el padrón sintético 11 de 15 personas no tienen designación vigente, así que sólo los roles globales las alcanzan. En producción la proporción va a ser otra, pero el caso es real —entre períodos las designaciones caducan— y el filo también: **un jefe de cátedra deja de ver a su docente justo cuando lo necesita para renovarlo**.

Se eligió la lectura literal sobre las dos alternativas —última designación aunque esté cerrada, o un período de gracia parametrizado— porque las dos hacen que el alcance nunca se achique, y un alcance que sólo crece deja de ser un alcance. Si el filo molesta en uso real, el período de gracia es el cambio siguiente y no toca nada de esta estructura.

### D3 — `AlcanzaTodo` pasa a depender de qué tocó la consulta

Hoy es un booleano del turno: `global AND designaciones.ver`. Con dos dominios con policies eso miente en las dos direcciones, y ya miente: un actor global con `designaciones.ver` y sin el permiso de portal recibe «no encontré ningún registro» sobre datos que existen y no alcanza.

Pasa a evaluarse contra los dominios que la consulta tocó, que el carril ya sabe calcular —`CoberturaDelPortal.TablasQueToca` hace exactamente esa detección para la cobertura—.

**Se reusa esa detección en vez de escribir una segunda.** Dos detectores del mismo hecho es la forma en que una respuesta declara cobertura de portal y a la vez afirma que no hay datos: cada uno con su propia idea de qué tocó la consulta.

**Es conservador por construcción:** ante un dominio que no se reconoce, no alcanza todo. Equivocarse hacia «no alcanzás a verlo» degrada la respuesta; hacia «no hay» la vuelve falsa.

### D4 — La consulta de cobertura corre con el mismo alcance, y por eso el denominador es del actor

`ConsultorDeCobertura` cuenta sobre cuántas personas existe el dato, y corre bajo la misma RLS. Con el ámbito acotado, el denominador que ve un jefe de cátedra es el de **su cátedra**, no el del departamento.

Eso es lo correcto y conviene decirlo: «de 4 docentes, 1 cargó sus habilidades» es verdad dentro de su alcance. Un denominador departamental sería un canal de inferencia sobre gente que el actor no puede ver, que es lo mismo que `PoliticaDeAbstencion` ya prohíbe al no declarar cuántas filas quedaron afuera.

## Alternativas descartadas

**Dejar el ámbito afuera y resolverlo con permisos distintos por rol.** Un permiso «ver trayectoria de mi cátedra» y otro «de mi carrera» reproduce el ámbito en la matriz de permisos, donde no se puede verificar contra los datos: nada impediría dárselo a alguien sin cátedra. El ámbito ya vive en `identity.user_roles` y el motor ya sabe evaluarlo.

**Acotar en la aplicación en vez de en la RLS.** Rompe el invariante #14: la frontera del asistente es del motor porque es falsable. Un filtro en C# lo deja de ser el día que alguien escriba otra consulta.
