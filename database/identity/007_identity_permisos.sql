-- identity.permisos
-- Catálogo CERRADO. Cada fila corresponde a un `code` que algún check de
-- autorización del backend lee. No se crean permisos desde ninguna superficie de
-- usuario: un permiso sin código que lo consuma no hace nada, y ofrecerlo sería
-- exactamente lo que prohíbe el invariante #7 (nada de fake UI).
--
-- Lo editable es la membresía (identity.rol_permisos), no este catálogo.

CREATE TABLE identity.permisos (
    id           UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    code         TEXT         NOT NULL UNIQUE,
    nombre       TEXT         NOT NULL,
    descripcion  TEXT         NOT NULL,
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT now()
);

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000001', 'designaciones.ver',                    'Ver designaciones',                    'Consultar el estado y detalle de designaciones sin modificarlas.'),
    ('b2000000-0000-4000-8000-000000000002', 'designaciones.gestionar',              'Gestionar designaciones',              'Crear y editar proyectos docentes e iniciar el flujo de designación.'),
    ('b2000000-0000-4000-8000-000000000003', 'designaciones.aprobar_coordinacion',   'Aprobar designaciones — Coordinación', 'Aprobar o rechazar designaciones en la instancia de coordinación de carrera.'),
    ('b2000000-0000-4000-8000-000000000004', 'designaciones.aprobar_secretaria',     'Aprobar designaciones — Secretaría',   'Aprobar o rechazar designaciones en la instancia de secretaría académica.'),
    ('b2000000-0000-4000-8000-000000000005', 'designaciones.aprobar_decanato',       'Aprobar designaciones — Decanato',     'Aprobar o rechazar designaciones en la instancia final del decanato.'),
    ('b2000000-0000-4000-8000-000000000006', 'aulas.ver',                            'Ver reservas de aulas',                'Consultar el calendario de reservas de aulas y laboratorios.'),
    ('b2000000-0000-4000-8000-000000000007', 'aulas.gestionar',                      'Gestionar reservas de aulas',          'Solicitar y asignar aulas o laboratorios para mesas de examen.'),
    ('b2000000-0000-4000-8000-000000000008', 'aulas.aprobar',                        'Aprobar reservas de aulas',            'Confirmar o rechazar pedidos de reserva realizados por administrativos.'),
    ('b2000000-0000-4000-8000-000000000009', 'usuarios.ver',                         'Ver usuarios',                         'Consultar el listado de usuarios registrados en el sistema.'),
    ('b2000000-0000-4000-8000-000000000010', 'usuarios.administrar',                 'Administrar usuarios',                 'Crear, editar, activar y desactivar cuentas de usuario del sistema.'),
    ('b2000000-0000-4000-8000-000000000011', 'roles.ver',                            'Ver roles',                            'Consultar el listado de roles y sus descripciones.'),
    ('b2000000-0000-4000-8000-000000000012', 'roles.administrar',                    'Administrar roles',                    'Crear y modificar roles del sistema.'),
    ('b2000000-0000-4000-8000-000000000013', 'roles.gestionar_membresia',            'Gestionar membresía de roles',         'Asignar y revocar permisos a cada rol.'),
    ('b2000000-0000-4000-8000-000000000014', 'periodos.administrar',                 'Administrar períodos',                 'Gestionar los períodos académicos habilitados para designaciones y reservas.'),
    ('b2000000-0000-4000-8000-000000000015', 'sistema.parametrizar',                 'Parametrizar sistema',                 'Configurar parámetros generales (umbrales, textos, fechas de corte).'),
    ('b2000000-0000-4000-8000-000000000016', 'tareas.ver',                           'Ver tareas',                           'Consultar el tablero de tareas internas del departamento.'),
    ('b2000000-0000-4000-8000-000000000017', 'tareas.gestionar',                     'Gestionar tareas',                     'Crear, editar, asignar y cerrar tareas internas del departamento.'),
    ('b2000000-0000-4000-8000-000000000018', 'portal.ver',                           'Ver portal personal',                  'Acceder al portal propio con datos personales y horas disponibles.'),
    ('b2000000-0000-4000-8000-000000000019', 'portal.editar',                        'Editar portal personal',               'Actualizar datos personales, horas disponibles y áreas de experticia.'),
    ('b2000000-0000-4000-8000-000000000020', 'reportes.ver',                         'Ver reportes globales',                'Acceder a reportes consolidados de designaciones, aulas y actividad docente.');

SELECT audit.attach('identity.permisos');
