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

### BR-`asistente`-005 Retención de 180 días del historial propio, sin opt-out

- **Statement:** Toda conversación con el asistente se persiste a `asistente.hilo_historico`/`turno_historico` — pregunta, SQL resuelta, estado y momentos, nunca filas ni el texto redactado —, sin excepción por conversación salvo el estado `Fallo` (que nunca produce un turno visible). Se retiene 180 días desde la última actividad de la conversación, y se purga automáticamente al vencer. **Archivar una conversación (asistente-rediseno-v3, design.md D3) no cambia esta regla**: es un timestamp nulable que no toca `ultima_actividad`, así que una conversación archivada sigue el mismo reloj de 180 días y se purga igual que cualquier otra. **Un borrado (design.md D4) es diferido y reversible sólo dentro de su ventana** (`Asistente__VentanaDeDeshacerSegundos`, default 15 s): mientras dura, la conversación es invisible para su dueño pero la fila sigue existiendo y «Deshacer» la restaura íntegra; vencida la ventana, el borrado es **final** — el `BackgroundService` de barrido (o la purga diaria, como red) la elimina físicamente, en cascada con sus turnos, sin exigir que el cliente siga abierto ni que nadie lo confirme de nuevo.
- **Rationale:** El sistema tiene que poder responder «el usuario X preguntó Y», a través del historial propio del usuario y del acceso de soporte auditado (BR-006); un modo que se pudiera saltear por conversación contradiría eso directamente. 180 días —más que los 90 de los registros anónimos, y con proporcionalidad frente a la Ley 25.326 (que no fija un número, así que el número lo justifica la finalidad: seguir sirviendo al propio uso continuado del usuario)— balancea seguir sirviendo a un usuario que retoma una conversación de hace meses contra no guardar indefinidamente. Archivar es una preferencia de organización de la propia bandeja, no una señal de que la conversación importe menos, así que no le acorta ni le alarga la retención. El borrado diferido existe para que «Eliminar» sea deshacible sin convertirse en una papelera permanente: la ventana es corta y su vencimiento es un límite duro, no una sugerencia, porque una recuperación posible indefinidamente sería, en los hechos, ausencia de borrado.
- **Provenance:** `from_spec`
- **Fuente normativa:** Ley 25.326, principio de proporcionalidad/finalidad (sin plazo numérico fijado por la norma).
- **Ejemplos:** Una conversación creada hace 300 días pero con actividad hace 10 sobrevive la purga; una sin actividad en 200 días se purga aunque se haya creado ayer (el corte es por `ultima_actividad`, no por `creado_en`). Una conversación archivada hace 200 días sin actividad se purga igual que una sin archivar. Un `DELETE` deshecho a los 8 s restaura la conversación sin cambios; el mismo `DELETE` sin deshacer a los 20 s ya no admite `POST .../deshacer` (`404`) y desaparece de la base dentro del minuto siguiente.
- **Roles afectados:** todos los que usan el asistente.
- **Mapping a test:** `HistorialYAuditoriaPurgaTests` (retención por última actividad, cascada, idempotencia, y `Una_conversacion_archivada_sigue_la_misma_retencion_y_se_purga`); `EscrituraDelHistorialTests` (los cuatro estados visibles se persisten, `Fallo` nunca); `PrivilegiosLecturaTests.El_historial_de_conversaciones_es_inalcanzable_por_ninguno_de_los_dos_roles`; `HistorialControllerTests.Deshacer_fuera_de_la_ventana_da_404_y_la_conversacion_sigue_pendiente` y `HistorialControllerTests.Un_borrado_fuera_de_la_ventana_se_purga_por_el_barrido_y_desaparece_de_verdad` (finalidad del borrado tras la ventana).

### BR-`asistente`-006 Acceso de soporte al historial ajeno: permiso propio, razón obligatoria, auditoría permanente

