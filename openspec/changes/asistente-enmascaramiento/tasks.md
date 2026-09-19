## 1. Manifiesto de sensibilidad (ARS-35)

- [x] 1.1 Crear `database/asistente/manifiesto-sensibilidad.json` con una entrada por cada columna que el manifiesto de privilegios concede a algún rol, clasificada en `publica`, `sensible-valor` o `sensible-texto`
- [x] 1.2 Clasificar `documento`, `cuil`, `telefono` y `fecha_nacimiento` de `identity.personas` como `sensible-valor`, con la etiqueta que el marcador va a mostrar
- [x] 1.3 Clasificar `designaciones.pedido_historial.comentario` como `sensible-texto`, con el motivo escrito
- [x] 1.4 Embeber el manifiesto como recurso del módulo, con el `Target` que falla el build si el archivo no está
- [x] 1.5 `Application/ManifiestoDeSensibilidad.cs` — tipo que deserializa el manifiesto desde el recurso embebido y expone la clasificación por columna cualificada
- [x] 1.6 Rechazar en la carga toda categoría que no sea una de las tres, nombrando la columna
- [x] 1.7 Test: toda columna que el manifiesto de privilegios concede figura clasificada
- [x] 1.8 Test: una columna concedida sin clasificar hace fallar la verificación nombrándola
- [x] 1.9 Test: las cuatro columnas personales son `sensible-valor` y el comentario del historial es `sensible-texto`
- [x] 1.10 Test de anti-vacuidad: la verificación compara un conjunto no vacío de columnas

## 2. Resolución a identificadores del motor (ARS-35)

- [x] 2.1 `Infrastructure/CatalogoDeSensibilidad.cs` — resolver cada entrada del manifiesto al par `(OID de tabla, número de atributo)` con una consulta a `pg_catalog`, cualificando el esquema porque los roles corren con `search_path` vacío
- [x] 2.2 Cachear la resolución por rol, con el mismo patrón de bloqueo que usa el proveedor de esquema, y exponer un contador interno de lecturas para poder afirmar la cache en un test
- [x] 2.3 Fallar la resolución si el manifiesto nombra una columna que no existe en la base, nombrándola
- [x] 2.4 Test: la resolución encuentra las cuatro columnas personales
- [x] 2.5 Test: dos turnos consecutivos con el mismo rol leen el catálogo una sola vez

## 3. Clasificación en el resultado de la ejecución (ARS-35)

- [x] 3.1 `Application/ClasificacionDeSensibilidad.cs` — enum con las tres categorías más `Desconocida` para el origen que el motor no reporta
- [x] 3.2 `Application/ResultadoDeConsulta.cs` — cuarto parámetro posicional con la clasificación por columna, con valor por omisión que trata todo como público
- [x] 3.3 `Infrastructure/EjecutorDeConsulta.cs` — leer `TableOID` y `ColumnAttributeNumber` de cada columna del lector y resolver su clasificación contra el catálogo
- [x] 3.4 Documentar en el ejecutor por qué se clasifica ahí y no en la capa de aplicación
- [x] 3.5 Test contra base real: una columna `sensible-valor` seleccionada **con alias** queda clasificada como sensible
- [x] 3.6 Test contra base real: una columna calculada queda `Desconocida`
- [x] 3.7 Test: el valor por omisión del cuarto parámetro trata todas las columnas como públicas, y está fijado a propósito

## 4. Enmascarador (ARS-36)

