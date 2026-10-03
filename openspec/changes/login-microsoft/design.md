## Context

La motivación y el alcance están en `proposal.md`; el comportamiento, en los specs delta de este cambio.

Estado actual que condiciona el diseño:

- El Host registra un único esquema, `IdentidadDesarrollo`, que lee `X-Dev-User-Id`/`X-Dev-Role-Code` sólo fuera de Production y con opt-in. Emite `NameIdentifier`, `Name`, `Email` (UPN), `Role` y un claim por permiso; `ICurrentUser`, las políticas por permiso y `ResolutorActor` consumen esos claims. En Production no hay ningún esquema registrado.
- `identity.users.azure_oid` es `NOT NULL UNIQUE`. `ServicioUsuarios`, `ServicioDocentes` y el seed sintético lo completan con valores inventados. `VinculadorPrimerLogin` vincula por documento y sólo lo usan pruebas.
- Los despliegues entran por Cloudflare Tunnel → Traefik → contenedores en HTTP, y sólo `/api` llega al backend. Producción vive en `https://<DOMINIO>`, staging en `staging.<DOMINIO>` y cada PR en `pr-N.<DOMINIO>`: para el navegador son el mismo _site_.
- En local, Vite (`:5173`) y la API (`:5000`) son orígenes distintos, y el cliente axios apunta directo a `localhost:5000`.
- Ya existe la registración "Ars Docendi (desarrollo)" en Microsoft Entra: cuentas de cualquier organización y personales, redirect web `http://localhost/api/auth/signin-oidc` (en localhost Microsoft ignora el puerto), un secret y los claims opcionales `email` y `xms_edov`.

## Goals / Non-Goals

**Goals:**

- Ingreso real sin cambiar `ICurrentUser`, `IConsultasIdentity` ni los contratos de los módulos.
- Una PoC local verificable con la registración de desarrollo antes de tocar datos.
- El mismo código para cualquier registración (la personal hoy, una de UNLaM mañana): el reemplazo es sólo de configuración.
- El grafo de proyectos no cambia: todo vive en el Host y en `ArsDocendi.Shared/Identity`, sin módulos ni edges nuevos.

**Non-Goals:**

- Elegir el rol al ingresar u operar con varios roles a la vez.
- Unificar el selector de desarrollo con la cookie: sigue autenticando por headers.
- Ingreso con Microsoft en los ambientes `pr-N`.
- Cerrar la sesión de Microsoft (single sign-out) o front-channel logout.
- Cambiar la pantalla de acceso denegado del login.
- Llamar a Microsoft Graph u otras APIs, o guardar tokens de Microsoft.
- Revocar del lado del servidor una sesión individual.

## Decisions

### 1. Backend como cliente confidencial y cookie propia

El backend hace el intercambio con Microsoft y emite su propia cookie de sesión.

- **Por qué:** frontend y API comparten origen, no se llama a APIs de Microsoft, los tokens nunca llegan al navegador (es el patrón que la guía del IETF para apps de navegador marca como más seguro) y el tiempo de sesión y la desactivación quedan bajo control propio. Además, aguas abajo se emiten los mismos claims que el esquema de desarrollo.
- **Alternativa descartada:** MSAL en el frontend y JWT bearer hacia la API. Deja tokens en el navegador, ata la sesión a los tokens de Microsoft y obliga a mantener dos caminos de autenticación distintos.

### 2. Handler OpenID Connect estándar con validación de issuer multi-tenant

- `Microsoft.AspNetCore.Authentication.OpenIdConnect` con authority `https://login.microsoftonline.com/{Tenant}/v2.0` y `Tenant = common` por defecto. Poner el id de una organización en `Tenant` restringe el ingreso a ella sin tocar código.
- Con `common`, cada tenant firma con su propio issuer: se valida con `AadIssuerValidator` y la validación del issuer de la clave de firma de `Microsoft.IdentityModel.Validators`, de la misma familia de paquetes que el handler.
- `response_type=code` con PKCE y client secret, scopes `openid profile email` y `SaveTokens = false`.
- **Alternativa:** `Microsoft.Identity.Web`. Resuelve lo mismo con más superficie (caché de tokens, APIs downstream) que no usamos. Queda como plan B si el validador no encaja.

### 3. Configuración y registraciones

