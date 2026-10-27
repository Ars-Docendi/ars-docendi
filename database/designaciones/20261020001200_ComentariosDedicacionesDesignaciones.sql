-- Comentarios del catálogo de dedicaciones, para el prefijo del asistente.
--
-- VAN EN SU PROPIA MIGRACIÓN Y NO EN 010, y el motivo es el orden: la migración
-- de comentarios del asistente es de agosto y el catálogo llegó en septiembre, así
-- que un COMMENT sobre `dedicacion_solicitada_id` escrito allá referencia una
-- columna que todavía no existe y tumba el migrador entero.
--
-- Lo que estos comentarios le dicen al modelo es la mitad que no se ve en el
-- esquema: que la dedicación se lee por la FK y que la columna textual es
-- historia, porque un trigger prohíbe escribirla desde la migración 010.
COMMENT ON TABLE designaciones.dedicaciones IS
    'Catálogo cerrado de dedicaciones docentes, seis categorías. La dedicación de un pedido o de una designación se lee uniendo por dedicacion_solicitada_id o dedicacion_id, no por la columna de texto.';
COMMENT ON COLUMN designaciones.dedicaciones.codigo IS
    'Número de categoría, del 1 al 6.';
COMMENT ON COLUMN designaciones.dedicaciones.nombre IS
    'Nombre para mostrar, con la forma «Categoría N».';
COMMENT ON COLUMN designaciones.dedicaciones.id IS
    'Identificador de la categoría. Es a esto que apuntan dedicacion_id y dedicacion_solicitada_id.';
COMMENT ON COLUMN designaciones.dedicaciones.orden IS
    'Orden de presentación de las categorías.';
COMMENT ON COLUMN designaciones.dedicaciones.created_at IS
    'Momento de alta del registro. Metadato del sistema.';
COMMENT ON COLUMN designaciones.dedicaciones.activo IS
    'Si la categoría se puede elegir hoy. Una designación vieja puede apuntar a una inactiva.';

COMMENT ON COLUMN designaciones.pedidos.dedicacion_solicitada_id IS
    'Dedicación pedida, contra designaciones.dedicaciones. Es la forma vigente: dedicacion_solicitada conserva el texto de los pedidos anteriores al catálogo.';
COMMENT ON COLUMN designaciones.designaciones.dedicacion_id IS
    'Dedicación vigente, contra designaciones.dedicaciones. Es la forma vigente: dedicacion conserva el texto de las designaciones anteriores al catálogo.';
COMMENT ON COLUMN designaciones.designaciones.horas_investigacion IS
    'Horas semanales de investigación de la designación.';
COMMENT ON COLUMN designaciones.designaciones.horas_externas IS
    'Horas semanales que la persona dedica en otra institución.';
