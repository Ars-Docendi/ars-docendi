using System.Data.Common;
using System.Text.Json;
using ArsDocendi.Host.Administracion;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace ArsDocendi.IntegrationTests.Backend;

// Todas las modificaciones/índices candidatos se limitan a una DB desechable.
public sealed class AuditoriaRendimientoTests(PostgresFixture postgres, ITestOutputHelper salida)
    : ClasePostgresAislada(postgres, "auditoria_medicion")
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed record Comando(string Sql, NpgsqlParameter[] Parametros);
    private sealed class Captura : DbCommandInterceptor
    {
        public List<Comando> Comandos { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Comandos.Add(new Comando(command.CommandText, command.Parameters.Cast<NpgsqlParameter>()
                .Select(p => p.Clone()).ToArray()));
            return ValueTask.FromResult(result);
        }
    }
    private async Task Sql(string sql)
    {
        await using var c = await AbrirConexionAsync();
        await using var cmd = new NpgsqlCommand(sql, c) { CommandTimeout = 120 };
        await cmd.ExecuteNonQueryAsync(Ct);
    }
    private async Task<JsonElement> Plan(Comando comando, bool escritura = false)
    {
        await using var c = await AbrirConexionAsync();
        await using var tx = escritura ? await c.BeginTransactionAsync(Ct) : null;
        await using var cmd = new NpgsqlCommand("EXPLAIN (ANALYZE,BUFFERS,WAL,FORMAT JSON) " + comando.Sql, c, tx) { CommandTimeout = 120 };
        cmd.Parameters.AddRange(comando.Parametros.Select(p => p.Clone()).ToArray());
        using var json = JsonDocument.Parse((string)(await cmd.ExecuteScalarAsync(Ct))!);
        var resultado = json.RootElement[0].Clone();
        if (tx is not null) await tx.RollbackAsync(Ct);
        return resultado;
    }
    [Fact]
    public async Task Pagina_y_count_reales_se_miden_por_separado_con_candidatos_y_costo_de_escritura()
    {
        await using var inicial = await AbrirConexionAsync();
        await using var conteoInicial = new NpgsqlCommand("SELECT count(*) FROM audit.change_log", inicial);
        var eventosIniciales = (long)(await conteoInicial.ExecuteScalarAsync(Ct))!;
        await Sql("""
            DROP INDEX IF EXISTS audit.change_log_recientes_idx;
            INSERT INTO audit.change_log(schema_name,table_name,row_pk,action,changed_at,new_row,row_pk_format,row_key)
            SELECT CASE WHEN g%10=0 THEN 'designaciones' ELSE 'tareas' END,
              CASE WHEN g%10=0 THEN 'pedidos' ELSE 'tareas' END,g::text,
              CASE WHEN g%3=0 THEN 'INSERT' ELSE 'UPDATE' END,
              '2026-01-01T00:00:00Z'::timestamptz+g*interval '1 minute','{}','simple-v1',jsonb_build_object('id',g)
            FROM generate_series(1,200000) g;
            """);
        await Sql("VACUUM (ANALYZE) audit.change_log;");
        var datos = new List<object>();
        foreach (var candidato in new[] { "vigente", "fecha_id", "tabla_fecha_id", "ambos" })
        {
            await Sql("DROP INDEX IF EXISTS audit.auditoria_bench_fecha; DROP INDEX IF EXISTS audit.auditoria_bench_tabla;");
            if (candidato is "fecha_id" or "ambos")
                await Sql("CREATE INDEX auditoria_bench_fecha ON audit.change_log(changed_at DESC,id DESC);");
            if (candidato is "tabla_fecha_id" or "ambos")
                await Sql("CREATE INDEX auditoria_bench_tabla ON audit.change_log(schema_name,table_name,changed_at DESC,id DESC);");
            await Sql("VACUUM (ANALYZE) audit.change_log;");
            foreach (var caso in new[] { "reciente", "tabla", "rango", "fila" })
            {
                var captura = new Captura();
                await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
                    .UseNpgsql(Cadena).AddInterceptors(captura).Options);
                var filtro = caso switch
                {
                    "tabla" => new ConsultaAuditoriaDto(Schema: "designaciones", Tabla: "pedidos"),
                    "rango" => new ConsultaAuditoriaDto(Desde: new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero)),
                    "fila" => new ConsultaAuditoriaDto(Schema: "tareas", Tabla: "tareas", RowPk: "199999"),
                    _ => new ConsultaAuditoriaDto(),
                };
                var pagina = await new RepositorioAuditoria(db).ListarAsync(filtro, Ct);
                Assert.Equal(caso switch { "tabla" => 20000, "fila" => 1, "rango" => eventosIniciales + 1281, _ => eventosIniciales + 200000 }, pagina.Total);
                Assert.Equal(Math.Min(pagina.Total, 50), pagina.Registros.Count);
                var orden = pagina.Registros.Select(x => x.Registro).ToArray();
                Assert.True(orden.Zip(orden.Skip(1)).All(p => p.First.CambiadoEn > p.Second.CambiadoEn
                    || (p.First.CambiadoEn == p.Second.CambiadoEn && p.First.Id > p.Second.Id)));
                var count = captura.Comandos.Single(x => x.Sql.Contains("count(*)", StringComparison.OrdinalIgnoreCase));
                var lista = captura.Comandos.Single(x => x.Sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
                foreach (var (tipo, comando) in new[] { ("count", count), ("pagina", lista) })
                {
                    // Primera observación no calentada deliberadamente, luego tres repeticiones
                    // calientes; no se afirma cache fría del host/daemon compartido.
                    for (var repeticion = 0; repeticion < 4; repeticion++)
                        datos.Add(new
                        {
                            candidato,
                            caso,
                            tipo,
                            repeticion,
                            sql = comando.Sql,
                            parametros = comando.Parametros.Select(p => new { p.ParameterName, p.Value }),
                            plan = await Plan(comando)
                        });
                }
                Assert.Equal(5, db.Database.GetCommandTimeout());
            }
            for (var repeticion = 0; repeticion < 3; repeticion++)
                datos.Add(new { candidato, tipo = "insert_10000", repeticion, plan = await Plan(new Comando("""
                    INSERT INTO audit.change_log(schema_name,table_name,row_pk,action,changed_at,new_row,row_pk_format,row_key)
                    SELECT 'tareas','tareas',('write-'||g),'UPDATE',now(),'{}','simple-v1',jsonb_build_object('id',g)
                    FROM generate_series(1,10000) g
                    """, []), escritura: true) });
            await using var conexion = await AbrirConexionAsync();
            await using var tamano = new NpgsqlCommand("""
                SELECT indexrelid::regclass::text,pg_relation_size(indexrelid)::bigint
                FROM pg_index WHERE indrelid='audit.change_log'::regclass ORDER BY indexrelid::regclass::text
                """, conexion);
            await using var lector = await tamano.ExecuteReaderAsync(Ct);
            while (await lector.ReadAsync(Ct))
                datos.Add(new { candidato, tipo = "tamano_indice", indice = lector.GetString(0), bytes = lector.GetInt64(1) });
        }
        var json = JsonSerializer.Serialize(datos, new JsonSerializerOptions { WriteIndented = true });
        salida.WriteLine(json);
        if (Environment.GetEnvironmentVariable("AUDITORIA_EVIDENCIA_DIR") is { Length: > 0 } destino)
        {
            Directory.CreateDirectory(destino);
            await File.WriteAllTextAsync(Path.Combine(destino, "auditoria-mediciones.json"), json, Ct);
        }
    }
}
