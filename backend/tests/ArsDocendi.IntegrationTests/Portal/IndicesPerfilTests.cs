using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

public sealed class IndicesPerfilTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "portal_indices")
{
    [Fact]
    public async Task Cuatro_colecciones_tienen_un_solo_indice_selectivo_por_perfil()
    {
        await using var conexion = await AbrirConexionAsync();
        foreach (var tabla in new[] { "experiencias", "educaciones", "certificaciones", "proyectos" })
        {
            await using var comando = new NpgsqlCommand("""
                SELECT count(*) FROM pg_index i JOIN pg_class t ON t.oid=i.indrelid
                JOIN pg_namespace n ON n.oid=t.relnamespace JOIN pg_attribute a ON a.attrelid=t.oid AND a.attname='perfil_id'
                WHERE n.nspname='portal' AND t.relname=@tabla AND i.indisvalid AND i.indpred IS NULL
                  AND i.indnkeyatts=1 AND i.indkey[0]=a.attnum;
                """, conexion);
            comando.Parameters.AddWithValue("tabla", tabla);
            Assert.Equal(1L, (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);
        }
    }
}
