## Context

La app no tiene backend de gestión de identidad/permisos implementado aún. El módulo de usuarios existe en el frontend como datos mock (ver `frontend/src/features/usuarios/mock/mockStore.ts`). Esta feature extiende ese patrón para roles y permisos: dos stores mock independientes en memoria reemplazan al backend hasta que los endpoints reales estén disponibles.

La restricción de acceso es por rol: solo `Secretaría` y `Administración` ven estas secciones, igual que `/usuarios`. El guard `RequireRole` ya existe en `shared/auth/RequireRole.tsx` y se reutiliza sin modificaciones.

## Goals / Non-Goals

**Goals:**

- Pantalla `/roles` funcional: listar, buscar, crear (con herencia opcional) y editar roles — todo sobre datos en memoria.
- Pantalla `/membresia-roles` funcional: listar roles con búsqueda, seleccionar uno y gestionar sus permisos con checkboxes — todo sobre datos en memoria.
- Integración al shell existente (sidebar + router) sin romper otras features.
- Datos mock fieles al esquema que tendrá el backend real para facilitar la integración futura.
- Accesible solo para los roles `Secretaría` y `Administración`.

**Non-Goals:**

- Llamadas HTTP reales al backend.
- Persistencia entre recargas.
- Gestión de permisos a nivel de carrera/materia (solo permiso global por ahora).
- Paginación (el mock tiene pocos registros).
- Eliminar roles (no requerido en este alcance).

## Decisions

### D1 — Dos features separadas, no una sola

**Opción elegida**: Dos features independientes: `frontend/src/features/roles/` y `frontend/src/features/membresia-roles/`. Cada una con su `mockStore.ts`, `routes.tsx`, `pages/` y `components/` propios.

**Alternativa descartada**: Una sola feature `admin-roles` con sub-rutas.

**Por qué**: Sigue la convención del proyecto (features aisladas, una por entidad funcional). La membresía de roles es un concepto distinto al CRUD de roles; mezclarlos en una feature crea acoplamiento innecesario. Cada feature puede evolucionar independientemente.

---

### D2 — Estado compartido vía ConfiguracionContext

**Opción elegida**: El estado de roles y membresías vive en `frontend/src/shared/configuracion/ConfiguracionContext.tsx`. Este context es un layout route de React Router (`<Outlet />`) que envuelve ambas features en el router. Los datos iniciales provienen de los mock stores de cada feature; las mutaciones (`agregarRol`, `editarRol`, `togglePermiso`) viven en el provider.

**Alternativa descartada**: Estado local independiente en cada página (el estado original de cada `useState`).

**Por qué**: Crear un rol en `/roles` debe ser visible inmediatamente en `/membresia-roles` — ambas páginas necesitan leer la misma fuente de verdad. El context provider como layout route es la forma idiomática de React Router para compartir estado entre rutas hermanas sin levantar el estado al root del árbol. Cuando llegue el backend, se reemplaza el `useState` del provider por hooks de React Query que apunten a los endpoints reales.

---

### D3 — Herencia de rol via copia de permisos en creación

**Opción elegida**: Al crear un rol con "rol base" seleccionado, el nuevo rol se inicializa con los mismos permisos que el rol base (copia en el momento de creación). No hay referencia dinámica al rol base post-creación.

**Alternativa descartada**: Herencia dinámica (el rol hijo siempre hereda los permisos del padre en tiempo de consulta).

**Por qué**: La herencia dinámica es compleja de implementar en mock y en el backend real. La copia en creación es más simple, predecible y representa mejor la intención del usuario ("quiero empezar igual que este rol"). Los cambios posteriores en el rol base no afectan al rol hijo, lo cual es el comportamiento esperado.

---

### D4 — Panel split en Membresía Roles

**Opción elegida**: La pantalla `/membresia-roles` usa un layout de dos paneles: izquierdo (lista de roles con buscador integrado dentro de un recuadro) y derecho (permisos del rol seleccionado + botón guardar). El panel derecho muestra un placeholder cuando no hay rol seleccionado.

**Alternativa descartada**: Una tabla de roles clickeable que navega a `/membresia-roles/:id`.

**Por qué**: El flujo de "seleccionar rol → ver permisos → modificar" es fluido en un panel split sin navegación. Evita rutas adicionales y mantiene el contexto visual de qué rol está siendo editado.

### D4b — Toggle inmediato + confirmación visual al guardar

**Opción elegida**: Hacer clic en un permiso lo activa/desactiva inmediatamente en el store. El botón "Guardar cambios" muestra un feedback visual "✓ Cambios guardados" durante 2.5 segundos sin bloquear la interacción.

**Alternativa descartada**: Estado pendiente diferido — los cambios se acumulan localmente y sólo se aplican al confirmar con Guardar.

**Por qué**: La interacción inmediata es más ágil y reduce fricción. El feedback visual del botón Guardar comunica al operador que el cambio quedó registrado sin interrumpir el flujo de trabajo. Con datos mock la distinción entre "aplicado" y "guardado" no tiene impacto técnico real.

---

### D5 — Acciones en modales (igual que usuarios)

**Opción elegida**: Crear y editar roles se hacen en modales sobre la tabla, no en rutas separadas.

**Por qué**: Los formularios son cortos (solo nombre y descripción). Consistente con el patrón establecido en la feature usuarios (D2 de admin-usuarios).

---

### D6 — Componentes de UI desde `@ars-docendi/ui`

**Opción elegida**: Todos los controles usan los componentes del design system: `Field` + `Input` para formularios, `Table.*` para la tabla de roles, `Modal` para modales, `Checkbox` para permisos y el prop `footer` de `Modal` para botones de acción.

**Por qué**: Consistencia con la feature usuarios (D7 de admin-usuarios). Accesibilidad automática y alineación con el design system.

---

### D7 — Búsqueda client-side con useMemo

**Opción elegida**: El buscador de roles (en ambas pantallas) vive en `useState` local de la página y el array filtrado se deriva con `useMemo`. No se persiste en query params.

**Por qué**: Mismo patrón que filtros en usuarios (D5 de admin-usuarios). Instantáneo con datos mock; se migra a query params cuando llegue el backend.

## Risks / Trade-offs

- **Estado efímero**: Al recargar la página, los stores se reinician. → Aceptado; comportamiento esperado en entorno dev con mocks.
- **Stores desconectados**: Los roles del mock de roles no están sincronizados con los `RolSistema` del mock de usuarios. → Cuando se integre el backend, ambos apuntarán al mismo endpoint de identidad.
- **Herencia one-shot**: Un cambio de permisos en el rol base no se propaga a roles derivados. → Documentado y aceptado. Es la semántica correcta para este caso de uso.
