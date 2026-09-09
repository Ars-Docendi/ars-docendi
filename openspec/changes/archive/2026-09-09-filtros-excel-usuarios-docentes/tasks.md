## 1. Control compartido de encabezado

- [x] 1.1 Crear el control visual de filtro de encabezado en `frontend/src/shared/ui/`, con botón accesible, indicador de filtro activo, apertura/cierre, Escape, clic fuera y retorno del foco; verificarlo con tests de componentes.
- [x] 1.2 Implementar el posicionamiento del menú fuera del contenedor con scroll horizontal y los estilos compactos de la superficie; verificar que el menú no se recorte y que los controles funcionen con teclado en un viewport angosto.
- [x] 1.3 Cubrir el control compartido con tests de accesibilidad: nombre del encabezado, `aria-expanded`, operación con Enter/Espacio y cierre sin disparar el ordenamiento; verificar con `pnpm --filter frontend test:run` sobre los tests del componente.

## 2. Ordenamiento y filtros de Usuarios

- [x] 2.1 Extraer la representación de filtros y orden de Usuarios a helpers puros, conservando normalización de texto, coincidencia parcial, selección múltiple y combinación AND; verificar con tests para Apellido y Nombre, roles, estado, perfil docente y valores vacíos.
- [x] 2.2 Implementar los comparadores de Usuarios para nombre, documento, legajo, UPN/Email y estado, incluyendo legajos numéricos, desempate estable y ciclo ascendente/descendente/sin orden; verificar con tests unitarios de ordenamiento.
- [x] 2.3 Integrar los controles de encabezado en `TablaUsuarios` y conectar el estado controlado con `IndexPage`, manteniendo el conteo de resultados y las acciones de fila; verificar filtros combinados, orden aplicado sólo a filas visibles y `aria-sort`.
- [x] 2.4 Eliminar el uso de `FiltrosUsuarios` y borrar su implementación cuando no tenga otros consumidores; verificar que no queden imports ni referencias y que `TablaUsuarios.test.tsx` cubra el encabezado Acciones sin filtro ni orden.

## 3. Ordenamiento y filtros de Docentes

- [x] 3.1 Extraer la representación de filtros y orden de Docentes a helpers puros, cubriendo nombre, documento, legajo, rol, ámbitos, asignaciones, cuenta y estado; verificar búsqueda por materia/cargo, selección múltiple y combinación AND.
- [x] 3.2 Implementar los comparadores de Docentes para nombre, documento, legajo, cuenta y estado, excluyendo de ordenamiento Rol, Ámbitos, Asignaciones y Acciones; verificar con tests unitarios de ordenamiento y valores vacíos.
- [x] 3.3 Integrar los controles de encabezado en `TablaDocentes` y conectar el estado controlado con `IndexPage`, preservando el ámbito devuelto para Jefe de Cátedra y la vista de solo lectura; verificar filtros, orden, conteo y acciones existentes.
- [x] 3.4 Eliminar el uso de `FiltrosDocentes` y borrar su implementación cuando no tenga otros consumidores; verificar que no queden imports ni referencias y que `TablaDocentes.test.tsx` cubra encabezados sin controles de escritura en solo lectura.

## 4. Documentación y verificación

- [x] 4.1 Actualizar `docs/product/designs/proyecto-docente-design-spec.md` para reemplazar el patrón de barra de filtros de Usuarios y Docentes por el patrón de filtros y ordenamiento por encabezado; verificar que la documentación no describa controles eliminados.
- [x] 4.2 Ejecutar la suite frontend y corregir regresiones: `pnpm --filter frontend test:run`, `pnpm --filter frontend lint` y `pnpm --filter frontend build`.
- [x] 4.3 Ejecutar `pnpm format:check` y `pnpm exec openspec validate --all --strict`; verificar que el cambio quede validado sin modificar contratos, arquitectura ni backend.
