using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Portal.Infrastructure.Migrations;

[DbContext(typeof(PortalDbContext))]
[Migration("20260906030000_RlsAsistentePortal")]
public sealed partial class RlsAsistentePortal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(RlsAsistentePortal).Assembly, "portal/003_portal_rls_asistente.sql"));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE portal.perfiles            DISABLE ROW LEVEL SECURITY;
            ALTER TABLE portal.educaciones         DISABLE ROW LEVEL SECURITY;
            ALTER TABLE portal.certificaciones     DISABLE ROW LEVEL SECURITY;
            ALTER TABLE portal.experiencias        DISABLE ROW LEVEL SECURITY;
            ALTER TABLE portal.docente_habilidades DISABLE ROW LEVEL SECURITY;
            ALTER TABLE portal.habilidades         DISABLE ROW LEVEL SECURITY;
            """);
}
