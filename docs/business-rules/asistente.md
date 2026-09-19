# Business rules: `asistente`

## Contexto

- **Módulo / superficie:** `backend/src/Modules.Asistente/`, `database/asistente/`.
- **Owner / stakeholders:** Secretaría Académica del Departamento (define quién consulta qué); el equipo de desarrollo (sostiene la frontera).
- **Change/Spec OpenSpec relacionado:** `openspec/changes/asistente-fundaciones/` y `openspec/changes/asistente-lee-portal/` (por crear, ARS-100).
- **Normativa de referencia:** ninguna directa. Estas reglas son de **arquitectura de seguridad**, y existen porque el asistente lee datos de otros bounded contexts sin pasar por sus `Contracts`.

> **Alcance.** Acá va lo que gobierna al asistente **como lector de schemas ajenos**. Lo que gobierna qué puede consultar de un dominio concreto vive en el `business-rules` de ese dominio: ver `portal.md` para el perfil docente.

## Reglas

### BR-`asistente`-001 Minimización en la exposición de schemas

- **Statement:** Al asistente se le conceden columnas, nunca tablas completas ni schemas completos. Toda columna que existe y no se concede lleva su motivo escrito en `manifiesto-privilegios.json`.
- **Rationale:** Es la regla que ya se practicaba y no estaba escrita. Un `GRANT ... ON ALL TABLES IN SCHEMA` no falla ni avisa: entrega cada tabla nueva por default, y alcanza con que alguien agregue una columna personal para que el asistente la lea sin que nadie lo haya decidido. La lista explícita obliga a pasar por el manifiesto, y el test falla si aparece algo sin clasificar.
- **Provenance:** `inferred_from_code`
- **Fuente normativa:** No es normativa. Es el correlato documental del invariante #14 de `CLAUDE.md`.
- **Ejemplos:** `identity.personas.documento` se concede sólo al rol PII, con motivo. `identity.users.azure_oid` no se concede a ninguno, con motivo. `experiencias.descripcion` no se concede, con motivo.
- **Roles afectados:** quien escribe una migración que toque `database/asistente/`.
- **Mapping a test:** `ManifiestoPrivilegiosTests`, que compara el manifiesto contra los privilegios reales en **cuatro** direcciones: privilegio efectivo no declarado, declaración sin privilegio, tabla de un schema expuesto sin clasificar, y schema de la base que el manifiesto no clasifica.

### BR-`asistente`-002 Lo que no se puede mostrar no se concede

- **Statement:** Cuando una columna no puede exponerse al modelo, se **deniega en el `GRANT`** en lugar de clasificarse como sensible y enmascararse.
- **Rationale:** El enmascarador identifica la columna por `(OID, attnum)`, y toda **expresión** —un `upper(columna)`, una concatenación— reporta OID 0, que se trata como pública. Es decir: la máscara protege la columna, no el valor. Para texto libre autodeclarado eso no alcanza, y la única frontera que el motor impone de verdad es no conceder.
- **Provenance:** `inferred_from_code`
- **Fuente normativa:** No es normativa.
- **Ejemplos:** `portal.experiencias.descripcion` no se concede. `identity.personas.documento` sí se concede al rol PII y se enmascara, porque es un valor corto que no se compone en expresiones útiles.
- **Roles afectados:** quien clasifica una columna nueva.
- **Consecuencia asumida:** la clasificación `sensible-texto` sigue existiendo y sirve para columnas ya concedidas, pero **no es la primera línea**: primero se decide si se concede.

### BR-`asistente`-003 El ámbito y el permiso son ejes independientes

- **Statement:** Un resultado vacío sólo puede presentarse como «no hay datos» cuando el actor alcanza **todo lo que la consulta tocó**: ámbito y permiso de dominio a la vez. En cualquier otro caso el texto tiene que reconocer el límite de alcance.
- **Rationale:** La policy de RLS conjuga permiso Y ámbito, así que un actor de ámbito global sin el permiso ve cero filas exactamente igual que uno cuyo literal no matcheó. Usar «es global» como sinónimo de «ve todo» hace que el sistema afirme «no encontré ningún registro» cuando la verdad es «no alcanzás a verlos» — una afirmación falsa dicha con seguridad.
- **Provenance:** `inferred_from_code`
- **Fuente normativa:** No es normativa: deriva de la métrica primaria, corrección con abstención.
- **Ejemplos:** Secretaría (global) sin `designaciones.ver` pregunta por pedidos → «no encontré nada dentro de lo que podés consultar», no «no hay pedidos».
- **Roles afectados:** todos.
- **Mapping a test:** `PerfilDelActorTests.Un_actor_global_sin_el_permiso_de_dominio_NO_alcanza_todo`.

### BR-`asistente`-004 No se anuncia lo que el actor no puede ejercer

- **Statement:** El catálogo de capacidades describe lo que **este actor** puede hacer, derivado de sus permisos en vivo, y nunca lo que el sistema sabría hacer si tuviera otro permiso.
- **Rationale:** Anunciar una capacidad que el actor no puede ejercer es el mismo defecto que un botón que no anda, sin botón. El invariante #7 lo prohíbe igual.
- **Provenance:** `from_spec`
- **Fuente normativa:** No es normativa: es el invariante #7 de `CLAUDE.md` aplicado a texto en vez de a controles.
- **Ejemplos:** Un actor sin `portal.ver_trayectoria_ajena` recibe «también podés consultar tu propio perfil profesional», no «podés buscar docentes por habilidad».
- **Roles afectados:** todos.
- **Mapping a test:** `CapacidadesTests.Sin_el_permiso_la_presentacion_ofrece_el_perfil_propio_y_no_la_busqueda`.
