## 1. Datos

- [x] 1.1 `006_asistente_administracion.sql` — `presupuesto_rol.acceso_habilitado` (default `true`) con `ADD COLUMN IF NOT EXISTS`
- [x] 1.2 `006_asistente_administracion.sql` — tabla `acceso_usuario_revocado`

## 2. Backend

- [x] 2.1 `ReglaDeAccesoEfectivo` — la regla de D2, única para turno y panel
- [x] 2.2 `IAccesoAlAsistente` + `AccesoPersistente`
- [x] 2.3 `MotivoSinModelo.SinAcceso`, primero en `DisponibilidadDelModeloReal`; motivo visible `sin_acceso`
- [x] 2.4 `IPresupuestosAdministrables` — editar acceso de rol, revocar/restablecer acceso de usuario, restablecer cupo de usuario; el estado trae acceso por rol y revocaciones
- [x] 2.5 Endpoints `PUT …/presupuestos/roles/{rol}/acceso`, `PUT …/presupuestos/usuarios/{actorId}/acceso`, `DELETE …/presupuestos/usuarios/{actorId}`, auditados
- [x] 2.6 `GET …/uso` — cada fila de usuario trae `accesoEfectivo`/`origenDeAcceso`

## 3. Frontend

- [x] 3.1 Columna «Acceso» en «Por usuario» y «Por rol», con restablecer
- [x] 3.2 Restablecer el cupo propio; cupo atenuado sin acceso
- [x] 3.3 `IndicadorDeCupo` — texto para `sin_acceso`

## 4. Tests y docs

- [x] 4.1 Unitarios de `ReglaDeAccesoEfectivo`
- [x] 4.2 Integración: revocar bloquea el turno; restablecer lo devuelve; rol apagado bloquea
- [x] 4.3 Tests del panel
- [x] 4.4 `api-contracts.md`, `data-model.md`, design spec
