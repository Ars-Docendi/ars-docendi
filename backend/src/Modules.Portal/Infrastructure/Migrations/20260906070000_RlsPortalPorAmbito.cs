using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Portal.Infrastructure.Migrations;

/// <summary>
/// El predicado de visibilidad de portal pasa a conjugar ámbito.
/// </summary>
/// <remarks>
/// <b>APLICA UN ARCHIVO DE `database/identity/` DESDE LA CADENA DE PORTAL, y eso
/// pide explicación.</b> La función vive en el schema `identity` y en su carpeta,
/// junto a las otras cinco `asistente_*`: para un predicado de seguridad, que se
/// encuentre donde están sus hermanas vale más que la prolijidad de qué migración
/// lo corre.
///
/// Pero no puede correr con ellas. Su cuerpo referencia
/// `designaciones.designaciones`, y una función `LANGUAGE sql` se parsea al
/// crearse: en la cadena de identity —que corre PRIMERO— ese schema todavía no
/// existe y el `CREATE FUNCTION` falla con «relation does not exist». El orden es
/// identity → designaciones → aulas → portal, así que acá ya está todo.
///
/// <b>No se pasó a plpgsql para esquivarlo.</b> Diferir el parseo haría que un
/// typo en el nombre de una función hermana no falle al migrar sino en la primera
/// consulta, y este predicado es el que decide quién ve el perfil de quién.
///
/// Las dos sentencias van juntas y en este orden porque son una unidad: las
/// policies invocan la función, y crearlas antes las dejaría rotas.
/// </remarks>
[DbContext(typeof(PortalDbContext))]
[Migration("20260906070000_RlsPortalPorAmbito")]
public sealed class RlsPortalPorAmbito : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(RecursosSql).Assembly,
            "identity/017_identity_funcion_alcance_de_persona.sql"));

        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(RlsPortalPorAmbito).Assembly,
            "portal/005_portal_rls_ambito.sql"));
    }

    // Vuelve al predicado sin ámbito reaplicando 003, que es idempotente y hace
    // DROP + CREATE de las mismas seis policies. Escribirlas de nuevo acá sería una
    // tercera copia del predicado viejo, que es lo que este cambio vino a eliminar.
    // La función se borra después, ya sin nadie que la invoque.
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(RlsPortalPorAmbito).Assembly,
            "portal/003_portal_rls_asistente.sql"));

        migrationBuilder.Sql("DROP FUNCTION IF EXISTS identity.asistente_alcanza_a(UUID);");
    }
}
