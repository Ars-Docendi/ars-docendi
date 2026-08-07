using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Designaciones.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega la secuencia y la función que generan el número de trámite legible
    /// (<c>pedidos.numero</c>). Ver <c>database/designaciones/007_designaciones_numeracion.sql</c>.
    /// </summary>
    public partial class NumeracionPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RecursosSql.Leer(
                typeof(NumeracionPedidos).Assembly,
                "designaciones/007_designaciones_numeracion.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS designaciones.siguiente_numero_pedido();");
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS designaciones.pedidos_numero_seq;");
        }
    }
}
