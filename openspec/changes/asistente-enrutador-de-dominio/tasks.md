## 1. Términos excluidos en el catálogo

- [x] 1.1 `Intencion` suma `Excluye`, y el JSON su campo
- [x] 1.2 La validación exige que los excluidos estén normalizados
- [x] 1.3 La validación rechaza un término exigido y excluido a la vez
- [x] 1.4 `pedidos-de-una-novedad` excluye `cuantos`
- [x] 1.5 Test: un término excluido impide el reconocimiento
- [x] 1.6 Test: un excluido sin normalizar no carga
- [x] 1.7 Test: exigido y excluido a la vez no carga

## 2. El enrutador

- [x] 2.1 `EnrutadorDeDominio` — devuelve la intención resuelta o nada
- [x] 2.2 No recibe proveedor del modelo ni cliente de otro módulo
- [x] 2.3 Test: una pregunta cubierta devuelve su intención con los slots
- [x] 2.4 Test: una pregunta no cubierta devuelve nada y no falla
- [x] 2.5 Test: un slot sin resolver no enruta
- [x] 2.6 Test: el enrutador no depende del proveedor del modelo

## 3. El banco de preguntas negativas

- [x] 3.1 Banco leído de `capacidad.json` y `robustez.json`
- [x] 3.2 El fallo nombra la pregunta y la intención culpable
- [x] 3.3 Test: ninguna pregunta del eje de capacidad se captura
- [x] 3.4 Test: ninguna pregunta del eje de robustez se captura
- [x] 3.5 Verificar en rojo: sacarle el excluido a la intención de novedad hace fallar 3.3

## 4. Cableado en modo sombra

- [x] 4.1 La capa conversacional consulta al enrutador entre el reescritor y la ambigüedad
- [x] 4.2 Registra qué intención habría enrutado, y sigue al carril SQL
- [x] 4.3 El comentario dice que está en sombra y qué falta para conectarlo
- [x] 4.4 Test: una pregunta capturada queda registrada y responde igual que antes
- [x] 4.5 Test: una colisión termina en aclaración y no en el carril determinista
- [x] 4.6 Test: la decisión no mueve el contador de llamadas al modelo

## 5. Documentación

- [x] 5.1 `docs/architecture/domains/asistente.md` — el enrutador, el modo sombra y el banco negativo
- [x] 5.2 Dejar escrito qué falta para conectarlo de verdad
