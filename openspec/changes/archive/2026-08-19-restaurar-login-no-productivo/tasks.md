## 1. Cobertura roja de la regresión

- [x] 1.1 Agregar pruebas frontend que fallen para un bundle optimizado con autenticación sembrada habilitada y cubran apertura del selector, sesión, headers y cambio de rol.
- [x] 1.2 Agregar una prueba de integración backend que falle al arrancar Staging con el opt-in habilitado y preserve la ausencia absoluta en Production.

## 2. Capacidad frontend de autenticación sembrada

- [x] 2.1 Centralizar `developmentAuthEnabled` y reemplazar los usos directos de `import.meta.env.DEV` en todo el flujo de autenticación.
- [x] 2.2 Incorporar `VITE_DEVELOPMENT_AUTH_ENABLED` al build Docker con default deshabilitado y documentarlo en la configuración frontend.

## 3. Configuración segura por ambiente

- [x] 3.1 Propagar `DevelopmentAuthentication__Enabled` por Compose y `spin-up.sh` con default deshabilitado.
- [x] 3.2 Configurar staging y previews con frontend/backend habilitados, y producción explícitamente deshabilitada.

## 4. Documentación y verificación

- [x] 4.1 Actualizar la documentación de infraestructura con la matriz local/staging/preview/producción y el smoke check del flujo de ingreso.
- [x] 4.2 Ejecutar tests frontend y backend, lint, build, chequeo de formato y validación OpenSpec estricta.