- **Statement:** Leer el historial de otro usuario exige `asistente.leer_historial_ajeno` — sembrado a **ningún** rol por default, ni siquiera `sys_admin`, y distinto de `asistente.consultar` — y una razón no vacía en cada pedido. Cada lectura escribe, antes de devolver cualquier dato, una fila permanente y no editable en `asistente.auditoria_acceso_historial` (lector, sujeto, conversación si aplica, cuándo, por qué), retenida 365 días en una ventana independiente de la del historial que describe. El sujeto nunca es informado de que su historial fue leído. El acceso de soporte nunca expone filas de resultado ni ofrece re-ejecutar la consulta del sujeto.
- **Rationale:** Es la contraparte auditada de BR-005: "quién preguntó qué" tiene que ser respondible por soporte para investigar un incidente, pero de forma narrow, siempre justificada y siempre trazable — nunca como una segunda superficie de consulta libre sobre datos de otra persona. La razón viaja en el cuerpo del `POST` (nunca en la URL) para que no termine en un log de acceso. La ausencia de FK en la fila de auditoría hacia la conversación es deliberada: el registro tiene que sobrevivir a que el propio dueño borre esa conversación. No informar al sujeto es una decisión final del cliente (no una pendiente), en línea con el mismo trade-off de ChatGPT Enterprise Compliance API, Claude Enterprise audit logs y Microsoft Purview eDiscovery.
- **Provenance:** `from_spec`
- **Fuente normativa:** No es normativa directa; es política institucional de soporte, análoga en estructura a BR-`asistente`-001/002 (minimización) aplicada a un canal de soporte en lugar de al esquema.
- **Ejemplos:** Un administrativo con `asistente.consultar` pero sin `asistente.leer_historial_ajeno` no puede leer el historial de otro usuario. Un pedido sin razón, o con razón en blanco, se rechaza y no escribe nada. Un lector con el permiso ve la SQL de la conversación ajena sin necesitar además `asistente.ver_consulta` (D9: un solo gate para el lector de soporte).
- **Roles afectados:** ninguno por default; quien reciba el permiso desde `/membresia-roles`.
- **Mapping a test:** `SoporteHistorialControllerTests` (permiso, razón obligatoria, auditoría antes de leer, fallo de auditoría bloquea la lectura, sin re-ejecución, sin exposición al sujeto); `PermisoLeerHistorialAjenoTests` (siembra a ningún rol); `PrivilegiosLecturaTests.La_auditoria_de_acceso_de_soporte_es_inalcanzable_por_ninguno_de_los_dos_roles`.

### BR-`asistente`-007 Cupo diario persistente por actor, con default por rol y override por usuario, cobrado en todo resultado que invocó al modelo

- **Statement:** El cupo diario de turnos de un actor se persiste en `asistente.presupuesto_rol` (default por código de rol) y `asistente.presupuesto_usuario` (override por actor, siempre gana sobre el default de su rol, más chico o más grande). El día se corta por calendario UTC. `0` desactiva el cupo (del rol o del override). Con varios roles vigentes sin override, el cupo efectivo es el mínimo entre los cupos ACTIVADOS (mayor que cero) de esos roles — un rol sin tope no arrastra a los demás a cero. Un turno se cobra contra el cupo si y solo si invocó al modelo al menos una vez, sin importar en qué estado terminó (`Respondida`, `NoContestable`, `NecesitaAclaracion`, `ServicioDegradado` o `Fallo`); un turno bloqueado antes de llegar al modelo (por este mismo cupo, por el tope organizacional, por un turno concurrente o por mantenimiento) nunca se cobra.
- **Rationale:** Reemplaza al cupo en memoria (`CuotaEnMemoria`, TD-011), que se perdía en cada redespliegue y no distinguía roles ni actores puntuales. Turnos/día es lo que un administrador no técnico puede fijar sin conocer el fan-out interno del pipeline (reescritor + generación + reintento + redacción), y un presupuesto persistido necesita un límite de período estable y auditable — una ventana deslizante recalculada contra `now()` en cada chequeo es un mal ajuste para un valor que un admin edita y espera que "resetee a la medianoche". Cobrar solo cuando el turno llegó a gastar algo preserva el invariante ya existente ("un turno resuelto sin modelo no gasta cupo") mientras cambia la unidad.
- **Provenance:** `from_spec`
- **Fuente normativa:** No es normativa institucional; es un control operativo de costo y equidad de uso, análogo en estructura a BR-`asistente`-005/006 (persistencia + retención) aplicado al presupuesto en lugar de al historial.
- **Ejemplos:** Un actor con cupo de rol 10 y sin override consume hasta 10 turnos que invocaron al modelo por día calendario UTC, y a las 00:00 UTC del día siguiente vuelve a tener los 10. Un actor con override en 3 queda en 3 aunque su rol tenga 10, y viceversa. Un actor con dos roles, uno con cupo 5 y otro en 0 (sin tope), queda en 5 — el rol sin tope no lo hace ilimitado. Un saludo o un menú de aclaración resuelto sin proveedor no descuenta cupo.
- **Roles afectados:** todos los que usan el asistente; los siete roles de sistema se siembran en cupo `0` (desactivado) hasta que el Departamento confirme los números reales.
- **Mapping a test:** `CuotaPersistenteTests` (bloqueo al límite, reset por día calendario, precedencia del override en los dos sentidos, mínimo entre roles activados, cero desactiva sin necesitar fila); `DegradacionDelTurnoTests`/`TurnoExclusivoDelActorTests` (cobro exactamente una vez por turno que invocó al modelo, incluido el camino de falla).

