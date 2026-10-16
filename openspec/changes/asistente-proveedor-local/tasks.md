## 1. Puerto y fallas

- [x] 1.1 `SolicitudAlModelo.EsquemaDeSalidaJson`, opcional (D3)
- [x] 1.2 `ProveedorSaturado` en `FallasDelProveedor` (D4)

## 2. Adaptador local (D2)

- [x] 2.1 `ProveedorLocal`: mensajes system/user, temperatura, `max_tokens`, `enable_thinking`, `response_format`
- [x] 2.2 Uso de tokens (`prompt_tokens`, `cached_tokens`), `finish_reason=length`, descarte de `<think>`
- [x] 2.3 Traducción de fallas (credencial, armado, transporte)
- [x] 2.4 Brazo `local` en `ConstruirProveedor` y opción `UrlDelProveedorLocal`

## 3. Concurrencia (D4)

- [x] 3.1 `CompuertaDelModelo` singleton con prioridad para turnos ya empezados
- [x] 3.2 `ProveedorConCompuerta` en `Encadenar`, antes del breaker
- [x] 3.3 Opciones `MaximoDeLlamadasConcurrentes` y `EsperaMaximaEnColaSegundos`, con validación
- [x] 3.4 El carril resuelve `ProveedorSaturado` como degradación con texto propio

## 4. Pipeline

- [x] 4.1 La generación declara su esquema de salida (D3)
- [x] 4.2 La reescritura que falla usa la pregunta cruda (D5)
- [x] 4.3 Opción `ReintentarConsultaVacia` (D6)

## 5. Infraestructura y documentación

- [x] 5.1 `infra/compose/compose.llm.yml` (vLLM, alternativa llama-server), perfil `compose.asistente-local.yml` y `spin-up.sh`
- [x] 5.2 `docs/architecture/modelo-local.md` con la investigación y las fuentes
- [x] 5.3 README del módulo (tablas de configuración) e `infrastructure.md`

## 6. Tests

- [x] 6.1 Adaptador contra transporte falso: request, uso, fallas, thinking, esquema
- [x] 6.2 Compuerta: límite, cola, saturación sin tocar el breaker, prioridad
- [x] 6.3 Opciones y composición (`Proveedor=local`)
- [x] 6.4 Reescritura que falla y reintento desactivado
