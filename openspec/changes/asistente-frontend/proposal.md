## Why

El asistente tiene endpoint, contrato de respuesta y catálogo de capacidades. No tiene interfaz: hoy nadie del Departamento puede usarlo.

Y hay un detalle que hace este cambio distinto de «agregar una pantalla»: en la barra superior existe un botón **«Ayuda»** `disabled`, con `title="Próximamente"`. Es un fake UI que el invariante #7 del repositorio prohíbe explícitamente, y está ahí desde el app-shell. Activarlo con el asistente **elimina** superficie falsa en lugar de agregar superficie nueva.

## What Changes

- **`features/asistente/`, con dos montajes de la misma vista**: la ruta `/asistente` y un lanzador global en la barra superior. Una ruta a la que hay que navegar no resuelve el descubrimiento: si el usuario tiene que acordarse de que el asistente existe y buscar dónde está, no lo usa.
- **El acceso se decide preguntándole al backend, no mirando el rol.** El frontend consulta `GET /api/asistente/capacidades`: si responde, hay acceso; si responde `403`, no. Una lista de roles en el frontend falla **abierta** con cualquier rol que no conozca, y `identity.roles` no es un catálogo cerrado.
- **Los cuatro estados se renderizan distinguibles**, y el degradado como **estado** y no como error: el asistente se degrada, no se cae.
- **Las columnas sensibles se renderizan como tabla**, con los valores reales que vienen en `filas`. Con columnas sensibles la narración deja de ser el vehículo del dato: el modelo redacta el marco y la interfaz muestra la tabla.
- **Región viva sólo sobre los mensajes**, con `role="log"`, y la línea de métricas **fuera**. El defecto a no repetir está verificado en el prototipo: la región envolvía el contenedor entero, así que cada re-render hacía que el lector de pantalla leyera todo de nuevo, métricas incluidas.
- **Indicador de proceso con umbral de aparición**, anunciado con `role="status"`. Los tres carriles tienen latencias muy distintas: mostrar «procesando» durante los milisegundos de una respuesta determinista parpadea, y es peor que no mostrar nada.
- **Ninguna etapa simulada.** No se inventan pasos: etapas reales exigirían streaming del servidor, que cambia el contrato. Un progreso por temporizador sería fake UI.

## Capabilities

### New Capabilities

- `asistente-superficie-frontend`: la feature, sus dos montajes, el gate por permiso real y el renderizado de los cuatro estados.
- `asistente-accesibilidad`: la región viva acotada a los mensajes, el rol de registro, el anuncio de proceso con umbral y la gestión de foco.

## Impact

- `frontend/src/features/asistente/` — la feature entera.
- `frontend/src/app/shell/TopBar.tsx` — el botón «Ayuda» deja de estar deshabilitado.
- `frontend/src/app/router.tsx` — la ruta `/asistente`.
- `frontend/src/app/AppLayout.tsx` — el montaje del panel lanzado desde la barra.
- Tests de la feature con Vitest y Testing Library.
- `docs/product/designs/` y `docs/architecture/domains/asistente.md`.

## Rollback

Aditivo salvo el botón «Ayuda», que vuelve a `disabled` quitando una línea. Sin la feature montada, la aplicación queda exactamente como estaba — con su fake UI de vuelta.
