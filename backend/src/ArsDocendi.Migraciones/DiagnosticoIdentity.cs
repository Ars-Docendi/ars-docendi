using ArsDocendi.Shared.Identity;

namespace ArsDocendi.Migraciones;

public sealed class DiagnosticoIdentity(IdentityDbContext db)
    : DiagnosticoEfSql<IdentityDbContext>(db, "identity", """
        SELECT 'destino', 'persona', id, NULL::text, NULL::uuid FROM identity.personas
        UNION ALL SELECT 'destino', 'usuario', id, NULL::text, NULL::uuid FROM identity.users
        UNION ALL SELECT 'destino', 'materia', id, NULL::text, NULL::uuid FROM identity.materias
        UNION ALL SELECT 'hallazgo', 'identity-upn-duplicado', id, NULL::text, NULL::uuid
        FROM (SELECT id, count(*) OVER (PARTITION BY lower(btrim(upn))) AS cantidad FROM identity.users) u
        WHERE cantidad > 1
        UNION ALL SELECT 'hallazgo', 'identity-nombre-rol-duplicado', id, NULL::text, NULL::uuid
        FROM (SELECT id, count(*) OVER (PARTITION BY lower(btrim(name))) AS cantidad
              FROM identity.roles WHERE is_active) r WHERE cantidad > 1
        UNION ALL SELECT 'hallazgo', 'identity-scope-incoherente', ur.id, NULL::text, NULL::uuid
        FROM identity.user_roles ur JOIN identity.roles r ON r.id = ur.role_id
        WHERE ur.deleted_at IS NULL AND CASE
          WHEN r.code = 'docente' THEN ur.materia_id IS NULL OR ur.carrera_id IS NULL
          WHEN r.scope = 'global' THEN ur.materia_id IS NOT NULL OR ur.carrera_id IS NOT NULL
          WHEN r.scope = 'materia' THEN ur.materia_id IS NULL OR ur.carrera_id IS NOT NULL
          WHEN r.scope = 'carrera' THEN ur.carrera_id IS NULL OR ur.materia_id IS NOT NULL
          ELSE true END
        """);
