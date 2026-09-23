using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Aulas.Infrastructure.Migrations;

/// <summary>
/// Crea el schema <c>aulas</c> ejecutando el SQL versionado bajo <c>database/aulas/</c>,
/// embebido como recurso de este assembly.
/// <para>
/// La entidad del contexto está mapeada con <c>ExcludeFromMigrations()</c>, así que
/// esta migración no declara DDL propio. El SQL es la fuente autorizada porque
/// contiene construcciones que EF Core no sabe generar: el CHECK de consistencia
/// entre <c>estado</c> y <c>aula_asignada</c>, y la llamada a <c>audit.attach</c>.
/// </para>
/// <para>
/// PRECONDICIÓN: el schema <c>audit</c> y <c>identity.personas</c> ya tienen que
/// existir — este DDL llama a <c>audit.attach</c> y declara una FK contra
/// <c>identity.personas</c>. Lo garantiza el orden de registración en el Host:
/// <c>AddArsDocendiShared()</c> va antes que <c>AddAulasModule()</c>.
/// </para>
/// </summary>
public partial class SchemaAulasSolicitudes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(RecursosSql.Leer(
            typeof(SchemaAulasSolicitudes).Assembly,
            "aulas/001_aulas_solicitudes.sql"));

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP SCHEMA IF EXISTS aulas CASCADE;");
}
