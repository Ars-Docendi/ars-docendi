## Why

El módulo de Designaciones necesita que Secretaría Académica pueda parametrizar los períodos durante los cuales los Jefes de Cátedra pueden generar pedidos de designación docente. Sin esta pantalla, no existe forma de abrir o cerrar un período desde la interfaz, bloqueando el inicio del workflow completo.

## What Changes

- Nueva ruta `/designaciones/periodos` con pantalla de gestión de períodos (lista, alta, edición, eliminación).
- `IndexPage` de `/designaciones` se mantiene como landing del módulo y agrega un botón "Configurar períodos de designación" que navega a la nueva ruta.
- Lista de períodos de designación con estado visual (Abierto / Próximo / Cerrado).
- Formulario de alta/edición de períodos en modal (nombre, cuatrimestre, año, fechas de apertura y cierre, estado).
- Confirmación de eliminación en modal con slot de error listo para validaciones de backend.
- Componente de dominio `EstadoPeriodoBadge` dentro del feature, usando tokens del tema existente.
- Mock data con períodos variados para validación visual con el cliente.

## Capabilities

### New Capabilities

- `gestion-periodos`: CRUD visual de períodos de designación — listar, crear, editar y eliminar períodos; mostrar estado Abierto/Cerrado; slots de InlineAlert listos para reglas de negocio futuras (solapamiento de fechas, pedidos asociados al período).

### Modified Capabilities

_(ninguna — el IndexPage actual es un placeholder sin spec asociado)_

## Impact

- **Frontend**: `features/designaciones/` — se modifican `types.ts`, `routes.tsx` e `IndexPage.tsx`; se crea `pages/PeriodosPage.tsx` y tres componentes nuevos en `components/`.
- **Backend**: ninguno en este cambio — toda la persistencia es mock data estático.
- **Routing**: se agrega child route `periodos` dentro de `designacionesRoutes`. La sidebar no cambia.
- **Pendiente explícito**: conexión con el endpoint real de períodos (GET/POST/PUT/DELETE `/api/designaciones/periodos`).
