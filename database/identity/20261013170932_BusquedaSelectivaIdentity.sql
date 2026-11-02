-- Normalización SQL de palabras; no persiste un directorio duplicado.
CREATE EXTENSION IF NOT EXISTS unaccent WITH SCHEMA public;
CREATE FUNCTION identity.normalizar_busqueda(texto text) RETURNS text
LANGUAGE sql STABLE STRICT PARALLEL SAFE AS $$
    SELECT lower(public.unaccent(texto));
$$;
