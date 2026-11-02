## Why

El ingreso a Ars Docendi hoy es simulado: sólo existe el selector de identidades sembradas de los ambientes no productivos y producción no registra ningún esquema de autenticación. Para usarse en la universidad, el sistema necesita un ingreso real con cuentas Microsoft que deje pasar únicamente a las personas que la administración dio de alta, sin manejar contraseñas propias.

## What Changes

- Agregar "Ingresar con Microsoft" para cuentas personales y de cualquier organización (registración multi-tenant con cuentas personales), resuelto por el backend como cliente confidencial con código de autorización y PKCE. Los tokens de Microsoft no llegan al navegador.
- Emitir una sesión propia por cookie (`HttpOnly`, `Secure`, `SameSite=Lax`) que sólo identifica al usuario. En cada solicitud el backend revalida que el usuario siga activo y resuelve su rol y permisos desde `identity`.
- El ingreso **no crea usuarios ni personas**: acepta sólo si el mail verificado por Microsoft coincide con el UPN de un usuario existente, activo y con un rol vigente. Los rechazos no escriben en la base y quedan en el log sin datos personales.
- La sesión toma el rol asignado al usuario. Si tuviera más de un rol distinto, se toma el primero por nombre y se registra una advertencia, hasta que exista una pantalla para elegirlo.
- Vincular la cuenta Microsoft (`oid` y tenant) en el primer ingreso y reconocer los ingresos siguientes por ese vínculo, no por el mail. `identity.users.azure_oid` pasa a admitir NULL hasta ese primer ingreso, se agrega `azure_tid` y las altas dejan de generar un oid aleatorio.
- Exponer `/api/auth/login`, `/api/auth/logout`, `/api/auth/sesion` y el callback `/api/auth/signin-oidc` bajo `/api`, la ruta que Traefik ya publica hacia el backend.
- Frontend: el botón de ingreso deriva al backend, la sesión se obtiene de `/api/auth/sesion`, un `401` lleva a `/login` y el cierre de sesión pasa por el backend. Vite proxea `/api` para que el desarrollo local sea del mismo origen, como los despliegues.
- Habilitación por ambiente: producción exige el ingreso con Microsoft y nunca el selector; staging ofrece ambos; los `pr-N` siguen sólo con el selector, porque Microsoft no admite redirect URIs comodín para este tipo de registración; en local el ingreso con Microsoft es opcional.
- Cuando ambos están habilitados, el selector de identidades sembradas pasa a ser un acceso secundario de la pantalla de ingreso.
- Despliegue: headers de proxy confiables, claves de Data Protection persistidas por ambiente, secretos por ambiente y un guard de arranque en producción.
- Retirar `VinculadorPrimerLogin`, que vinculaba por documento, un dato que Microsoft no entrega.

La entrega se ordena en fases para validar una PoC local antes de completar: (1) PoC local que reconoce por mail verificado, sin migraciones; (2) vínculo por `oid` y endurecimiento de la sesión; (3) staging y producción.

## Capabilities

### New Capabilities

- `inicio-sesion-microsoft`: ingreso con cuentas Microsoft vía backend y cookie, verificación contra usuarios dados de alta, sesión propia revalidada por solicitud, rol de la sesión, rechazos sin persistencia y habilitación por ambiente.

### Modified Capabilities

- `persistencia-identity`: el primer ingreso vincula la cuenta Microsoft (`oid` y tenant) a un usuario existente sin crear usuarios ni personas, y `azure_oid` queda vacío hasta ese momento.
- `sesion-desarrollo-sembrada`: el selector convive con el ingreso de Microsoft; cuando ambos están habilitados deja de ser la acción del botón principal y se ofrece como acceso secundario no productivo.

## Impact

- **Backend (`ArsDocendi.Host`):** esquemas de cookie y OpenID Connect, selección de esquema junto a la autenticación de desarrollo, endpoints `/api/auth/*`, validación de sesión por solicitud, headers de proxy y guard de producción.
- **Identity (`ArsDocendi.Shared`):** servicio de ingreso que reemplaza a `VinculadorPrimerLogin`, consulta de la identidad de sesión, migración SQL versionada sobre `identity.users`; `ServicioUsuarios` y `ServicioDocentes` dejan de inventar `azure_oid`; la auditoría oculta `azure_tid` igual que `azure_oid`.
- **Frontend:** `shared/auth` (`LoginPage`, `useCurrentUser`, `RequireAuth`, cierre de sesión), `shared/api/client.ts`, `vite.config.ts` y `.env.example`.
- **API:** endpoints nuevos de autenticación. `ICurrentUser`, `IConsultasIdentity` y los contratos de negocio no cambian; los módulos siguen recibiendo los mismos claims.
- **Documentación:** `api-contracts.md`, `stack.md` (deja de describir validación de JWT), `data-model.md`, `infrastructure.md`, `dependency-graph.md` y README.
- **Dependencias:** `Microsoft.AspNetCore.Authentication.OpenIdConnect` y `Microsoft.IdentityModel.Validators` en el Host. Sin cambios en el grafo de proyectos ni edges entre módulos.
- **Infra:** variables `AutenticacionMicrosoft__*` en Compose y en los workflows de staging y producción, secretos por ambiente y volumen para las claves de Data Protection. La registración de la app en Microsoft Entra vive fuera del repo: hoy en la cuenta personal de un integrante, reemplazable por una de UNLaM cambiando sólo configuración.
- **Normativa:** no toca reglas institucionales; no hay BR nuevas.
- **Rollback:** la fase 1 se desactiva con `AutenticacionMicrosoft:Habilitada=false` y vuelve al comportamiento actual. La migración de la fase 2 es aditiva; su `Down` repone oids aleatorios antes de restaurar `NOT NULL`. En la fase 3, volver a la imagen anterior deja producción como hoy, sin ingreso.
