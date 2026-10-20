-- Decanato pasa a tener `usuarios.ver`.
--
-- QUÉ HABILITA, Y ES MÁS QUE LO QUE MOTIVÓ EL CAMBIO
-- El pedido fue que Decanato pueda consultar por el asistente los datos de
-- contacto de un docente. En el asistente, ese acceso lo abre la conjunción
-- «ámbito global Y `usuarios.ver`»: Decanato ya era global, así que le faltaba
-- este permiso y con él pasa a la conexión con datos personales —teléfono y mail
-- institucionales, documento, CUIL, fecha de nacimiento—.
--
-- Pero `usuarios.ver` no gobierna sólo al asistente, y conviene que quede escrito
-- acá porque el que lo lea después no lo va a deducir del nombre del archivo:
--
--   · `GET /api/administracion/usuarios` y `/{id}` — el listado y la ficha de
--     usuarios de la superficie de administración.
--   · `GET /api/administracion/catalogos` — los catálogos de esa pantalla.
--   · `DocentesController` — deja de acotar el listado de docentes al ámbito.
--
-- ES TODO DE LECTURA. Las escrituras de esa superficie van por
-- `usuarios.administrar`, que Decanato NO recibe: no puede crear, editar,
-- activar ni desactivar a nadie.
--
-- POR QUÉ NO SE CREÓ UN PERMISO NUEVO, que era la otra salida
-- «Ver datos personales del padrón» y «entrar a la administración de usuarios»
-- son cosas distintas, y el asistente usa el segundo como proxy del primero. Un
-- permiso propio las separaría — es el mismo argumento con que se creó
-- `portal.ver_trayectoria_ajena` en vez de reusar `portal.ver`.
--
-- No se hizo porque acá el proxy no falla ABIERTO: quien tiene `usuarios.ver` ya
-- puede ver el padrón entero por la pantalla de administración, así que dárselo
-- también por el asistente no le agrega ningún dato. En el caso de portal era al
-- revés — `portal.ver` lo tenían los siete roles y significaba «entrar a mi propio
-- portal», así que reusarlo habría abierto el padrón a todos.
--
-- Si algún día hace falta que un rol vea datos personales por el asistente SIN
-- entrar a la administración, ahí sí corresponde el permiso propio. Hoy no hay
-- ningún rol en esa situación.
--
-- Idempotente: ON CONFLICT DO NOTHING.

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
FROM identity.roles r
CROSS JOIN identity.permisos p
WHERE r.code = 'decanato'
  AND p.code = 'usuarios.ver'
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
