## Context

El acceso al asistente hoy es sólo el permiso `asistente.consultar`, que vive en identity. El módulo no puede escribir identity (AGENTS.md, regla 4), así que el control operativo del panel necesita estado propio en el schema `asistente`, igual que el cupo.

## Decisions

### D1 — El acceso es una capa **adicional** al permiso, nunca un reemplazo

`asistente.consultar` sigue siendo la puerta de autorización de los endpoints. El acceso operativo sólo puede **restringir**: un rol o usuario sin ese permiso no gana nada por tener el interruptor encendido. Por eso el default de la columna es `true` y la ausencia de fila de revocación significa «hereda».

### D2 — Regla de acceso efectivo (`ReglaDeAccesoEfectivo`)

1. Si el usuario tiene una revocación vigente → **sin acceso · propio**.
2. Si no tiene ningún rol de sistema → **con acceso** (lo decide sólo el permiso; no hay rol del que heredar).
3. Si **alguno** de sus roles tiene el acceso habilitado → **con acceso · del rol**; si todos lo tienen apagado → **sin acceso · del rol**.

«Alguno» y no «todos» es deliberado: un rol extra no puede quitarle el acceso a quien otro rol se lo da — el acceso se quita explícitamente por usuario. Una única clase resuelve la regla para el bloqueo del turno y para lo que muestra el panel, así no pueden divergir (a diferencia del espejo documentado de `ReglaDeCupoEfectivo`).

### D3 — «Quitar, no dar»

La revocación por usuario es un booleano: existe o no existe. No hay un «acceso propio concedido» que pueda saltarse un rol apagado; restablecer sólo borra la revocación.

### D4 — El bloqueo usa el carril de degradación existente

`MotivoSinModelo.SinAcceso`, consultado **primero** en `DisponibilidadDelModeloReal`, resuelve como `ServicioDegradado` con un texto propio y el motivo visible `sin_acceso`. Ningún bypass: ni el de mantenimiento del admin lo levanta.

### D5 — Restablecer el cupo cierra la vigencia

`presupuesto_usuario` es versionado; restablecer hace `UPDATE … SET vigente_hasta = now()` sin abrir fila nueva. El historial queda completo.
