using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

/// <summary>
/// Crea los schemas <c>identity</c> y <c>audit</c> ejecutando el SQL versionado
/// bajo <c>database/</c>, embebido como recurso de este assembly.
/// <para>
/// La migración no declara DDL propio: todas las entidades del contexto están
/// mapeadas con <c>ExcludeFromMigrations()</c>. El SQL es la fuente autorizada
/// porque contiene construcciones que EF Core no sabe generar — funciones y
/// triggers plpgsql, <c>NULLS NOT DISTINCT</c>, las llamadas a <c>audit.attach</c>.
/// </para>
/// </summary>
public partial class SchemaIdentityYAudit : Migration
{
    /// <summary>
    /// Orden de aplicación. NO es alfabético, y el motivo es una dependencia
    /// circular entre los dos schemas:
    /// <list type="bullet">
    ///   <item><c>audit.change_log</c> declara <c>changed_by REFERENCES identity.users(id)</c>,
    ///         así que <c>identity.users</c> tiene que existir primero.</item>
    ///   <item><c>identity.users</c> necesita <c>audit.attach()</c> para engancharse,
    ///         y esa función la crea el schema audit.</item>
    /// </list>
    /// Se resuelve creando <c>identity.users</c> sin enganche, después audit completo,
    /// y difiriendo el <c>attach</c> de users al último archivo.
    /// </summary>
    private static readonly string[] ArchivosEnOrden =
    [
        "identity/001_identity_users.sql",
        "audit/001_audit_schema.sql",
        "identity/002_identity_roles.sql",
        "identity/003_identity_carreras.sql",
        "identity/004_identity_materias.sql",
        "identity/005_identity_user_roles.sql",
        "identity/006_identity_personas.sql",
        "identity/007_identity_permisos.sql",
        "identity/008_identity_rol_permisos.sql",
        "identity/009_identity_audit_attach.sql",
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var assembly = typeof(SchemaIdentityYAudit).Assembly;

        foreach (var archivo in ArchivosEnOrden)
        {
            migrationBuilder.Sql(RecursosSql.Leer(assembly, archivo));
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // CASCADE porque los triggers de auditoría de las tablas de negocio de
        // otros schemas dependen de audit.log_change. Un rollback de identity
        // implica que ya no hay nada que auditar.
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS audit CASCADE;");
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS identity CASCADE;");
    }
}
