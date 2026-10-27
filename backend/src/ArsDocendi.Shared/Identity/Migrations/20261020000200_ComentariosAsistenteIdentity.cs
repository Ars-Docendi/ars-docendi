using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261020000200_ComentariosAsistenteIdentity")]
[RecursosMigracionSql("identity/20261020000200_ComentariosAsistenteIdentity.sql")]
public sealed class ComentariosAsistenteIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AplicarRecursosSql(typeof(ComentariosAsistenteIdentity));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Recuperación explícita desde respaldo; no revertir DDL automáticamente.");
}
