## 1. Unificar el cliente de inferencia

- [x] 1.1 Mover `InferenciaOptions` a `Modules.Asistente/Configuracion/OpcionesInferencia.cs`, con la autenticación por pedido.
- [x] 1.2 Mover `ClienteInferencia` y `GET /api/inferencia/estado` al módulo, sin cambiar ruta, permiso ni contrato.
- [x] 1.3 Registrar opciones, validación al arranque (salvo Development) y cliente en `AddAsistenteModule`.
- [x] 1.4 Eliminar `ArsDocendi.Inferencia` del repo, de la solución, del Host y del manifiesto de aristas; mover sus tests.

## 2. Adaptador `ollama`

- [x] 2.1 `ProveedorOllama` contra `POST /api/chat`: mensajes, `think`, `format`, `options` (`temperature`, `num_predict`, `num_ctx`).
- [x] 2.2 Respuesta entera y por fragmentos NDJSON; tokens; corte por techo; razonamiento colado descartado.
- [x] 2.3 Traducción de fallas (401/403/404/429/400/5xx, `error` en el flujo, flujo sin cierre) sin loguear prompt ni secretos.
- [x] 2.4 Opción `Asistente:ContextoDelModelo` (0 = la del servidor) y brazo `ollama` en el registro de adaptadores.
- [x] 2.5 Tests del adaptador contra un servidor falso, de composición y de que las credenciales no viajan a otro proveedor.

## 3. Despliegue

- [x] 3.1 `infra/compose/compose.asistente-ollama.yml` con el perfil del servicio compartido.
- [x] 3.2 `spin-up.sh`: con `ollama` suma el perfil y exige `INFERENCIA_*` en vez de `ASISTENTE_CLAVE`; tests.
- [x] 3.3 `pr-env-deploy.yml`: la label `asistente-ollama` elige el proveedor `ollama`.

## 4. Documentación

- [x] 4.1 README del módulo, `dependency-graph.md` y `api-contracts-administracion.md`.
- [x] 4.2 `docs/architecture/modelo-local.md` y `docs/operations/ollama-compartido.md`: configuración recomendada del servidor para el asistente.

## 5. Verificación en el preview (operador)

- [ ] 5.1 Desplegar un PR con `deploy-preview` + `asistente-ollama` y comprobar «Inferencia (IA)» disponible y un turno real.
- [ ] 5.2 Correr el evaluador contra el servicio con los límites acordados y registrar la medición.
