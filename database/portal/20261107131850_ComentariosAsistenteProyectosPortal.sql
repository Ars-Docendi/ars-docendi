-- Comentarios de dominio sobre `portal.proyectos`, que el asistente lee desde
-- 2026-11-07. Mismo criterio que 20261020001600: estos comentarios arman el
-- prefijo del prompt, no son documentación. `descripcion` no se comenta porque no
-- se concede (texto libre que el enmascarador no puede proteger).
--
-- Idempotente: COMMENT ON reemplaza el comentario anterior.

COMMENT ON TABLE portal.proyectos IS
    'Proyectos de investigación, extensión o desarrollo que el docente declara sobre sí mismo en su Portal. Sinónimos del dominio: proyecto, investigación, antecedentes de investigación. AUTODECLARADO y opcional: un conteo cuenta a quienes lo cargaron, no al Departamento. Sirve para saber si alguien investiga en un área afín, pero NO prueba que cumpla los requisitos de una materia. El texto descriptivo del proyecto no está disponible.';

COMMENT ON COLUMN portal.proyectos.id IS
    'Identificador del proyecto.';
COMMENT ON COLUMN portal.proyectos.perfil_id IS
    'Perfil del docente que declaró el proyecto, en portal.perfiles. Para llegar al nombre hay que pasar por portal.perfiles.persona_id y de ahí a identity.personas.';
COMMENT ON COLUMN portal.proyectos.nombre IS
    'Nombre del proyecto, escrito libremente por el docente: términos afines pueden estar escritos de otra manera, por eso conviene buscar con ILIKE y varias variantes.';
COMMENT ON COLUMN portal.proyectos.rol IS
    'Rol del docente en el proyecto (director, investigador, becario, etc.), escrito libremente.';
COMMENT ON COLUMN portal.proyectos.desde IS
    'Fecha de inicio de su participación.';
COMMENT ON COLUMN portal.proyectos.hasta IS
    'Fecha de fin de su participación. NULO significa QUE SIGUE EN CURSO, no un dato faltante.';
COMMENT ON COLUMN portal.proyectos.doi IS
    'DOI de la publicación o del proyecto. OPCIONAL: nulo significa que no cargó uno, no que el proyecto no tenga producción.';
COMMENT ON COLUMN portal.proyectos.created_at IS
    'Momento de carga del registro. Metadato del sistema.';
