## 1. Fase 1 — PoC local: ingreso por mail verificado, sin migraciones

- [x] 1.1 Agregar `Microsoft.AspNetCore.Authentication.OpenIdConnect` y `Microsoft.IdentityModel.Validators` al Host, `UserSecretsId` al `.csproj`, las opciones `AutenticacionMicrosoft` con `Habilitada = false` y `Tenant = common` por defecto en `appsettings.json` (el `ClientId` de desarrollo se carga con `user-secrets`), y quitar la sección `AzureAd` vacía. Verificar que `dotnet build` pasa y que `AutenticacionDesarrolloTests` sigue en verde.
- [x] 1.2 Escribir primero las pruebas de integración de la regla de ingreso por mail: aceptado, mail sin `xms_edov` de cuenta organizacional, cuenta personal por `tid`, mail no registrado, usuario inactivo, usuario sin rol vigente y diferencias de mayúsculas y espacios. Comprobar además que ningún rechazo escribe filas en `identity.users` ni en `audit.change_log`. Verificar que fallan antes de implementar.
- [x] 1.3 Implementar `ServicioIngreso`, `ServicioSesion` y `RepositorioIngreso` en `ArsDocendi.Shared/Identity`. La resolución de rol usa el único rol vigente o el primero por nombre, con `LogWarning` sin datos personales. Verificar que las pruebas de 1.2 pasan, más una nueva para un usuario con dos roles distintos.
- [x] 1.4 En `ArsDocendi.Host/Autenticacion/`, registrar:
  - la cookie `__Host-ars-sesion`;
  - OIDC con authority `common`, validación de issuer multi-tenant, código con PKCE, `SaveTokens = false` y callback `/api/auth/signin-oidc`;
  - el policy scheme junto al esquema de desarrollo, con `UseAuthentication` cuando haya algún esquema;
  - los eventos `OnTokenValidated` (aceptar con un principal mínimo, o redirigir a `/login?error=forbidden` con un log sin datos personales) y `OnRemoteFailure` (`/login?error=auth`).

  Verificar con pruebas de integración que con `Habilitada = false` no existen las rutas `/api/auth/*` y que, con ambos esquemas, las solicitudes con `X-Dev-*` siguen autenticando por el esquema de desarrollo.

- [x] 1.5 Validar la cookie en cada solicitud (`OnValidatePrincipal` → `ServicioSesion`) sin persistir los claims hidratados, y responder `401`/`403` en lugar de redirigir. Verificar con pruebas de integración que un usuario desactivado recibe `401` en la solicitud siguiente y que un cambio de rol o de permisos aplica sin volver a ingresar. Las pruebas emiten la cookie con un sign-in registrado sólo en el host de pruebas.
- [x] 1.6 Agregar los endpoints `EndpointsAutenticacion` con `GET /api/auth/login`, `GET /api/auth/sesion` y `POST /api/auth/logout`. Verificar con pruebas que:
  - un `returnUrl` externo se reemplaza por la ruta inicial;
  - `/sesion` sin cookie responde `401`;
  - con sesión válida devuelve nombre, UPN, rol y permisos;
  - el logout borra la cookie y la siguiente consulta a `/sesion` responde `401`.
- [x] 1.7 Frontend:
  - proxy de `/api` en `vite.config.ts`;
  - `client.ts` con rutas relativas por defecto;
  - `VITE_MICROSOFT_LOGIN_ENABLED` en `frontend/.env.example`, reemplazando a `VITE_AUTH_LOGIN_URL`.

  Verificar que el selector de desarrollo funciona a través del proxy y que `pnpm --filter frontend build` pasa.

- [x] 1.8 Frontend:
  - `useCurrentUser` con el camino de sesión por cookie (React Query sobre `GET /api/auth/sesion`);
  - `RequireAuth` y `LoginPage` basados en ese estado;
  - interceptor de `401`;
  - cierre de sesión contra el backend;
  - invalidación de la sesión en las mutaciones de roles.

  Verificar con Vitest los estados cargando, usuario y `401`, la redirección a `/login`, el botón principal hacia `/api/auth/login?returnUrl=...` y que el acceso secundario al selector sólo aparece con ambos flags.

- [x] 1.9 Documentar la fase 1:
  - `api-contracts.md`: rutas `/api/auth/*`, cookie y `401` sin redirección, reemplazando el párrafo de la "futura integración Azure AD";
  - `stack.md`: backend con cookie en lugar de validación de JWT;
  - `dependency-graph.md`: paquetes del Host;
  - README: cómo correr la PoC con `user-secrets`, `VITE_MICROSOFT_LOGIN_ENABLED` y el proxy.

  Verificar con `pnpm format:check`.

- [x] 1.10 PoC manual con la registración de desarrollo y el secret en `user-secrets`:
  - darse de alta desde el selector como `sys_admin`, con el mail de una cuenta Microsoft personal;
  - ingresar con Microsoft;
  - confirmar el rechazo de una cuenta no registrada y el corte de sesión al desactivar al usuario.

  Registrar en `design.md`, sin datos personales, qué claims llegaron (`email`, `xms_edov`) para una cuenta personal y, si hay una disponible, para una @unlam.edu.ar, y qué pidió el consentimiento. Si `xms_edov` llega para cuentas personales, quitar la excepción por `tid`.

- [x] 1.11 Correr la verificación de la fase: `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check` y `pnpm exec openspec validate --all --strict`.

## 2. Fase 2 — Vínculo por `oid`, vencimientos y anti-falsificación

