using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Designaciones.Infrastructure.Migrations
{
    /// <summary>
    /// Crea el schema <c>designaciones</c> ejecutando el SQL versionado bajo
    /// <c>database/designaciones/</c>, embebido como recurso de este assembly.
    /// <para>
    /// Todas las entidades del contexto están mapeadas con
    /// <c>ExcludeFromMigrations()</c>, así que esta migración no declara DDL propio.
    /// El SQL es la fuente autorizada porque contiene construcciones que EF Core no
    /// sabe generar: el índice único parcial de BR-designaciones-001, la constraint
    /// <c>EXCLUDE</c> de no solapamiento de vigencias y las llamadas a
    /// <c>audit.attach</c>.
    /// </para>
    /// <para>
    /// PRECONDICIÓN: el schema <c>audit</c> y las tablas de <c>identity</c> ya tienen
    /// que existir — este DDL llama a <c>audit.attach</c> y declara FKs contra
    /// <c>identity.personas</c> y <c>identity.materias</c>. Lo garantiza el orden de
    /// registración en el Host: <c>AddArsDocendiShared()</c> va antes que
    /// <c>AddDesignacionesModule()</c>, y el contenedor devuelve los
    /// <c>IMigradorModulo</c> en ese orden.
    /// </para>
    /// </summary>
    public partial class SchemaDesignaciones : Migration
    {
        private static readonly string[] ArchivosEnOrden =
        [
            "designaciones/001_designaciones_cargos.sql",
            "designaciones/002_designaciones_periodos.sql",
            "designaciones/003_designaciones_pedidos.sql",
            "designaciones/004_designaciones_pedido_adjuntos.sql",
            "designaciones/005_designaciones_pedido_historial.sql",
            "designaciones/006_designaciones_designaciones.sql",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var assembly = typeof(SchemaDesignaciones).Assembly;

            foreach (var archivo in ArchivosEnOrden)
            {
                migrationBuilder.Sql(RecursosSql.Leer(assembly, archivo));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS designaciones CASCADE;");
        }
    }
}
