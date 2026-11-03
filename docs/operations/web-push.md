# Notificaciones push (Web Push)

Las notificaciones push usan Web Push (RFC 8030) con identificación VAPID (RFC 8292) y cifrado de contenido de punta a punta (RFC 8291): Google, Mozilla, Apple y Microsoft sólo transportan texto cifrado. El diseño está en [domains/notificaciones.md](../architecture/domains/notificaciones.md) y el contrato HTTP en [api-contracts.md](../architecture/api-contracts.md#notificaciones-apinotificaciones).

## Configuración

| Setting (.NET)                      | Variable de Compose   | Descripción                                               |
| ----------------------------------- | --------------------- | --------------------------------------------------------- |
| `Notificaciones:WebPush:PublicKey`  | `WEBPUSH_PUBLIC_KEY`  | Clave pública VAPID (base64url, 65 bytes). No es secreta. |
| `Notificaciones:WebPush:PrivateKey` | `WEBPUSH_PRIVATE_KEY` | Clave privada VAPID (base64url, 32 bytes). **Secreto.**   |
| `Notificaciones:WebPush:Subject`    | `WEBPUSH_SUBJECT`     | `mailto:<correo real>` o `https://<sitio real>`           |

- **Sin claves el push está apagado**: `GET /api/notificaciones/push/clave-publica` y el alta de suscripciones responden `404`, la interfaz explica que no está disponible y no se encola nada. Las notificaciones del panel siguen igual.
- **Generar un par de claves** (una sola vez por ambiente; rotarlas invalida todas las suscripciones existentes, que se vuelven a registrar al abrir la app): `npx web-push generate-vapid-keys`.
- **El `Subject` no puede usar un dominio reservado** (`.local`, `.localhost`, `.test`, `.example`, `.invalid`, `example.com|org|net`): el servicio de push de Apple lo rechaza con `403 BadJwtToken`, y entonces los iPhone no reciben nada aunque Chrome y Firefox sí. Usá un correo o un sitio reales de la institución.
- **Validación al arrancar.** Una configuración inválida (una sola clave, claves mal formadas, `Subject` que no es `mailto:`/`https:`, o dominio reservado) hace **fallar el arranque en Production**, igual que las claves de sellado, con un mensaje que nombra el setting y nunca imprime las claves. Fuera de Production el push se deshabilita y se registra el motivo; un `Subject` en dominio reservado sólo registra una advertencia, porque Chrome y Firefox lo aceptan.

## Despliegue

Compose pasa las tres variables al backend (`infra/compose/compose.base.yml`) y los workflows de prod, staging y previews las toman de Environment:

| Scope                                            | Nombre                                  |
| ------------------------------------------------ | --------------------------------------- |
| Environment `prod`/`staging`/`pr-preview` var    | `WEBPUSH_PUBLIC_KEY`, `WEBPUSH_SUBJECT` |
| Environment `prod`/`staging`/`pr-preview` secret | `WEBPUSH_PRIVATE_KEY`                   |

Vacías (o sin definir) dejan el push apagado en ese ambiente. Los ambientes no deberían compartir claves. Las claves nunca se guardan en el repo, en la base ni en logs.

## Desarrollo local

Las claves de desarrollo viven en los user-secrets de `backend/src/ArsDocendi.Host` (`dotnet user-secrets set "Notificaciones:WebPush:PublicKey" ...`). Si el `Subject` guardado ahí es del tipo `mailto:dev@ars-docendi.local`, Chrome y Firefox funcionan pero **Apple responde 403**: cambialo a un `mailto:` o `https:` real para probar en un iPhone (`dotnet user-secrets set "Notificaciones:WebPush:Subject" "mailto:tu.correo@unlam.edu.ar"`). Fuera de Production el backend lo advierte al arrancar.

Los navegadores sólo permiten Web Push en `https://` o en `localhost`. Para probar en un celular real hace falta un túnel https (por ejemplo `tailscale serve` o Cloudflare Tunnel) hacia el servidor de Vite; la pantalla debe agregarse a la pantalla de inicio en iOS. Ver la guía de usuario en el Centro de ayuda: «Activar las notificaciones en el celular».

## Operación

- **Entrega**: `Notificador` sólo encola (cola en memoria de 1000 trabajos); `ProcesadorPush` envía en segundo plano con un timeout de 10 s por envío. Si la cola se llena se descarta el trabajo y se registra una advertencia con el tipo de aviso.
- **Logs**: un push aceptado se registra a nivel Information con el código de estado y el host del servicio de push (sirve para confirmar un envío); un fallo, sólo código de estado y motivo del servicio (por ejemplo `403 {"reason":"BadJwtToken"}`) o el tipo de excepción. Nunca títulos, mensajes, ids de usuario ni endpoints.
- **Suscripciones caducadas**: un `404`/`410` del servicio borra la suscripción. Otros fallos la conservan.
- **TTL**: 24 h. Un aviso que no pudo entregarse en un día (teléfono apagado) se descarta en el servicio de push; el aviso sigue en el panel.
- **Servidor web**: `frontend/nginx.conf` sirve `sw.js` y `manifest.webmanifest` con `Cache-Control: no-cache` para que un service worker nuevo se tome en la próxima carga.