- La sección `AutenticacionMicrosoft` tiene `Habilitada`, `Tenant`, `ClientId`, `ClientSecret`, `MinutosInactividad` (60 por defecto) y `HorasMaximas` (10 por defecto). Los datos del administrador inicial van en la sección `AdministradorInicial` (decisión 12).
- `ClientId` no es secreto, pero `appsettings.Development.json` está ignorado por git: en local, `ClientId`, `ClientSecret` y `Habilitada` se cargan con `dotnet user-secrets` (el README trae los comandos). En los despliegues, `ClientId` va como variable y `ClientSecret` en GitHub Secrets por ambiente; el secret nunca va en el repo ni en imágenes.
- Una registración por ambiente (desarrollo, staging y producción), con secretos independientes y margen de rotación: las registraciones con cuentas personales admiten sólo dos secretos. La de producción puede terminar siendo la de UNLaM.
- Se elimina la sección `AzureAd` vacía de `appsettings.json`.

### 4. Rutas bajo `/api/auth`

| Ruta                                | Uso                                                                |
| ----------------------------------- | ------------------------------------------------------------------ |
| `GET /api/auth/login?returnUrl=...` | Anónima; descarta `returnUrl` no locales y desafía al esquema OIDC |
| `/api/auth/signin-oidc`             | Callback del handler OIDC (no es un endpoint de controller)        |
| `GET /api/auth/sesion`              | Sesión vigente (nombre, UPN, rol, permisos) o `401`                |
| `POST /api/auth/logout`             | Borra la cookie y responde `204`                                   |

Bajo `/api` pasan por Traefik y por el proxy de Vite sin reglas nuevas. Son endpoints mínimos del Host (`EndpointsAutenticacion`, como los de desarrollo) que se mapean sólo con el ingreso habilitado, así que con el ingreso apagado no existen.

### 5. Regla de ingreso en `ArsDocendi.Shared/Identity`

```
token validado (tid, oid, email, xms_edov)
  |
  +-- [fase 2] usuario con (azure_tid, azure_oid)? --- si ------------------+
  |                                                    no                   |
  +-- mail verificado? --------------------------- no --> MailNoVerificado  |
  |   (xms_edov = true, o tid de cuentas personales)                        |
  +-- usuario con upn = mail normalizado? -------- no --> NoRegistrado      |
  +-- [fase 2] ya vinculado a otra cuenta? ------- si --> CuentaDistinta    |
  |                                                                         |
  +<------------------------------------------------------------------------+
  +-- activo? ------------------------------------ no --> Inactivo
  +-- rol vigente? ------------------------------- no --> SinRol
  +-- [fase 2] vincular si hacía falta + último ingreso
  v
aceptado(usuarioId) --> cookie con usuarioId e inicio de sesión
```

- **Rechazos:** `OnTokenValidated` corta el flujo (`HandleResponse`), redirige a `/login?error=forbidden` y registra un log con el motivo y el tipo de cuenta (personal u organizacional), sin datos personales. Los errores remotos (`OnRemoteFailure`) redirigen a `/login?error=auth`.
- **Cuentas personales:** `xms_edov` documenta que los mails de cuentas Microsoft personales cuentan como verificados, pero no está garantizado que el claim se emita para ellas. Por eso se aceptan también por `tid` de cuentas personales (`9188040d-6c67-4c5b-b112-36a304b66dad`). La PoC lo confirma; si el claim llega, se quita esa excepción.
- **Fases:** la fase 1 no vincula ni escribe: reconoce siempre por mail. La fase 2 agrega el vínculo y el último ingreso. Antes de esa escritura se asigna el principal propio al contexto, para que la auditoría registre al usuario como actor.
- **Capas:** los endpoints y los eventos del Host llaman a `ServicioIngreso` y `ServicioSesion`, que usan `RepositorioIngreso` sobre `IdentityDbContext`. Sólo se escribe `identity.users` (nunca personas, roles ni permisos), igual que hacía el vinculador.
- **Retiro de `VinculadorPrimerLogin`:** se elimina junto con su registro en DI. Las pruebas que lo usan para preparar datos pasan a insertar usuarios directamente.

### 6. Sesión por cookie revalidada en cada solicitud

- **Cookie:** `__Host-ars-sesion`, con `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/` y sin `Domain`, así que no viaja a otros subdominios. Sólo contiene `NameIdentifier` y el inicio de la sesión.
- **Validación:** `OnValidatePrincipal` consulta identity en cada solicitud (activo, rol y permisos) y reemplaza el principal por los mismos claims que hoy emite el handler de desarrollo; esos claims no se persisten en la cookie. Si el usuario dejó de estar activo, se quedó sin rol o se superó la duración máxima, rechaza el principal y borra la cookie.
- **Vencimiento:** la inactividad usa `SlidingExpiration` con `ExpireTimeSpan = MinutosInactividad`; la duración máxima compara el inicio guardado contra `HorasMaximas`.
- **Sin redirecciones en la API:** `OnRedirectToLogin` responde `401` y `OnRedirectToAccessDenied` responde `403`.
- **Rol:** se usa el único rol vigente. Si hay varios distintos, el primero por `Rol.Nombre` (orden ordinal), con un `LogWarning` que registra el `UsuarioId` y la cantidad de roles.
- **Costo:** una consulta por solicitud, igual que el handler de desarrollo. No se cachea porque la desactivación tiene que aplicar en la solicitud siguiente.