- [x] 2.1 Escribir primero las pruebas:
  - el primer ingreso vincula `oid` y `tid` y actualiza el último ingreso, con el usuario como actor en `audit.change_log`;
  - el ingreso posterior se reconoce por el vínculo aunque cambien el mail o el UPN;
  - otra cuenta con el mismo mail se rechaza sin modificar el vínculo;
  - las altas de usuarios y de docentes dejan `azure_oid` y `azure_tid` vacíos.

  Verificar que fallan antes de implementar.

- [x] 2.2 Crear `database/identity/013_identity_users_cuenta_microsoft.sql`, su migración EF y el snapshot actualizado: `azure_oid` nullable y vaciado, y `azure_tid` con `CHECK` de "ambos o ninguno". El `Down` repone oids aleatorios antes de restaurar `NOT NULL`. Verificar `--migrate` sobre una base limpia y sobre una con datos, y que la prueba de idempotencia de migraciones sigue en verde.
- [x] 2.3 Actualizar `Usuario`, `IdentityDbContext`, `ServicioUsuarios`, `ServicioDocentes` e `infra/scripts/seed-data/sintetico.sql`, y ocultar `azure_tid` en `ServicioAuditoria`. Retirar `VinculadorPrimerLogin` y su registro en DI, y adaptar `IdentityPersistenciaTests` y `PedidosApiTests`. Verificar `dotnet test` en verde, incluido `SeedSinteticoTests`.
- [x] 2.4 Implementar el vínculo en la regla de ingreso, asignando el principal propio al contexto antes de escribir. Verificar que las pruebas de 2.1 pasan.
- [x] 2.5 Implementar el vencimiento por `MinutosInactividad` (sliding) y por `HorasMaximas` (desde el inicio de sesión). Verificar con pruebas sobre un `TimeProvider` controlado que la sesión vence por inactividad y por duración máxima aunque haya actividad.
- [x] 2.6 Agregar anti-falsificación con `IAntiforgery`, la cookie `XSRF-TOKEN` y el header `X-XSRF-TOKEN` en las mutaciones autenticadas por cookie, sin afectar al esquema de desarrollo. Verificar con pruebas de integración:
  - sin token se rechaza y no hay cambios;
  - con token se procesa;
  - con headers de desarrollo se procesa.

  Verificar también en Vitest que axios envía el header.

- [x] 2.7 Documentar la fase 2: `data-model.md` (`azure_oid` nullable, `azure_tid` y vínculo en el primer ingreso) y `api-contracts.md` (anti-falsificación). Verificar con `pnpm format:check`.
- [x] 2.8 Correr la verificación completa de la fase (los mismos comandos que en 1.11).

## 3. Fase 3 — Staging y producción

- [ ] 3.1 Configurar `ForwardedHeaders` para que confíe sólo en la red interna. Verificar con pruebas de integración que `X-Forwarded-Proto: https` desde la red confiable produce un `redirect_uri` con `https://` y cookies anti-falsificación `Secure`, y que desde una IP no confiable se ignora.
- [ ] 3.2 Persistir Data Protection con `PersistKeysToFileSystem` y `SetApplicationName`, más un volumen nombrado en `compose.base.yml` para producción y staging. Verificar en staging que una sesión sobrevive a reiniciar el backend.
- [ ] 3.3 Agregar el guard de arranque en Production, sin exigirlo en `--migrate`. Verificar con pruebas que un host Production sin configuración falla con un mensaje que no expone secretos y que `--migrate` termina con exit 0.
- [ ] 3.4 Implementar `AdministradorInicial`, aplicado por `--migrate` de forma idempotente a través de la administración de identidad. Verificar con pruebas que:
  - si falta, crea persona, usuario activo y `sys_admin`;
  - si ya existe (incluso desactivado), no lo modifica;
  - sin configuración, no crea nada.
- [ ] 3.5 Cargar variables y secretos:
  - `AutenticacionMicrosoft__*` y `AdministradorInicial__*` en `compose.base.yml` (deshabilitado por defecto), `.env.example` e `infra/compose/.env.example`;
  - `ClientId` como variable y secretos por ambiente en `deploy-staging.yml` y `deploy-prod.yml`;
  - el build arg `VITE_MICROSOFT_LOGIN_ENABLED` en `frontend/Dockerfile` y en los workflows, con `pr-env-deploy.yml` en `false`.

  Verificar con `infra/tests` y con un ambiente `pr-N` que no ofrece Microsoft.

- [ ] 3.6 Fuera del repo, lo hace un integrante con acceso a Microsoft Entra. Crear las registraciones de staging y producción con:
  - redirect `https://staging.<DOMINIO>/api/auth/signin-oidc` y `https://<DOMINIO>/api/auth/signin-oidc`;
  - un secret por registración;
  - los claims `email` y `xms_edov`;
  - sin `User.Read`;
  - `removeUnverifiedEmailClaim = true`.

  Verificar leyendo `authenticationBehaviors` vía Microsoft Graph.

- [ ] 3.7 Documentar la fase 3 en `infrastructure.md`: variables, secretos y sus vencimientos, volumen de claves, headers de proxy, registraciones por ambiente y administrador inicial. Verificar con `pnpm format:check`.
- [ ] 3.8 Validar en staging:
  - ingreso real con una cuenta registrada;
  - rechazo de una no registrada;
  - corte al desactivar;
  - selector como acceso secundario.

  Después, desplegar producción con el administrador inicial y verificar su primer ingreso.
