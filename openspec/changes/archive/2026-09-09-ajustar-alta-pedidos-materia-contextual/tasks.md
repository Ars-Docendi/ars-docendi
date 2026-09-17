## 1. Contrato de identidad y pedidos

- [x] 1.1 Agregar primero tests backend para un Alta con DNI/nombre/apellido sin `personaId`, para documento duplicado y para confirmar que no se crea `identity.users`; verificar que fallen antes de implementar el contrato.
- [x] 1.2 Exponer la operación pública mínima de identity para crear o resolver una persona sin cuenta, con unicidad por documento y auditoría; verificar con tests de persistencia y concurrencia que no duplica personas.
- [x] 1.3 Cambiar el DTO de creación/edición de pedidos para admitir datos de persona sólo en Alta y `personaId` para Baja/Cambio; verificar que las combinaciones inválidas sean rechazadas con errores de validación sin historial parcial.
- [x] 1.4 Integrar la resolución canónica de persona con la creación del pedido y el primer-login existente; verificar que un Alta válida persista persona y pedido, y que el primer login vincule la cuenta sin crear otra persona.

## 2. Materia contextual y validación backend

- [x] 2.1 Agregar tests de dominio/API para Alta con cualquiera de las materias del Jefe, Baja/Cambio con la intersección actor-docente, una única opción, varias opciones, ninguna opción y materia manipulada fuera del ámbito; verificar red-green.
- [x] 2.2 Validar en `ServicioPedidos` que Alta use sólo `MateriasACargo` y que Baja/Cambio requieran una designación vigente del docente en `materiaId`; verificar que el backend rechace materias ajenas aunque el frontend las envíe.
- [x] 2.3 Hacer que snapshot, horas y datos actuales de Baja/Cambio se obtengan de la designación correspondiente a la materia elegida; verificar que no se use la primera designación del docente cuando tiene varias.
- [x] 2.4 Mantener la validación de una materia por pedido, el enrutamiento por carrera y la unicidad por persona/período; verificar la suite existente de pedidos y persistencia.

## 3. Formulario frontend

- [x] 3.1 Actualizar primero los tests de `PedidoForm` para quitar el selector superior, mostrar DNI/nombre en Alta, mostrar materias del actor en Alta y materias compatibles después de seleccionar docente en Baja/Cambio; verificar los casos de una, varias y cero materias.
- [x] 3.2 Conservar `materiaId` en las asignaciones actuales del catálogo y en `DocenteExistente`; verificar que el modelo no dependa de comparar nombres para identificar la materia.
- [x] 3.3 Refactorizar `PedidoFormPage` y `PedidoForm` para mantener una única selección contextual de materia y no remontar el formulario por cambios del selector superior; verificar que Alta y edición conserven sus valores esperados.
- [x] 3.4 Refactorizar `SeccionDocentePedido` para que Alta use campos de persona nueva y Baja/Cambio use docente seguido de materia contextual; verificar errores inline y estados sin opciones.
- [x] 3.5 Actualizar el payload frontend para enviar el UUID de materia elegido y los datos de persona de Alta; verificar con tests de `pedidosApi` que no se envíe una materia implícita ni una persona administrativa del catálogo.

## 4. Documentación y compatibilidad

- [x] 4.1 Actualizar el design spec del proyecto docente con el flujo condicional de materia, el Alta con datos nuevos y la ausencia de creación automática de usuario; verificar que no contradiga las delta specs.
- [x] 4.2 Actualizar `docs/architecture/api-contracts-designaciones.md`, `docs/architecture/api-contracts.md`, `docs/architecture/data-model.md` y `docs/architecture/domains/designaciones.md` con el payload, la resolución de persona y las validaciones; verificar enlaces y nombres canónicos.
- [x] 4.3 Revisar el grafo de dependencias y documentar la frontera pública de identity sin agregar referencias entre implementaciones de módulos; verificar que el análisis de arquitectura siga acíclico.

## 5. Verificación final

- [x] 5.1 Ejecutar `dotnet test backend/ArsDocendi.slnx` y `pnpm --filter frontend test:run`; verificar todos los tests verdes.
- [x] 5.2 Ejecutar `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check` y `pnpm exec openspec validate --all --strict`; verificar que no queden errores de lint, build, formato ni OpenSpec.