### 7. Convivencia con el esquema de desarrollo

- **Esquema por defecto:** un policy scheme. Si la solicitud trae `X-Dev-User-Id` y el esquema de desarrollo está registrado, delega en él; si no, en la cookie. El challenge siempre va por la cookie y termina en `401`.
- `UseAuthentication()` se llama siempre que haya al menos un esquema registrado.
- **Production:** sólo existen la cookie y OIDC.

### 8. Frontend

- **Proxy local:** `vite.config.ts` redirige `/api` a `http://localhost:5000` conservando el `Host`, para que el backend arme el redirect con el origen de Vite. `client.ts` usa rutas relativas por defecto y `VITE_API_URL` deja de hacer falta en local.
- **Flag de build:** `VITE_MICROSOFT_LOGIN_ENABLED`, simétrico a `VITE_DEVELOPMENT_AUTH_ENABLED` (las imágenes ya se construyen por ambiente). Reemplaza a `VITE_AUTH_LOGIN_URL`, que sólo leía `LoginPage`.
- **`LoginPage`:** con Microsoft habilitado, el botón principal navega a `/api/auth/login?returnUrl=<destino>`. Si además hay selector, se muestra un acceso secundario con el `Button` existente. Es un control sólo no productivo y no hay design spec del login, así que no requiere diseño en Pencil.
- **`useCurrentUser`:** si hay una selección de desarrollo, sigue el camino actual. Si no, consulta `GET /api/auth/sesion` con React Query, y un `401` significa que no hay usuario. `RequireAuth` y `LoginPage` usan ese estado (cargando, usuario o sin usuario) en lugar del `isAuthenticated()` síncrono.
- **Interceptor de axios:** un `401` que no venga de la consulta de sesión limpia esa consulta y navega a `/login`.
- **Cerrar sesión:** llama a `POST /api/auth/logout` y limpia la selección de desarrollo.
- **Cambios de roles:** las mutaciones de roles que hoy invalidan el catálogo de desarrollo también invalidan la consulta de sesión.

### 9. Vínculo por `oid` (fase 2)

- **Migración:** SQL versionado `database/identity/013_identity_users_cuenta_microsoft.sql` más su migración EF.
  - `azure_oid` pasa a admitir NULL y se vacía en todas las filas: nunca hubo un ingreso real, así que todos los valores actuales son inventados.
  - Se agrega `azure_tid UUID NULL` con un `CHECK` de "ambos o ninguno".
  - La unicidad de `azure_oid` se mantiene: los NULL no colisionan y el oid es un GUID.
  - `Down` repone `gen_random_uuid()` antes de restaurar `NOT NULL` y quita `azure_tid`.
- **Código y datos:** `Usuario.AzureOid` pasa a `Guid?` y se agrega `AzureTid`. `ServicioUsuarios`, `ServicioDocentes` e `infra/scripts/seed-data/sintetico.sql` dejan de inventarlos, y `ServicioAuditoria` oculta `azure_tid` igual que `azure_oid`.

### 10. Anti-falsificación (fase 2)

- **Mecanismo:** `IAntiforgery` con una cookie `XSRF-TOKEN` legible por JS y el header `X-XSRF-TOKEN`, que axios ya envía en solicitudes al mismo origen.
- **Alcance:** se valida en `POST`, `PUT`, `PATCH` y `DELETE` autenticados por cookie. El esquema de desarrollo autentica por headers, no es vulnerable a CSRF y queda afuera.
- **Por qué no alcanza `SameSite`:** `pr-N`, staging y producción comparten _site_, y cada `pr-N` ejecuta el código de una rama. Desde un sitio hermano se pueden disparar formularios `POST` sin cuerpo que llegan con la cookie de producción. `__Host-` evita que la cookie viaje a los subdominios, pero no que se envíe en solicitudes que salen de ellos.

### 11. Despliegue (fase 3)

