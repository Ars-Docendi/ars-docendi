## Context

Ver `proposal.md` para la motivación. El frontend usa `import.meta.env.DEV` en cinco puntos de la autenticación sembrada. Ese indicador describe el modo de compilación de Vite, no el ambiente donde se desplegará el artefacto: todas las imágenes Docker lo fijan en `false`. A la vez, `spin-up.sh` materializa staging y previews con `ASPNETCORE_ENVIRONMENT=Production` y no propaga `DevelopmentAuthentication__Enabled`, aunque esos ambientes sí reciben el dataset sintético.

La solución debe permitir pruebas integrales en ambientes descartables sin debilitar la garantía de que producción no registra ni consume la suplantación.

## Goals / Non-Goals

**Goals:**

- Separar el modo de build de Vite de la capacidad explícita de autenticación sembrada.
- Hacer coincidir la configuración del frontend y el backend en local, staging y previews.
- Mantener defaults cerrados y dos guardas independientes en producción.
- Detectar mediante pruebas cualquier rama en la que el botón visible no tenga destino.

**Non-Goals:**

- Integrar Azure AD/MSAL o hacer funcional el login productivo.
- Cambiar el endpoint, los DTOs, la validación de identidades o el modelo de datos.
- Habilitar datos sintéticos o suplantación en producción.

## Decisions

### D1 — Capacidad frontend centralizada y explícita

Un módulo compartido expondrá `developmentAuthEnabled`, verdadero cuando Vite ejecuta su servidor de desarrollo o cuando `VITE_DEVELOPMENT_AUTH_ENABLED` es exactamente `true`. Login, sesión, interceptor Axios, resolución del usuario y cambio de rol dependerán de esa única decisión.

La imagen frontend declarará un `ARG`/`ENV` de build con default `false`. Los workflows de staging y previews pasarán `true`; producción pasará `false` explícitamente. Esto conserva tree-shaking para producción y evita inferir seguridad desde nombres de host o ramas.

**Alternativa descartada:** usar `import.meta.env.MODE=staging`; las imágenes actuales se construyen una sola vez con Vite en modo production y mezclar modo de optimización con ambiente reproduce el acoplamiento que causó el bug.

### D2 — Opt-in backend propagado por infraestructura

Compose propagará `DevelopmentAuthentication__Enabled`, con default `false`, y `spin-up.sh` la incluirá en su archivo de ambiente efímero. Los workflows no productivos fijarán `ASPNETCORE_ENVIRONMENT=Staging` y la opción en `true`; producción conservará `Production` y `false`.

El Host mantendrá su condición actual `!IsProduction() && Enabled`. Así, cualquiera de las dos guardas deshabilita la superficie y una opción accidentalmente verdadera no alcanza para habilitarla en producción.

**Alternativa descartada:** derivar automáticamente el opt-in de `AMBIENTE != prod`; una variable explícita hace auditable la habilitación y conserva el fail-closed del diseño original.

### D3 — Red-green en el límite de configuración

Las pruebas frontend cubrirán la tabla de decisión del flag y el comportamiento observable del botón en un bundle no-DEV habilitado. Las pruebas de integración backend cubrirán staging habilitado y conservarán la prueba de ausencia en Production. Los smoke checks validarán selector, catálogo y sesión en el artefacto desplegado.

**Alternativa descartada:** probar sólo el helper booleano; no detectaría que un consumidor siga usando directamente `import.meta.env.DEV`.

## Risks / Trade-offs

- **[Desalineación entre flags frontend/backend]** → Los workflows fijan ambos valores juntos y el smoke check prueba el flujo completo.
- **[Suplantación incluida por error en producción]** → Defaults `false`, build de producción explícito, guarda `IsProduction` independiente y pruebas negativas frontend/backend.
- **[Configuración horneada en el bundle]** → Es deliberado porque Vite reemplaza variables en build; cada ambiente ya produce su imagen por separado.

## Migration Plan

1. Incorporar primero pruebas fallidas para bundle optimizado habilitado y backend Staging habilitado.
2. Centralizar la capacidad frontend y propagar las variables en Docker, Compose y `spin-up.sh`.
3. Configurar workflows de staging/previews y dejar producción explícitamente deshabilitada.
4. Desplegar en preview, validar selector, catálogo, selección, headers y cambio de rol; luego desplegar staging.
5. Para rollback, retirar los argumentos/variables o revertir la imagen; no hay migraciones ni datos que restaurar.
