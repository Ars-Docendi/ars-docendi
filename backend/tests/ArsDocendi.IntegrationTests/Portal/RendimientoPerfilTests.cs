using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

public sealed class RendimientoPerfilTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "portal_medicion")
{
    [Fact]
    public async Task Medir_planes_y_writes_reales_con_2000_perfiles_y_20000_experiencias()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await EjecutarAsync(conexion, """
            INSERT INTO portal.perfiles(id,persona_id) SELECT md5(n::text)::uuid,md5(n::text)::uuid FROM generate_series(1,2000) n;
            INSERT INTO portal.experiencias(perfil_id,puesto,organizacion,descripcion,desde)
              SELECT md5(((n-1)%2000+1)::text)::uuid,'Puesto','Organización',repeat('D',100),'2020-01-01'::date FROM generate_series(1,20000) n;
            INSERT INTO portal.educaciones(perfil_id,nivel,carrera,institucion,desde)
              SELECT md5(((n-1)%2000+1)::text)::uuid,'Grado','Carrera','Institución','2020-01-01'::date FROM generate_series(1,20000) n;
            INSERT INTO portal.certificaciones(perfil_id,nombre,emisor,fecha)
              SELECT md5(((n-1)%2000+1)::text)::uuid,'Certificación','Emisor','2020-01-01'::date FROM generate_series(1,20000) n;
            INSERT INTO portal.proyectos(perfil_id,nombre,rol,descripcion,desde)
              SELECT md5(((n-1)%2000+1)::text)::uuid,'Proyecto','Rol',repeat('D',100),'2020-01-01'::date FROM generate_series(1,20000) n;
            """, ct);
        var tablas = new[] { "experiencias", "educaciones", "certificaciones", "proyectos" };
        foreach (var tabla in tablas) await EjecutarAsync(conexion, $"DROP INDEX portal.{tabla}_perfil_idx; ANALYZE portal.{tabla};", ct);
        var evidencia = new Dictionary<string, object>();
        await using (var version = new NpgsqlCommand("SELECT version()", conexion)) evidencia["servidor"] = (await version.ExecuteScalarAsync(ct))!;
        evidencia["dataset"] = new { perfiles = 2000, experiencias = 20000, educaciones = 20000, certificaciones = 20000, proyectos = 20000 };
        evidencia["politicaCache"] = "Primera ejecución no cebada explícitamente y tres repeticiones warm; no se afirma cold OS/cache.";
        foreach (var fase in new[] { "antes", "despues" })
        {
            if (fase == "despues")
                foreach (var tabla in tablas) await EjecutarAsync(conexion, $"CREATE INDEX {tabla}_perfil_idx ON portal.{tabla}(perfil_id); ANALYZE portal.{tabla};", ct);
            var planes = new Dictionary<string, object>();
            foreach (var tabla in tablas)
            {
                var consultas = new List<JsonElement>();
                for (var repeticion = 0; repeticion < 4; repeticion++)
                {
                    var plan = await PlanAsync(conexion, $"SELECT * FROM portal.{tabla} WHERE perfil_id=md5('1')::uuid", null, ct);
                    Assert.Equal(10d, plan[0].GetProperty("Plan").GetProperty("Actual Rows").GetDouble());
                    if (fase == "antes") Assert.Contains("Seq Scan", plan.ToString());
                    else { Assert.Contains("Index", plan.ToString()); Assert.DoesNotContain("Rows Removed by Filter", plan.ToString()); }
                    consultas.Add(plan);
                }
                planes[tabla] = consultas;
            }
            var writes = new List<object>();
            foreach (var tabla in tablas)
                for (var repeticion = 0; repeticion < 3; repeticion++)
                {
                    await using var tx = await conexion.BeginTransactionAsync(ct);
                    var columnas = tabla switch
                    {
                        "experiencias" => "puesto,organizacion,descripcion,desde",
                        "educaciones" => "nivel,carrera,institucion,desde",
                        "certificaciones" => "nombre,emisor,fecha",
                        _ => "nombre,rol,descripcion,desde"
                    };
                    var valores = tabla == "certificaciones" ? "'Nombre','Emisor','2020-01-01'::date" : "'Texto','Texto','Texto','2020-01-01'::date";
                    var insert = await PlanAsync(conexion, $"INSERT INTO portal.{tabla}(perfil_id,{columnas}) SELECT md5('1')::uuid,{valores} FROM generate_series(1,1000)", tx, ct);
                    var campo = tabla == "experiencias" ? "puesto" : tabla == "educaciones" ? "carrera" : "nombre";
                    var update = await PlanAsync(conexion, $"UPDATE portal.{tabla} SET {campo}={campo}||' actualizado' WHERE id IN (SELECT id FROM portal.{tabla} LIMIT 1000)", tx, ct);
                    writes.Add(new { tabla, repeticion, insert, update });
                    await tx.RollbackAsync(ct);
                }
            evidencia[fase] = new { planes, writes };
        }
        foreach (var tabla in tablas)
        {
            await using var contar = new NpgsqlCommand($"SELECT count(*) FROM portal.{tabla}", conexion);
            Assert.Equal(20000L, (long)(await contar.ExecuteScalarAsync(ct))!);
        }
        var tamanos = new Dictionary<string, long>();
        foreach (var tabla in tablas)
        {
            await using var tamano = new NpgsqlCommand($"SELECT pg_relation_size('portal.{tabla}_perfil_idx')", conexion);
            tamanos[tabla] = (long)(await tamano.ExecuteScalarAsync(ct))!;
        }
        evidencia["bytesIndices"] = tamanos;
        var salida = Path.Combine(Raiz(), ".artifacts", "portal", "medicion-2000-20000.json");
        Directory.CreateDirectory(Path.GetDirectoryName(salida)!);
        await File.WriteAllTextAsync(salida, JsonSerializer.Serialize(evidencia, new JsonSerializerOptions { WriteIndented = true }), ct);
    }

    private static async Task<JsonElement> PlanAsync(NpgsqlConnection conexion, string sql, NpgsqlTransaction? tx, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + sql, conexion, tx) { CommandTimeout = 180 };
        using var doc = JsonDocument.Parse((string)(await cmd.ExecuteScalarAsync(ct))!);
        return doc.RootElement.Clone();
    }

    private static async Task EjecutarAsync(NpgsqlConnection conexion, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conexion) { CommandTimeout = 180 };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string Raiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null) { if (File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) return d.FullName; d = d.Parent; }
        throw new DirectoryNotFoundException();
    }
}