- **Headers de proxy:** `ForwardedHeaders` acepta `X-Forwarded-Proto` y `X-Forwarded-For` sólo desde la red interna del Compose. Sin esto el backend arma la URL de retorno con `http://` y Microsoft responde AADSTS50011.
- **Data Protection:** `PersistKeysToFileSystem` sobre un volumen nombrado por proyecto Compose, con `SetApplicationName` fijo, en producción y staging. Los `pr-N` no emiten cookies de sesión y no lo necesitan.
- **Variables:** `AutenticacionMicrosoft__*` se agrega a `compose.base.yml` deshabilitado por defecto. Los workflows de staging y producción pasan `ClientId` como variable y `ClientSecret` como secreto por ambiente. El build del frontend recibe `VITE_MICROSOFT_LOGIN_ENABLED`: `true` en staging y producción, `false` en `pr-N`.
- **Guard:** en Production, si `Habilitada` está apagado o faltan `ClientId` o `ClientSecret`, el host web no arranca. El modo `--migrate` no lo exige.
- **Registraciones:** en las de staging y producción se fija `removeUnverifiedEmailClaim = true` vía Microsoft Graph. Hoy es el default para apps multi-tenant nuevas; se explicita. Se documenta en `infrastructure.md` junto con el vencimiento de cada secreto.

### 12. Administrador inicial (fase 3)

- **Mecanismo:** `AdministradorInicial` (`Upn`, `Nombre`, `Apellido`, `Documento`) se declara como secreto del despliegue. El modo `--migrate`, después de aplicar las migraciones, crea la persona, el usuario activo y el rol `sys_admin` sólo si no existe un usuario con ese UPN. Nunca modifica uno existente y lo hace a través de la superficie de administración de identidad.
- **El resto de los administradores** se da de alta desde la pantalla de usuarios, que ya valida los datos y deja en auditoría quién dio de alta a quién.
- **Alternativa descartada:** un SQL versionado con los datos. Deja datos personales (incluido el DNI) en el historial de git aunque el repo sea privado, y los replicaría en staging y en cada `pr-N`.

## Resultado de la PoC (fase 1)

Probado en local con la registración de desarrollo (2026-10-02):

- **Cuenta personal de Microsoft:** sin alta, rechazada como `NoRegistrado`; dada de alta, ingresa. No se registró si llegó `xms_edov`, así que se mantiene la excepción por `tid` de cuentas personales (Microsoft verifica su mail al crearlas).
- **Cuenta @unlam.edu.ar:** dada de alta, ingresa. UNLaM no exige aprobación de un administrador para la app, y el mail llega con `xms_edov = true`; si no, la regla la habría rechazado.
- **Cierre de sesión y desactivación con la sesión abierta:** el cierre vuelve al login; al desactivar al usuario, la solicitud siguiente lo devuelve al login.

## Risks / Trade-offs

- [Algunos Safari no envían cookies `Secure` en `http://localhost`, y el callback usa cookies de correlación `SameSite=None; Secure`] → Probar la PoC en Chrome, Edge o Firefox; si hace falta, servir Vite por HTTPS.
- [Organizaciones que exigen aprobación de su admin para apps de publicadores no verificados] → UNLaM no lo exige hoy (PoC). Si cambia su política, se resuelve con consentimiento de administrador o con la registración institucional.
- [Cuentas de organización sin buzón, que no traen `email`] → Se rechazan como mail no verificado. La cuenta UNLaM probada sí lo trae.
- [`xms_edov` ausente para cuentas personales] → Se mantiene la excepción por `tid` de cuentas personales (ver Resultado de la PoC).
- [Usuarios con varios roles pierden los permisos del resto] → Advertencia en el log; la pantalla de elección queda para un cambio posterior.
- [Pérdida de las claves de Data Protection] → Vencen todas las sesiones y hay que volver a ingresar, sin pérdida de datos.
- [Vencimiento del client secret] → Fecha registrada en `infrastructure.md` y rotación con el segundo secreto de la registración.
- [Una cookie robada sigue siendo válida hasta vencer] → `HttpOnly`, `Secure` y duración acotada; desactivar al usuario la invalida en la solicitud siguiente.

## Migration Plan

1. **Fase 1 (PoC local):** se integra con `Habilitada = false` en todos los ambientes desplegados. En local se habilita con `dotnet user-secrets` (`ClientId`, `ClientSecret` y `Habilitada`). Sin migraciones.
2. **Fase 2:** la migración `013` la aplica `--migrate` en el deploy. Backend y frontend se despliegan juntos por la anti-falsificación.
3. **Fase 3:** crear las registraciones de staging y producción, cargar secretos y variables, desplegar staging y validar el ingreso real. Recién entonces producción, con el administrador inicial.
4. **Rollback:** ver `proposal.md`, sección Impact.

## Open Questions

- Valores definitivos de inactividad y duración máxima (son configurables; por defecto 60 minutos y 10 horas).
- Si la registración de producción será la de UNLaM (sólo cambia la configuración).
