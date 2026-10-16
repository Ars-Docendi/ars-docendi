# Tareas

## 1. La enmienda

- [x] 1.1 Enmendar por escrito D1 de `asistente-lee-portal/design.md`: el ámbito entra, y por qué el argumento original valía sólo a falta de definición del cliente.
- [x] 1.2 Corregir `BR-portal-002` y agregar la regla de ámbito a `docs/business-rules/portal.md`.
- [x] 1.3 Corregir `docs/product/consulta-secretaria-portal-asistente.md`: hoy **advierte** que un Coordinador vería todo el padrón, y eso deja de ser cierto. La pregunta 2 del gate cambia de forma.

## 2. El motor

- [x] 2.1 `identity.asistente_personas_visibles()`: global ve a todos; el resto, a quienes tengan designación vigente en una materia visible.
- [x] 2.2 Las policies de las seis tablas de portal conjugan ámbito, sin tocar la rama del perfil propio.
- [x] 2.3 Test: jefe ve a los suyos, no a los ajenos; coordinador ve su carrera; global ve todo.
- [x] 2.4 Test: sin designación vigente, sólo lo alcanzan los globales.
- [x] 2.5 Test: el perfil propio se ve sin permiso y sin designación.

## 3. El alcance por dominio

- [x] 3.1 `AlcanzaTodo` deja de ser un booleano del turno; se evalúa contra los dominios que la consulta tocó.
- [x] 3.2 La detección se **reusa** de `CoberturaDelPortal.TablasQueToca`. No escribir una segunda.
- [x] 3.3 Conservador ante un dominio desconocido: no alcanza todo.
- [x] 3.4 Test del defecto medido: actor global sin el permiso de portal pregunta por una habilidad que existe y NO recibe «no encontré ningún registro».
- [x] 3.5 Test: una consulta que no toca portal conserva la evaluación de antes.

## 4. Medición

- [ ] 4.1 Revisar las referencias del eje de capacidad: cambia qué filas ve cada actor.
- [ ] 4.2 Corrida financiada. **Requiere autorización de gasto explícita.**
