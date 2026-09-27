# Modules.Asistente.Contracts

## La decisión: conservado

Este proyecto nació vacío a propósito mientras el equipo decidía si conservarlo o
borrarlo: la convención del repo es que cada `Modules.<X>` tenga su
`Modules.<X>.Contracts` como única superficie pública cross-module, y con el
asistente esa simetría no se sostenía sola — el módulo **consumía** Contracts
ajenos pero **nadie lo consumía a él**.

`sistema-seccion-unificada` (ARS-157) cerró la decisión en **conservarlo**: el
asistente publica su primer contrato porque el Host necesita leer
`asistente.auditoria_administracion` y el estado de mantenimiento sin cruzar a
SQL directo ni referenciar los internals del módulo (regla 1 de AGENTS.md).

## Primeros tipos y consumidores

- `IConsultasDeAuditoriaDeAdministracion` / `EventoDeAdministracion` /
  `CampoDeAdministracion` / `LoteDeAuditoriaDeAdministracion`: el feed unificado
  de auditoría (`ServicioAuditoria` en `ArsDocendi.Host`) lee el rastro de
  administración del asistente a través de esta interfaz — nunca el JSON crudo
  de `antes`/`despues`, que el módulo normaliza antes de exponerlo.
- `IConsultaDeMantenimiento` / `EstadoDeMantenimientoPublico`: `GET
/api/administracion/sistema/estado` la usa para mostrar el modo mantenimiento
  a quien sólo tiene `sistema.estado.ver`, sin exponer la razón ni el actor
  (esos quedan detrás de `asistente.consultar`).

`Modules.Asistente` implementa ambas como clases `internal` registradas en su
`ModuleExtensions`; los tipos públicos de esta implementación son exactamente los
de este proyecto. `ArsDocendi.Host` y `Modules.Asistente` referencian este
proyecto; este proyecto no referencia a nadie, así que ningún ciclo es posible.

## Reglas

Valen las mismas que para el resto de los `.Contracts` del repo: sólo DTOs,
interfaces y tokens. **Sin lógica.**
