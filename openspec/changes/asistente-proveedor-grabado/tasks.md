## 1. La clave y el sello, sin tocar HTTP

- [x] 1.1 Test rojo primero: la misma solicitud produce la misma clave en dos procesos distintos
- [x] 1.2 Test rojo primero: cambiar prefijo, mensaje, esfuerzo o modelo —de a uno— cambia la clave
- [x] 1.3 Test rojo primero: cambiar solo el techo de tokens NO cambia la clave
- [x] 1.4 `ClaveDeCassette` — huella SHA-256 de los cuatro campos, con el criterio de `ProveedorSimulado.Huella`
- [x] 1.5 Los cuatro campos se leen del cuerpo de la solicitud; un campo esperado ausente falla nombrándolo
- [x] 1.6 Test: un cuerpo sin el campo del modelo falla ruidoso y no produce una clave sobre cadena vacía
- [x] 1.7 Verificar en rojo: hashear el cuerpo completo hace fallar 1.3

## 2. El sobre en disco

- [x] 2.1 Test rojo primero: el cassette escrito declara modelo, fecha, hash del prefijo y hash del fixture
- [x] 2.2 Test rojo primero: el cuerpo almacenado es byte por byte el recibido
- [x] 2.3 Test rojo primero: un cassette al que le falta un campo del sello no se sirve y el error nombra archivo y campo
- [x] 2.4 `SelloDelCassette` — los cuatro campos, y la escritura falla si falta cualquiera
- [x] 2.5 `AlmacenDeCassettes` — leer y escribir el sobre JSON: sello arriba, cuerpo verbatim abajo
- [x] 2.6 La escritura es atómica: un fallo a mitad de camino no deja archivo a medio escribir
- [x] 2.7 Test: una escritura interrumpida no deja archivo en el directorio
- [x] 2.8 Verificar en rojo: reserializar el cuerpo con `JsonSerializer` hace fallar 2.2

## 3. El sello se verifica antes de servir

- [x] 3.1 Test rojo primero: un cassette sellado con otro hash de prefijo se rechaza en vez de servirse
- [x] 3.2 Test rojo primero: con el directorio lleno de cassettes de otro prefijo, el error dice que el prefijo cambió
- [x] 3.3 Test rojo primero: un cassette sin el hash del fixture vigente no se sirve
- [x] 3.4 Verificación del sello contra el prefijo vigente y el fixture vigente, antes de devolver nada
- [x] 3.5 Mensajes de error que distinguen «falta el cassette» de «los cassettes son de otro prefijo»
- [x] 3.6 Verificar en rojo: servir el cassette sin mirar el sello hace fallar 3.1

## 4. El handler en el pipeline

- [x] 4.1 Test rojo primero: sin cassette y sin la variable de re-grabación, el transporte de adentro recibe cero solicitudes
- [x] 4.2 Test rojo primero: el error de falla cerrada nombra la clave faltante y el directorio donde se la buscó
- [x] 4.3 Test rojo primero: con la re-grabación puesta y sin cassette, la llamada sale y queda grabada
- [x] 4.4 Test rojo primero: con el cassette presente, la llamada no sale aunque la re-grabación esté puesta
- [x] 4.5 `GrabadorDeCassettes : DelegatingHandler` — reproducir, grabar o fallar cerrado
- [x] 4.6 No nombra el SDK: lee campos del cable (`system`, `messages`, `model`, `output_config`)
- [x] 4.7 Nunca almacena cabeceras de la solicitud
- [x] 4.8 Structured logging con Serilog: qué clave se sirvió, cuál se grabó, cuál faltó
- [x] 4.9 Verificar en rojo: llamar a `base.SendAsync` antes de decidir hace fallar 4.1

## 5. El orden respecto del reintento

