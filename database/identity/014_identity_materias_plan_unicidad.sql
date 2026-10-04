-- Target del FK compuesto de designaciones.designaciones: (materia_plan_id, materia_id) debe
-- coincidir con una pertenencia real. El id ya es único; la pareja también, sin cambiar el PK.
ALTER TABLE identity.materias_plan
    ADD CONSTRAINT materias_plan_id_materia_unico UNIQUE (id, materia_id);
