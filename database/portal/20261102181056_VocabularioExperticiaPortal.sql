-- 20261102181056_VocabularioExperticiaPortal (Portal).
-- No editar después de publicar: crear otra migración incremental.
--
-- Change catalogo-aulas-y-vocabulario-portal.
--
-- Vocabulario base de habilidades e intereses: los términos que hasta ahora fijaba
-- el frontend. Es un catálogo versionado, no un fixture. termino_norm se escribe
-- explícito con la misma normalización del backend (Trim + ToUpperInvariant) para
-- no depender de la collation de upper(). Un término que ya existía como sugerido
-- queda incorporado.

INSERT INTO portal.habilidades (termino, termino_norm, sugerido) VALUES
    ('Algoritmos', 'ALGORITMOS', false),
    ('Arquitectura de software', 'ARQUITECTURA DE SOFTWARE', false),
    ('Bases de datos', 'BASES DE DATOS', false),
    ('Ciberseguridad', 'CIBERSEGURIDAD', false),
    ('Ciencia de datos', 'CIENCIA DE DATOS', false),
    ('Cloud computing', 'CLOUD COMPUTING', false),
    ('Computación gráfica', 'COMPUTACIÓN GRÁFICA', false),
    ('Desarrollo web', 'DESARROLLO WEB', false),
    ('DevOps', 'DEVOPS', false),
    ('Machine learning', 'MACHINE LEARNING', false),
    ('Matemática discreta', 'MATEMÁTICA DISCRETA', false),
    ('Programación orientada a objetos', 'PROGRAMACIÓN ORIENTADA A OBJETOS', false),
    ('Redes de computadoras', 'REDES DE COMPUTADORAS', false),
    ('Robótica', 'ROBÓTICA', false),
    ('Sistemas embebidos', 'SISTEMAS EMBEBIDOS', false),
    ('Sistemas operativos', 'SISTEMAS OPERATIVOS', false),
    ('Testing y calidad', 'TESTING Y CALIDAD', false)
ON CONFLICT (termino_norm) DO UPDATE SET sugerido = false;

-- Un término fusionado apunta a otro, nunca a sí mismo.
ALTER TABLE portal.habilidades
    ADD CONSTRAINT habilidades_canonica_distinta CHECK (canonica_id IS NULL OR canonica_id <> id);
