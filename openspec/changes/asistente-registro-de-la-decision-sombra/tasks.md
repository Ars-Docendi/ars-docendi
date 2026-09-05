## 1. La columna en el registro operativo

- [x] 1.1 Test rojo primero: `intencion_sombra` existe en `asistente.registro_operativo`, es `text`, admite nulo y no declara `DEFAULT`
- [x] 1.2 Agregar la columna al `CREATE TABLE` de `database/asistente/002_asistente_registros.sql` — no un `003` con `ALTER`, que el guard de arquitectura prohíbe (D4)
- [x] 1.3 `COMMENT ON COLUMN` que la distinga de `carril`: una es la ruta real y la otra la que se habría tomado
- [x] 1.4 Escribir en el archivo, junto a la columna, por qué va sin `DEFAULT`: nulo es «no capturó», y un default inventaría una decisión
- [x] 1.5 Verificar que el guard `El_DDL_del_asistente_no_borra_ni_altera_nada_ni_siquiera_lo_propio` sigue en verde

## 2. La decisión viaja del paso 5 al registro

- [x] 2.1 Test rojo primero: una pregunta capturada deja su intención en la fila del registro operativo
- [x] 2.2 Portador con alcance de turno para la decisión sombra, con la forma de `ContadorDeLlamadasDelTurno` (D6), registrado `AddScoped`
- [x] 2.3 `TurnoParaRegistrar` suma el campo de la intención sombra, documentado como «va solo al registro operativo»
- [x] 2.4 `CapaConversacional` anota la decisión del paso 5 en el portador y `RegistrarAsync` la lee
- [x] 2.5 `RegistroDelTurno` la escribe en el `INSERT` del operativo, y no toca el del analítico
- [x] 2.6 Test: una pregunta que ninguna intención cubre deja nulo y no falla
- [x] 2.7 Test: un turno que termina antes del paso 5 —un saludo, una meta-pregunta— deja nulo
- [x] 2.8 Test: un turno que se cae después de decidir conserva la intención en la fila del fallo
- [x] 2.9 Verificar en rojo: no anotar la decisión en el portador hace fallar 2.1 y 2.8

## 3. Lo que el registro no debe perder ni ganar

- [x] 3.1 Test: un turno capturado que responde por SQL registra `carril` del carril SQL y la intención en su columna
- [x] 3.2 Test: un turno capturado que termina pidiendo aclaración registra el carril de aclaración, no el determinista
- [x] 3.3 Confirmar que `CarrilDe` sigue derivando del estado del turno y no de la decisión sombra
- [x] 3.4 Test: la respuesta al usuario es la misma con y sin captura — el modo sombra no cambia ninguna respuesta
- [x] 3.5 Test: `asistente.registro_analitico` no tiene ninguna columna para la intención sombra (D2). Queda en verde desde el primer día a propósito: su valor es ponerse en rojo el día que alguien la agregue por consistencia
- [x] 3.6 Test: un turno capturado deja la fila analítica con solo pregunta, categoría, estado y día

## 4. La tabla dorada offline

- [x] 4.1 Archivo de tabla dorada versionado con una entrada por ítem de `capacidad.json` y de `robustez.json`, cada una con su intención capturada o nulo
- [x] 4.2 Test que corre el enrutador sobre los dos datasets y compara contra la tabla dorada, sin ninguna llamada al proveedor del modelo
- [x] 4.3 El mensaje de fallo nombra el ítem, la dirección del cambio (`nulo → intención` es posible laxitud; `intención → nulo` es captura perdida) y qué lectura corresponde a cada una
- [x] 4.4 Test: falla si un ítem de los datasets no tiene entrada en la tabla, y falla si la tabla tiene una entrada que ningún ítem reclama
- [x] 4.5 Verificar en rojo: cambiar a mano una entrada de la tabla dorada hace fallar nombrando ese ítem
- [x] 4.6 Verificar en rojo: sacarle el término excluido a `pedidos-de-una-novedad` hace fallar nombrando el ítem que pasa a capturarse
- [x] 4.7 Reemplazar el assert booleano `Ninguna_pregunta_del_dataset_se_captura` por la tabla dorada, que lo subsume (D5), y trasladar su guard de banco no vacío a la cobertura de 4.4
- [x] 4.8 Escribir en el test que la tabla no se regenera como efecto de correrlo: se edita a mano y el diff es lo que se revisa

## 5. Consistencia de fraseo y el número

- [x] 5.1 Test rojo primero: cada ítem de `robustez.json` y su `origen` de `capacidad.json` capturan la misma intención, o ninguno captura
- [x] 5.2 El mensaje de fallo nombra los dos ítems y las dos decisiones divergentes
- [x] 5.3 Test: un `origen` que no corresponde a ningún ítem de capacidad hace fallar nombrando el ítem y el origen
- [x] 5.4 El test deja el número a la vista: cuántos ítems del corpus captura el catálogo, sobre el total
- [x] 5.5 Escribir en el test que NO afirma que la intención capturada sea la correcta para la pregunta (D7), porque los datasets llevan `sql_referencia` y no una intención esperada

## 6. Documentación

- [x] 6.1 `docs/architecture/data-model.md` — `intencion_sombra` en la fila de `asistente.registro_operativo`, en el MISMO commit que el DDL (invariante #6)
- [x] 6.2 En el mismo renglón, completar `tokens_de_cache`, que la tabla del doc también omite
- [ ] 6.3 README de `Modules.Asistente` — la consulta ejecutable que produce la cobertura sobre tráfico real desde el registro operativo
- [ ] 6.4 README — la advertencia junto a la consulta: `carril` es la ruta real, `intencion_sombra` la que se habría tomado, y no responden la misma pregunta
- [ ] 6.5 README — dónde vive la tabla dorada, qué mide y cómo se la regenera a mano
- [ ] 6.6 README — qué pasa con la columna cuando ARS-46 se apruebe: pasa a registrar la intención que sí enrutó, no se borra ni se renombra (D3)
