-- Tokens independientes para reemplazar listas, incluso listas vacías.
ALTER TABLE portal.perfiles
  ADD COLUMN version_habilidades bigint NOT NULL DEFAULT 0 CHECK (version_habilidades >= 0),
  ADD COLUMN version_intereses bigint NOT NULL DEFAULT 0 CHECK (version_intereses >= 0);
UPDATE portal.perfiles p SET version_habilidades = 1
  WHERE EXISTS (SELECT 1 FROM portal.docente_habilidades h WHERE h.perfil_id = p.id AND h.tipo = 'habilidad');
UPDATE portal.perfiles p SET version_intereses = 1
  WHERE EXISTS (SELECT 1 FROM portal.docente_habilidades h WHERE h.perfil_id = p.id AND h.tipo = 'interes');
