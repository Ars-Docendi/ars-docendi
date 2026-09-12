## 1. Pruebas de regresión

- [x] 1.1 Agregar una prueba de `TablaUsuarios` con membresías que verifique que no se renderizan el encabezado ni las celdas `Ámbitos`, y que la prueba falle antes del ajuste.
- [x] 1.2 Agregar una prueba de `ModalEditarUsuario` que verifique que "Fecha de nacimiento" usa un input `type="date"`, conserva el valor inicial y no contiene `.cal-ico`; comprobar que falle antes del ajuste.

## 2. Ajustes de Usuarios

- [x] 2.1 Quitar de `TablaUsuarios` la columna `Ámbitos`, sus celdas y el helper de resumen sin modificar `membresias`, el editor ni los payloads; verificar las pruebas de tabla y de adaptadores existentes.
- [x] 2.2 Reemplazar únicamente en el modal de edición el `DatePicker` por el `Input` nativo de fecha, manteniendo la validación obligatoria, el formato `YYYY-MM-DD` y el guardado; verificar la prueba del modal.

## 3. Ajustes de Roles y documentación UX

- [x] 3.1 Alinear `roles.css` con la tipografía y los tamaños del design system usados por Usuarios y Docentes, limitando los selectores a los paneles de Roles; verificar que buscador, lista, permisos y controles usen los tokens compartidos y conserven el responsive.
- [x] 3.2 Actualizar `docs/product/designs/administracion-usuarios-docentes-design-spec.md` y `docs/product/designs/administracion-roles-permisos-design-spec.md` con la ausencia de `Ámbitos`, el control nativo de fecha y la tipografía común; verificar que no contradigan las specs delta.

## 4. Verificación final

- [x] 4.1 Ejecutar `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build` y `pnpm format:check`; corregir sólo regresiones introducidas por este change.
- [x] 4.2 Ejecutar `pnpm exec openspec validate --all --strict` y verificar que todos los artefactos del change queden válidos y listos para aplicar.
