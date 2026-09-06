# Consulta a Secretaría Académica: el asistente y el perfil docente

> **Para llevar a la reunión.** No es un documento técnico: es lo que hay que
> preguntar, con lo que hace falta saber para preguntarlo bien. La respuesta escrita
> —aunque sea un mail— es lo que da sustento normativo al acceso y lo que se cita en
> `BR-portal-001`.

## Qué se construyó, y por qué está apagado

El asistente ya puede leer el Portal Docente: formación, experiencia laboral,
certificaciones y habilidades declaradas. **Está desplegado y apagado.** Hoy cada
persona ve únicamente su propio perfil; nadie ve el de otro.

Encenderlo es conceder un permiso desde la pantalla de roles. No requiere
despliegue ni intervención técnica, y por eso la decisión es de Secretaría.

## Por qué se pregunta, si los datos ya están en el sistema

Es la parte que conviene no saltear. Los datos **ya existen y ya son accesibles**:
cualquiera con acceso al portal podría abrir los perfiles de a uno y leerlos. Nadie
lo hace, porque es impracticable.

El asistente no agrega datos nuevos. **Le saca la fricción al acceso** — lo que
llevaba horas pasa a resolverse en dos segundos, cruzado. Y esa fricción era, en los
hechos, parte de la protección.

La Ley 25.326 llama a eso un cambio de finalidad: los datos se recogieron para que
cada docente mantenga su ficha, no para consultarlos cruzados. Por eso hace falta
declarar para qué se van a usar, aunque no haya ningún dato nuevo de por medio.

## Las cuatro preguntas que esto habilita

Ninguna la contesta hoy ninguna pantalla del sistema:

1. ¿Qué docentes saben Kubernetes?
2. ¿Quiénes tienen un posgrado terminado?
3. ¿A qué docentes se les vence una certificación este año?
4. ¿Qué docentes declararon interés en dictar Bases de Datos?

El caso de uso real es **una vacante o una acreditación CONEAU**: encontrar a alguien
con el perfil adecuado sin recorrer el padrón a mano.

## Lo que hay que decir, aunque no lo pregunten

### No existe «contar sin nombrar»

El sistema no puede ofrecer estadística agregada sin identificar personas. Quien
pueda preguntar _cuántos docentes saben Python_ va a poder preguntar _quiénes son_:
las dos preguntas atraviesan exactamente el mismo camino técnico.

Si lo que el Departamento quiere es estadística anónima, **esto no es lo que se
construyó** y haría falta otra herramienta.

### El portal está casi vacío, y eso cambia lo que se puede esperar

El propio diseño del portal parte de que los docentes no cargan sus datos. Las
respuestas van a decir siempre sobre cuántos se sabe —«de 120 docentes, 14 cargaron
sus habilidades»— justamente para que nadie lea «no hay ninguno» como un hecho sobre
el plantel.

Habilitar esto probablemente sirva más el día que haya datos cargados que hoy.

### El teléfono y el mail personales quedan afuera

Decisión ya tomada, y no hace falta discutirla salvo que Secretaría quiera lo
contrario: el contacto **personal** que el docente carga en su portal no es
consultable por el asistente. El contacto **institucional** sí lo era ya y lo sigue
siendo.

El motivo: el institucional responde las mismas preguntas, y el personal se cargó en
una pantalla que no dice quién va a verlo.

### Del CV sólo se sabe que existe

El asistente puede decir que alguien cargó su CV y de cuándo es. No entrega el
archivo. Mismo criterio que ya rige para los adjuntos de los trámites.

## Las tres preguntas

### 1. ¿Se habilita, y para qué tarea concreta?

Necesitamos la finalidad escrita: _«buscar docentes con perfil adecuado ante una
vacante o una acreditación»_ es una respuesta válida. _«Para tenerlo disponible»_ no
lo es — sin una tarea concreta no hay finalidad declarada que citar.

### 2. ¿A quiénes alcanza cada rol?

Cada rol ve dentro de su ámbito, y eso ya está construido así:

| Rol                                  | Alcanza a                                            |
| ------------------------------------ | ---------------------------------------------------- |
| Jefe de Cátedra                      | los docentes con designación vigente en sus materias |
| Coordinador de Carrera               | los docentes designados en materias de su carrera    |
| Secretaría, Administración, Decanato | todo el padrón                                       |

**Lo que hay que confirmar, y tiene filo:** un docente **sin designación vigente**
no lo ve ni su jefe ni su coordinador — sólo los tres roles departamentales. Es
coherente con «sus docentes asignados»: sin designación no es de nadie. Pero
significa que **entre períodos un docente desaparece de la vista de su jefe**,
justo cuando hay que mirarlo para renovarlo.

Las dos alternativas se descartaron por el mismo motivo: conservar la última
designación aunque esté cerrada, o una ventana de gracia de N meses, hacen que el
alcance nunca se achique. Si a Secretaría el hueco le molesta, la ventana de gracia
es un cambio chico — pero hay que fijar el N y que ellos lo aprueben.

### 3. ¿Quién puede otorgar el permiso, y con qué criterio?

**Es la pregunta más importante de las tres**, y la que suele quedar afuera.

El permiso se concede desde la pantalla de roles en treinta segundos. El sistema
puede impedir que alguien _sin_ el permiso lea datos; **no puede impedir que alguien
con acceso a la administración de roles se lo conceda a quien sea**. Sólo puede
dejarlo registrado.

O sea que la política real no está en el código: está en quién tiene la potestad de
otorgarlo y bajo qué criterio. Eso es lo que hay que acordar y escribir.

## Qué pasa después de la respuesta

- **Si es que sí:** se concede el permiso a los roles acordados y queda registrado.
  No hace falta desplegar nada.
- **Si es que no:** no se toca nada. La máquina queda como está —cada uno ve su
  propio perfil— y el trabajo no se pierde: el día que se decida, es un clic.
- **En los dos casos:** hace falta confirmar con el área legal de la universidad si
  el banco de datos está inscripto ante la AAIP y quién actualiza esa declaración
  cuando cambian los destinatarios de los datos.