- [x] 4.1 `Application/Enmascarador.cs` — función pura que toma el resultado clasificado y devuelve el resultado que va al prompt
- [x] 4.2 Reemplazar cada valor `sensible-valor` por un marcador tomado de un diccionario por respuesta, asignado por orden de primera aparición
- [x] 4.3 Suprimir por completo toda columna `sensible-texto`: fuera de la lista de columnas y fuera de cada fila
- [x] 4.4 Documentar en el código que el marcador es un contador y **no** una función del valor, con el motivo: un hash de un documento es invertible por fuerza bruta
- [x] 4.5 Documentar en el código que el enmascaramiento es asimétrico y no cubre la pregunta cruda del usuario
- [x] 4.6 Test: ningún valor `sensible-valor` aparece en el prompt de redacción
- [x] 4.7 Test: ni el nombre ni los valores de una columna `sensible-texto` aparecen en el prompt
- [x] 4.8 Test: el mismo valor recibe el mismo marcador y valores distintos reciben marcadores distintos
- [x] 4.9 Test: el marcador nombra la clase de dato que reemplaza
- [x] 4.10 Test: las columnas públicas viajan intactas
- [x] 4.11 Test: un resultado cuya única columna es `sensible-texto` deja un resultado enmascarado sin columnas
- [x] 4.12 Verificar en rojo: quitar la supresión de `sensible-texto` hace fallar los tests que la cubren

## 5. Cableado en el carril (ARS-36)

- [x] 5.1 `Application/CarrilSql.cs` — enmascarar entre la ejecución y la redacción, dejando el resultado crudo para el valor de retorno
- [x] 5.2 `Application/ResultadoDelTurno.cs` — exponer la clasificación por columna junto a las filas reales
- [x] 5.3 `ModuleExtensions.cs` — registrar el catálogo de sensibilidad y el manifiesto
- [x] 5.4 Test de punta a punta: el turno devuelve los valores reales y el prompt de redacción no los contiene
- [x] 5.5 Test: el log del turno no contiene ningún valor de ninguna fila
- [x] 5.6 Verificar en rojo: sacar la llamada al enmascarador hace fallar el test de punta a punta

## 6. Cierre de la selección de conexión (ARS-37)

- [x] 6.1 `Infrastructure/ConsultorDeAlcance.cs` — anotar el riesgo residual donde se toma la decisión, con la referencia al endurecimiento pendiente de `identity.personas`
- [x] 6.2 Test: un actor con permiso y alcance global resuelve a la conexión con datos personales
- [x] 6.3 Test: un actor con permiso pero ámbito acotado resuelve a la conexión básica
- [x] 6.4 Test de punta a punta: un actor de ámbito acotado que pregunta por teléfonos no recibe ningún teléfono
- [x] 6.5 Test: el rechazo del motor por falta de privilegio no aparece en el texto de la respuesta

## 7. Documentación en el mismo cambio

- [x] 7.1 `docs/architecture/domains/asistente.md` — la frontera de salida, las tres categorías y la asimetría
- [x] 7.2 `docs/architecture/data-model.md` — el manifiesto de sensibilidad junto al de privilegios
- [x] 7.3 `docs/quality/tech-debt.md` — el riesgo residual de la columna calculada, con su mitigación y su alcance
- [x] 7.4 `backend/src/Modules.Asistente/README.md` — el enmascarador en la descripción del carril

## 8. Hallazgo del camino, resuelto acá

El test de aceptación de la tarea 6.4 descubrió que `PostgresException` **no estaba
contemplada** en el orquestador: cuando el motor rechazaba la lectura por falta de
privilegio —o sea, cuando la defensa funcionaba—, la excepción escapaba del turno
entero y llegaba cruda a quien llamara, con el nombre de la tabla en el mensaje.

Era justamente el criterio de aceptación que ARS-37 declaraba y nadie había
verificado: «un actor sin el permiso que pregunta por un teléfono recibe una
abstención, no un error crudo ni un valor».

- [x] 8.1 `CarrilSql` — capturar `PostgresException` con SQLSTATE `42501` y resolver como abstención
- [x] 8.2 `CarrilSql` — capturar cualquier otro rechazo del motor y resolverlo sin exponer el mensaje crudo
- [x] 8.3 `PoliticaDeAbstencion` — dos textos nuevos: sin acceso a los datos, y error al consultar
- [x] 8.4 Test: el actor acotado que pregunta por teléfonos recibe una abstención y ningún teléfono
- [x] 8.5 Test: el mensaje del motor no aparece en la respuesta, ni el SQLSTATE, ni el nombre de la tabla
