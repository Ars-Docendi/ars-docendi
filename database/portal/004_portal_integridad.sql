-- Baseline consolidado: portal/004_portal_integridad.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

ALTER TABLE ONLY portal.certificaciones
    ADD CONSTRAINT certificaciones_perfil_id_fkey FOREIGN KEY (perfil_id) REFERENCES portal.perfiles(id) ON DELETE CASCADE;

ALTER TABLE ONLY portal.contactos
    ADD CONSTRAINT contactos_perfil_id_fkey FOREIGN KEY (perfil_id) REFERENCES portal.perfiles(id) ON DELETE CASCADE;

ALTER TABLE ONLY portal.cvs
    ADD CONSTRAINT cvs_perfil_id_fkey FOREIGN KEY (perfil_id) REFERENCES portal.perfiles(id) ON DELETE CASCADE;

ALTER TABLE ONLY portal.docente_habilidades
    ADD CONSTRAINT docente_habilidades_habilidad_id_fkey FOREIGN KEY (habilidad_id) REFERENCES portal.habilidades(id) ON DELETE CASCADE;

ALTER TABLE ONLY portal.docente_habilidades
    ADD CONSTRAINT docente_habilidades_perfil_id_fkey FOREIGN KEY (perfil_id) REFERENCES portal.perfiles(id) ON DELETE CASCADE;

ALTER TABLE ONLY portal.educaciones
    ADD CONSTRAINT educaciones_perfil_id_fkey FOREIGN KEY (perfil_id) REFERENCES portal.perfiles(id) ON DELETE CASCADE;

ALTER TABLE ONLY portal.experiencias
    ADD CONSTRAINT experiencias_perfil_id_fkey FOREIGN KEY (perfil_id) REFERENCES portal.perfiles(id) ON DELETE CASCADE;

ALTER TABLE ONLY portal.habilidades
    ADD CONSTRAINT habilidades_canonica_id_fkey FOREIGN KEY (canonica_id) REFERENCES portal.habilidades(id);

ALTER TABLE ONLY portal.proyecto_documentos
    ADD CONSTRAINT proyecto_documentos_proyecto_id_fkey FOREIGN KEY (proyecto_id) REFERENCES portal.proyectos(id) ON DELETE CASCADE;

ALTER TABLE ONLY portal.proyectos
    ADD CONSTRAINT proyectos_perfil_id_fkey FOREIGN KEY (perfil_id) REFERENCES portal.perfiles(id) ON DELETE CASCADE;