- [x] 5.1 Test rojo primero: un transporte que falla y después responde deja UN solo cassette, con el cuerpo exitoso
- [x] 5.2 Test rojo primero: reproducir un cassette de una llamada que necesitó reintentos no espera ningún backoff
- [x] 5.3 Registrar el grabador ANTES de `ReintentoDeTransporte` en `AddHttpClient(ClienteDelProveedor)`
- [x] 5.4 Test: el reintento sigue presente en el pipeline y conserva su comportamiento (`ReintentoYTechoTests` en verde)
- [x] 5.5 Verificar en rojo: registrarlo después del reintento hace fallar 5.1

## 6. Configuración y apagado por default

- [x] 6.1 `OpcionesAsistente` — directorio de cassettes y variable de re-grabación, las dos vacías por default
- [x] 6.2 Con el directorio vacío el handler NO se registra; el pipeline queda idéntico al de hoy
- [x] 6.3 Test rojo primero: sin configuración, el pipeline del cliente del proveedor es el de antes del cambio
- [x] 6.4 Test: el `ping` responde con el mecanismo apagado
- [x] 6.5 Test: `OpcionesDocumentadasTests` cubre las dos opciones nuevas
- [x] 6.6 Verificar en rojo: registrar el handler siempre hace fallar 6.3

## 7. Grabar y reproducir punta a punta, sin clave

- [x] 7.1 Test rojo primero: grabar contra un transporte que impersona la API deja el cassette en un directorio temporal
- [x] 7.2 Test rojo primero: reproducir ese cassette con un terminal que falla si lo llaman devuelve la misma `RespuestaDelModelo`
- [x] 7.3 Test: el motivo de corte por techo de tokens sobrevive el viaje a disco y vuelve como `SeQuedoSinTokens`
- [x] 7.4 Test: los tokens de entrada, de salida y de caché salen del cuerpo grabado
- [x] 7.5 Test: un cuerpo sin bloque de texto se reproduce como texto vacío y no como falla de transporte
- [x] 7.6 Verificar en rojo: apuntar la reproducción a un directorio equivocado hace fallar 7.2 por el terminal que explota

## 8. Higiene: sin credencial, sin PII, sin excepción de arquitectura

- [ ] 8.1 Test rojo primero: un guard barre los cassettes versionados buscando la forma de la credencial y no encuentra nada
- [ ] 8.2 Test rojo primero: el detector reconoce una credencial en un cassette sintético
- [ ] 8.3 Test rojo primero: todos los cassettes versionados declaran el hash del fixture sintético vigente
- [ ] 8.4 Test: `El_SDK_del_proveedor_se_nombra_en_un_solo_archivo` sigue en verde sin sumar excepciones
- [ ] 8.5 Test: el módulo sigue referenciando solo `ArsDocendi.Shared`

## 9. Los cassettes alimentan el parseo

- [ ] 9.1 Test rojo primero: un directorio de cassettes vacío falla diciendo que no hay nada que ejercitar
- [ ] 9.2 Los tests de parseo iteran el directorio: un caso por cassette encontrado
- [ ] 9.3 `GeneradorDeSql.Interpretar` sobre cada cassette de generación
- [ ] 9.4 El redactor sobre cada cassette de redacción
- [ ] 9.5 El reescritor sobre cada cassette de reescritura
- [ ] 9.6 Los primeros cassettes versionados, grabados contra el transporte que impersona la API
- [ ] 9.7 Test: agregar un cassette al directorio suma un caso sin tocar ningún archivo de test
- [ ] 9.8 Verificar en rojo: vaciar el directorio hace fallar 9.1

## 10. Documentación

- [ ] 10.1 `docs/quality/tech-debt.md` — TD-017: fixture congelada sin recaptura programada no detecta un cambio de formato de cable
- [ ] 10.2 `backend/eval/README.md` — qué deja grabado la corrida financiada y por qué se commitea
- [ ] 10.3 `backend/src/Modules.Asistente/README.md` — las dos opciones nuevas y que un cassette prueba el parseo, no la calidad
- [ ] 10.4 `docs/architecture/domains/asistente.md` — el grabador en el pipeline y por qué va por fuera del reintento
- [ ] 10.5 Dejar escrito que la corrida financiada la bloquea ARS-67 y que este cambio no la incluye
