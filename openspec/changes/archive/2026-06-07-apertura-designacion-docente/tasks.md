## 1. Tipos y mock data

- [x] 1.1 Definir tipo `PeriodoDesignacion` en `features/designaciones/types.ts` (campos: id, nombre, cuatrimestre, anio, fechaApertura, fechaCierre, estado)
- [x] 1.2 Definir tipo `EstadoPeriodo` como union type (`"abierto" | "cerrado" | "proximo"`)
- [x] 1.3 Crear `features/designaciones/api/periodosMock.ts` con array `PERIODOS_MOCK` de 4 períodos variados (1 Abierto, 2 Cerrados, 1 Próximo; distintos años y cuatrimestres)

## 2. Routing

- [x] 2.1 Agregar child route `{ path: "periodos", element: <PeriodosPage /> }` en `features/designaciones/routes.tsx`

## 3. Componente de dominio EstadoPeriodoBadge

- [x] 3.1 Crear `features/designaciones/components/EstadoPeriodoBadge.tsx` con prop `estado: EstadoPeriodo`
- [x] 3.2 Implementar estilos usando únicamente tokens del tema (`--color-status-success-*`, `--color-status-warning-*`, `--color-border-default`, etc.) — sin valores de color hardcodeados
- [x] 3.3 Verificar que los tres estados (Abierto/verde, Próximo/amarillo, Cerrado/gris) se renderizan correctamente

## 4. Modal crear/editar período

- [x] 4.1 Crear `features/designaciones/components/ModalPeriodo.tsx` con props: `open`, `onOpenChange`, `periodo` (opcional — si viene, es edición; si no, es alta)
- [x] 4.2 Implementar formulario con `Field` + `Input` (Nombre), `Field` + `Select` (Cuatrimestre: 1C/2C/Verano), `Field` + `Input type="number"` (Año), `Field` + `DatePicker` (Fecha apertura), `Field` + `DatePicker` (Fecha cierre), `Field` + `Select` (Estado)
- [x] 4.3 Pre-poblar campos cuando `periodo` esté presente (modo edición)
- [x] 4.4 Agregar slot `InlineAlert` de severidad `"warning"` en la parte superior del body del modal, renderizado condicionalmente (prop `error?: string`)
- [x] 4.5 Implementar footer con `Button` variant `"secondary"` (Cancelar) y `Button` variant `"primary"` (Guardar)
- [x] 4.6 Verificar que el título cambia entre "Nuevo período" y "Editar período" según el modo

## 5. Modal confirmación eliminar

- [x] 5.1 Crear `features/designaciones/components/ModalEliminarPeriodo.tsx` con props: `open`, `onOpenChange`, `periodo`, `onConfirmar`
- [x] 5.2 Implementar body con texto de confirmación que mencione el nombre del período
- [x] 5.3 Agregar slot `InlineAlert` de severidad `"danger"` condicionalmente (prop `error?: string`)
- [x] 5.4 Implementar footer con `Button` variant `"secondary"` (Cancelar) y `Button` variant `"destructive"` (Eliminar)

## 6. Tabla de períodos

- [x] 6.1 Crear `features/designaciones/components/TablaPeriodos.tsx` con prop `periodos: PeriodoDesignacion[]` y callbacks `onEditar` y `onEliminar`
- [x] 6.2 Implementar con `Table.Root`, `Table.Head`, `Table.Body` y columnas: Nombre, Cuatrimestre, Año, Apertura, Cierre, Estado, Acciones
- [x] 6.3 Renderizar `EstadoPeriodoBadge` en la columna Estado
- [x] 6.4 Agregar botones de acción por fila: `Button` variant `"ghost"` con ícono de editar (llama `onEditar`) y `Button` variant `"ghost"` con ícono de eliminar (llama `onEliminar`)
- [x] 6.5 Formatear fechas en columnas Apertura y Cierre como `DD/MM/YYYY`

## 7. PeriodosPage — composición

- [x] 7.1 Crear `features/designaciones/pages/PeriodosPage.tsx`
- [x] 7.2 Agregar `Breadcrumbs` con items `[{ label: "Inicio", href: "/" }, { label: "Pedidos", href: "/designaciones" }, { label: "Períodos de designación" }]`
- [x] 7.3 Agregar `PageHeader` con `pretitle="Configuración"`, `title="Períodos de designación"` y `actions=<Button variant="primary">Nuevo período</Button>`
- [x] 7.4 Manejar estado local: `periodos` (inicializado con `PERIODOS_MOCK`), `modalPeriodoAbierto`, `periodoEditando`, `modalEliminarAbierto`, `periodoEliminando`
- [x] 7.5 Conectar `TablaPeriodos` con los handlers de editar y eliminar
- [x] 7.6 Conectar `ModalPeriodo` con estado de alta/edición y handler de guardado (actualiza array mock en memoria)
- [x] 7.7 Conectar `ModalEliminarPeriodo` con handler de confirmación (filtra el período del array en memoria)
- [x] 7.8 Verificar que alta, edición y eliminación funcionan visualmente sobre el mock en memoria sin recargar la página

## 8. IndexPage — botón de acceso a períodos

- [x] 8.1 Agregar en `features/designaciones/pages/IndexPage.tsx` un `Button` variant `"secondary"` con label "Configurar períodos de designación" que navega a `/designaciones/periodos` usando `useNavigate`
