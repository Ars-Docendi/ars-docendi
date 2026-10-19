using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class IndicesSelectivosMedidosTests(PostgresFixture postgres)
{
    private static readonly string[] Nombres =
    ["designaciones_materia_idx", "pedido_historial_actor_idx", "pedido_historial_rol_idx", "idempotencia_pedido_idx"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Instalacion_y_upgrade_crean_indices_validos_sin_modificar_datos(bool upgrade)
    {
        var ct = TestContext.Current.CancellationToken;
        var cadena = await postgres.CrearBaseMigradaAsync("indices_medidos",
            upgrade ? "20261005000200_BaselineDesignaciones" : null);
        try
        {
            await using var conexion = new NpgsqlConnection(cadena);
            await conexion.OpenAsync(ct);
            if (upgrade)
            {
                await using var anteriores = new NpgsqlCommand("""
                    SELECT count(*) FROM pg_indexes
                    WHERE schemaname = 'designaciones' AND indexname = ANY(@nombres)
                    """, conexion);
                anteriores.Parameters.AddWithValue("nombres", Nombres);
                Assert.Equal(0L, await anteriores.ExecuteScalarAsync(ct));
            }
            await using (var datos = new NpgsqlCommand("""
                INSERT INTO identity.personas(id, documento, nombre, apellido)
                VALUES ('44444444-4444-4444-8444-444444444441', 'INDICES-001', 'Persona', 'Sintetica');
                INSERT INTO identity.materias(id, code, name)
                VALUES ('44444444-4444-4444-8444-444444444442', '99881', 'Materia sintetica');
                INSERT INTO identity.carreras(id, code, name)
                VALUES ('44444444-4444-4444-8444-444444444443', 'INDICES', 'Carrera sintetica');
                INSERT INTO designaciones.designaciones(persona_id, materia_id, carrera_id, cargo_id,
                    dedicacion_id, horas, vigente_desde)
                SELECT '44444444-4444-4444-8444-444444444441', '44444444-4444-4444-8444-444444444442',
                    '44444444-4444-4444-8444-444444444443',
                    (SELECT id FROM designaciones.cargos WHERE activo ORDER BY id LIMIT 1),
                    (SELECT id FROM designaciones.dedicaciones WHERE activo ORDER BY id LIMIT 1), 10, '2026-10-13';
                """, conexion))
                await datos.ExecuteNonQueryAsync(ct);
            var antes = await DatosAsync(conexion, ct);
            await using (var db = PostgresFixture.CrearDesignaciones(cadena))
                await db.GetService<IMigrator>().MigrateAsync(cancellationToken: ct);
            Assert.Equal(antes, await DatosAsync(conexion, ct));
            await using var catalogo = new NpgsqlCommand("""
                SELECT i.relname, x.indisvalid, x.indisready, am.amname
                FROM pg_index x JOIN pg_class i ON i.oid = x.indexrelid
                JOIN pg_namespace n ON n.oid = i.relnamespace
                JOIN pg_am am ON am.oid = i.relam
                WHERE n.nspname = 'designaciones' AND i.relname = ANY(@nombres)
                ORDER BY i.relname
                """, conexion);
            catalogo.Parameters.AddWithValue("nombres", Nombres);
            var encontrados = new List<string>();
            await using (var lector = await catalogo.ExecuteReaderAsync(ct))
            {
                while (await lector.ReadAsync(ct))
                {
                    encontrados.Add(lector.GetString(0));
                    Assert.True(lector.GetBoolean(1));
                    Assert.True(lector.GetBoolean(2));
                    Assert.Equal("btree", lector.GetString(3));
                }
            }
            Assert.Equal(Nombres.Order(), encontrados);
            await using (var db = PostgresFixture.CrearDesignaciones(cadena))
                await db.Database.MigrateAsync(ct);
            Assert.Equal(antes, await DatosAsync(conexion, ct));
        }
        finally { await postgres.EliminarBaseAsync(cadena); }
    }

    private static async Task<string> DatosAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand("""
            SELECT md5(coalesce(string_agg(fila, '|' ORDER BY fila), '')) FROM (
                SELECT 'designacion:' || to_jsonb(d)::text AS fila FROM designaciones.designaciones d
                UNION ALL SELECT 'pedido:' || to_jsonb(p)::text FROM designaciones.pedidos p
                UNION ALL SELECT 'historial:' || to_jsonb(h)::text FROM designaciones.pedido_historial h
                UNION ALL SELECT 'comando:' || to_jsonb(c)::text FROM designaciones.idempotencia_comandos c
                UNION ALL SELECT 'audit:' || to_jsonb(a)::text FROM audit.change_log a
            ) filas
            """, conexion);
        return (string)(await comando.ExecuteScalarAsync(ct))!;
    }
}
