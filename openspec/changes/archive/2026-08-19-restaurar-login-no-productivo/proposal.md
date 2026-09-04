## Why

El cambio `datos-ejemplo-y-frontend-con-api` condicionó la autenticación sembrada a `import.meta.env.DEV`, pero las imágenes Docker de staging y previews se compilan como bundles optimizados con ese valor en `false`. Como tampoco existe aún una URL SSO configurada, el botón de ingreso queda sin ninguna acción y esos ambientes no pueden validarse de punta a punta.

## What Changes

- Habilitar la sesión basada en identidades sembradas en staging y ambientes `pr-N` mediante opt-in explícito de frontend y backend, aunque el frontend se compile en modo optimizado.
- Mantener la superficie de suplantación ausente en producción mediante defaults deshabilitados y la doble guarda de ambiente y configuración del backend.
- Centralizar en frontend la decisión de disponibilidad de autenticación de desarrollo para que login, sesión, headers, usuario actual y cambio de rol usen el mismo criterio.
- Incorporar cobertura automatizada y smoke checks de despliegue que detecten un botón de ingreso sin destino.
- Documentar las variables de build/runtime y el comportamiento por ambiente.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `sesion-desarrollo-sembrada`: Extender el selector de identidades sembradas a despliegues no productivos habilitados explícitamente, independientemente del modo de compilación de Vite.

## Impact

- **Frontend:** autenticación compartida, imagen Docker y configuración Vite de staging/previews.
- **Backend/infraestructura:** composición de `ArsDocendi.Host`, Compose, `spin-up.sh` y workflows de despliegue no productivo; no cambian endpoints ni DTOs.
- **Consumidores cross-module:** ninguno; la superficie sigue siendo transversal y exclusiva del Host/Shared.
- **Grafo de dependencias:** sin cambios ni ciclos nuevos.
- **Producción:** conserva `ASPNETCORE_ENVIRONMENT=Production`, el opt-in deshabilitado y el selector fuera del bundle.
- **Rollback:** revertir los argumentos/variables de despliegue vuelve a deshabilitar la autenticación sembrada sin migraciones ni cambios de datos.
- **Normativa:** no introduce ni modifica reglas institucionales.
