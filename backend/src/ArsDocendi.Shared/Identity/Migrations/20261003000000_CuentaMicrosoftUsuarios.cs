using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArsDocendi.Shared.Identity.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261003000000_CuentaMicrosoftUsuarios")]
public sealed class CuentaMicrosoftUsuarios : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(CuentaMicrosoftUsuarios).Assembly,
            "identity/013_identity_users_cuenta_microsoft.sql"));

    // Vuelve al esquema anterior: azure_oid obligatorio con valores provisionales.
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            ALTER TABLE identity.users DROP CONSTRAINT IF EXISTS users_cuenta_microsoft_completa;
            UPDATE identity.users SET azure_oid = gen_random_uuid() WHERE azure_oid IS NULL;
            ALTER TABLE identity.users ALTER COLUMN azure_oid SET NOT NULL;
            ALTER TABLE identity.users DROP COLUMN IF EXISTS azure_tid;
            """);
}