### BR-`asistente`-008 Tope organizacional de gasto mensual, bloquea a todos los actores por igual

- **Statement:** El gasto mensual estimado de toda la organización, en USD, se acumula incrementalmente en `asistente.consumo_organizacional_mensual` (por año/mes) cada vez que un turno invoca al modelo, con el precio vigente **en ese momento** contra `asistente.tabla_de_precios` (versionada por rango de vigencia). Al alcanzar `asistente.tope_organizacional` (`0` desactiva), todo turno nuevo que necesite el modelo se bloquea con `ServicioDegradado`, **sin importar el cupo individual** del actor que lo pide. El texto que ve el usuario nunca menciona el costo ni el tope de la organización. Una fila cuyo proveedor/modelo no tiene ningún precio vigente no acumula nada — nunca se costea en cero.
- **Rationale:** Es un control distinto del cupo por actor (BR-007) y no una variante: el cupo por actor responde "¿esta persona usa el asistente con equidad respecto de sus pares?" (turnos, una unidad legible); el tope organizacional responde "¿estamos a punto de recibir una factura inesperada?" (USD, donde el driver real es tokens × precio y no la cantidad de turnos — dos proveedores o dos modelos a distinta verbosidad harían de "turnos" una proxy pobre). Bloquear a todos por igual, incluso a quien todavía tiene cupo propio, es la única forma en que el tope cumple su función de última línea contra el gasto. No exponer el número al usuario evita que un límite operativo se convierta en información de negocio filtrada a cualquiera con admisión al asistente.
- **Provenance:** `from_spec`
- **Fuente normativa:** No es normativa institucional; es control operativo de costo, mismo criterio que BR-007.
- **Ejemplos:** Con el tope en USD 100 y un acumulado de USD 100.01, cualquier actor que pida algo que necesite el modelo recibe `ServicioDegradado` con un texto sin números, aunque su propio cupo diario esté intacto. Un turno con un proveedor sin fila en `tabla_de_precios` no mueve el acumulado. El acumulado de un mes nuevo arranca en cero sin ninguna acción de administración, porque un mes sin fila vale cero.
- **Roles afectados:** todos los que usan el asistente; el tope se siembra en `0` (desactivado) hasta que el Departamento confirme el número real.
- **Mapping a test:** `PresupuestoOrganizacionalTests` (bloqueo al alcanzar el tope, reset al mes siguiente, fila sin precio no acumula, un actor bajo su propio cupo queda bloqueado igual, texto sin números); `CalculadoraDeCostoTests` (precio vigente al momento en que la fila ocurrió, fila sin precio nunca en cero).
