using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Modules.Portal.Infrastructure.Migrations;

[DbContext(typeof(PortalDbContext))]
[Migration("20260906040000_ComentariosAsistentePortal")]
public sealed partial class ComentariosAsistentePortal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(ComentariosAsistentePortal).Assembly,
            "portal/004_portal_comentarios_asistente.sql"));

    // Sin Down: quitar los comentarios no restaura ningún estado anterior —antes no
    // había ninguno— y dejaría al prefijo del prompt describiendo tablas mudas.
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
