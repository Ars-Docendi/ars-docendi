## 1. Backend

- [x] 1.1 Llevar el avance a 100 al pasar a Resuelta y registrarlo en el historial; verificar con `TareasHttpTests`
- [x] 1.2 Recalcular el promedio de hijas no canceladas subiendo por los ancestros y rechazar con 422 la edición manual; verificar con `AvanceTareasHttpTests`
- [x] 1.3 Exponer `avanceCalculado` en `TareaDto`; verificar con `AvanceTareasHttpTests`

## 2. Frontend

- [x] 2.1 Ocultar la edición de avance si `avanceCalculado` y aclararlo en el Detalle; verificar con `maquinaEstadosTarea.test.ts`
- [x] 2.2 Mostrar "Vencido" sólo con la fecha de fin vencida y el semáforo después de la fecha; verificar con `semaforoTarea.test.ts` y `DetalleTareaPage.test.tsx`

## 3. Documentación

- [x] 3.1 Actualizar `api-contracts.md`, `domains/tareas.md` y los artículos del Centro de ayuda; verificar con `pnpm format:check` e `integridad.test.ts`
