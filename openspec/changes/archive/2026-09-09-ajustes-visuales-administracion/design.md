## Context

El frontend ya centraliza la tipografía y los tamaños base en los tokens de `@ars-docendi/ui`.
Usuarios y Docentes consumen esos tokens mediante `Table`, `Input` y `Select`, mientras que Roles
define parte de sus controles nativos en `roles.css` con tamaños `rem` independientes. La tabla de
Usuarios también conserva una columna de ámbitos que no forma parte de la presentación canónica.
El componente `DatePicker` de la librería agrega un SVG `.cal-ico`; el pedido alcanza únicamente al
modal de edición de usuarios.

## Goals / Non-Goals

**Goals:**

- Hacer coincidir la superficie visible de Usuarios con las columnas definidas por su spec.
- Mantener intactas las membresías que necesita el editor de usuarios.
- Usar un control nativo de fecha en el modal de edición, sin SVG adicional y con el mismo formato
  de valor.
- Alinear la familia tipográfica, el tamaño base y los tamaños secundarios de Roles con la escala
  visual administrativa existente.
- Dejar pruebas de presentación para la ausencia de la columna y del icono, y una verificación
  mínima de la superficie de Roles.

**Non-Goals:**

- No cambiar endpoints, DTOs, payloads, persistencia, permisos ni reglas de membresías.
- No modificar el formulario de alta de usuarios ni el `DatePicker` compartido para otras
  pantallas.
- No crear un nuevo componente tipográfico, wrapper o dependencia; se reutilizan los controles y
  tokens existentes.
- No rediseñar el layout de Roles ni convertirlo en una tabla distinta.

## Decisions

1. **Eliminar la columna en el componente de tabla.** Se quitarán el encabezado, la celda y el
   helper de resumen de ámbitos de `TablaUsuarios`. El modelo, la consulta y `MembresiasSelector`
   no se tocan, por lo que abrir el editor seguirá mostrando las asignaciones completas.

2. **Usar `Input` con `type="date"` sólo en edición.** El `Input` existente mantiene la clase y
   la accesibilidad del design system, pero no agrega el SVG que renderiza `DatePicker`. Se
   conservarán `value`, `onChange`, el campo obligatorio y el formato ISO `YYYY-MM-DD`. Ocultar el
   SVG mediante CSS se descarta porque dejaría el componente incorrecto y podría afectar otras
   pantallas.

3. **Alinear Roles mediante CSS local y tokens existentes.** Los paneles, el buscador, el texto de
   descripción, las acciones y los metadatos usarán `var(--font-sans)` y los tamaños
   `--text-body-sm-size`, `--text-caption-size` y `--text-micro-size` según su jerarquía. El
   buscador nativo heredará la tipografía del panel. No se agregará una abstracción compartida para
   una sola pantalla.

4. **Verificar comportamiento sin ampliar el alcance.** Se agregarán pruebas de componente para
   confirmar que Usuarios no expone el encabezado `Ámbitos` y que el editor usa un input de fecha
   sin `.cal-ico`. La coherencia tipográfica de Roles se validará con los estilos de tokens y el
   build/lint del frontend; no se introducirá una prueba frágil de píxeles.

5. **Actualizar la documentación de UX.** Se explicitarán en los design specs administrativos la
   ausencia de la columna, el control nativo del editor y la escala tipográfica compartida.

## Risks / Trade-offs

- **El icono de fecha nativo varía por navegador** → se elimina sólo el SVG propio de la librería,
  conservando el control nativo y su affordance estándar.
- **Un selector de CSS demasiado amplio puede alterar otra pantalla** → los estilos se limitarán
  a `.roles-list-panel` y `.roles-permissions-panel`.
- **El texto puede cambiar de ancho en Roles** → se mantiene el layout actual y se verifica el
  responsive existente a 760 px.

## Migration Plan

1. Aplicar los cambios únicamente en el frontend y en los design specs/documentos delta de este
   change.
2. Ejecutar las pruebas de componentes, lint, build y formato proporcionales.
3. Revertir los archivos del change si la revisión visual detecta una regresión; no hay migración
   de datos ni despliegue coordinado con backend.
