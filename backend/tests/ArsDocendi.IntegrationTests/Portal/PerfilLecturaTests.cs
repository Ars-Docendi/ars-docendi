using System.Data.Common;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Modules.Portal.Infrastructure;
using Modules.Portal.Repositories;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

public sealed class PerfilLecturaTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "portal_lectura")
{
    [Fact]
    public async Task Perfil_completo_separa_colecciones_y_lee_snapshot_repeatable_read()
    {
        var ct = TestContext.Current.CancellationToken;
        var persona = Guid.NewGuid();
        await using var conexion = await AbrirConexionAsync();
        await using var sembrar = new NpgsqlCommand("""
            INSERT INTO portal.perfiles(id, persona_id) VALUES (@id,@id);
            INSERT INTO portal.experiencias(perfil_id,puesto,organizacion,descripcion,desde)
              SELECT @id,'Puesto','Organización','Descripción','2020-01-01'::date FROM generate_series(1,3);
            INSERT INTO portal.educaciones(perfil_id,nivel,carrera,institucion,desde)
              SELECT @id,'Grado','Carrera','Institución','2020-01-01'::date FROM generate_series(1,3);
            INSERT INTO portal.certificaciones(perfil_id,nombre,emisor,fecha)
              SELECT @id,'Certificación','Emisor','2020-01-01'::date FROM generate_series(1,3);
            INSERT INTO portal.proyectos(perfil_id,nombre,rol,descripcion,desde)
              SELECT @id,'Proyecto','Rol','Descripción','2020-01-01'::date FROM generate_series(1,2);
            INSERT INTO portal.habilidades(id,termino,termino_norm)
              SELECT gen_random_uuid(),'Término '||n,'TERMINO '||n FROM generate_series(1,4) n;
            INSERT INTO portal.docente_habilidades(perfil_id,habilidad_id,tipo)
              SELECT @id,id,'habilidad' FROM portal.habilidades WHERE termino_norm LIKE 'TERMINO %';
            """, conexion);
        sembrar.Parameters.AddWithValue("id", persona);
        await sembrar.ExecuteNonQueryAsync(ct);
        var captura = new CapturaConsultas();
        await using var db = new PortalDbContext(new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql(Cadena).AddInterceptors(captura).Options);
        // Consulta anterior: mismas secciones, sin el split; se mide el SQL emitido por EF.
        var legado = db.Perfiles.Include(p => p.Contacto).Include(p => p.Cv)
            .Include(p => p.Experiencias).Include(p => p.Educaciones).Include(p => p.Certificaciones)
            .Include(p => p.Proyectos).ThenInclude(p => p.Documento)
            .Include(p => p.Habilidades).ThenInclude(h => h.Habilidad)
            .Where(p => p.PersonaId == persona).AsSingleQuery().ToQueryString();
        await using (var planLegado = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + legado, conexion))
        {
            foreach (System.Text.RegularExpressions.Match parametro in System.Text.RegularExpressions.Regex.Matches(legado, "@[A-Za-z0-9_]+"))
                if (!planLegado.Parameters.Contains(parametro.Value)) planLegado.Parameters.AddWithValue(parametro.Value, persona);
            var jsonLegado = (string)(await planLegado.ExecuteScalarAsync(ct))!;
            using var documentoLegado = System.Text.Json.JsonDocument.Parse(jsonLegado);
            Assert.Equal(216d, documentoLegado.RootElement[0].GetProperty("Plan").GetProperty("Actual Rows").GetDouble());
            var destino = Path.Combine(Raiz(), ".artifacts", "portal"); Directory.CreateDirectory(destino);
            await File.WriteAllTextAsync(Path.Combine(destino, "perfil-single-sql.txt"), legado, ct);
            await File.WriteAllTextAsync(Path.Combine(destino, "perfil-single-plan.json"), jsonLegado, ct);
        }
        captura.Intercalar = async token =>
        {
            await using var externa = await AbrirConexionAsync();
            await using var insertar = new NpgsqlCommand("INSERT INTO portal.experiencias(perfil_id,puesto,organizacion,descripcion,desde) VALUES (@id,'Concurrente','Org','Desc','2020-01-01')", externa);
            insertar.Parameters.AddWithValue("id", persona); await insertar.ExecuteNonQueryAsync(token);
        };
        var perfil = await new RepositorioPortal(db).ObtenerAsync(persona, ct);
        Assert.NotNull(perfil);
        Assert.Equal(3, perfil.Experiencias.Count);
        Assert.Equal(3, perfil.Educaciones.Count);
        Assert.Equal(3, perfil.Certificaciones.Count);
        Assert.Equal(2, perfil.Proyectos.Count);
        Assert.Equal(4, perfil.Habilidades.Count);
        Assert.Equal(6, captura.Sql.Count);
        Assert.All(captura.Aislamientos, nivel => Assert.Equal(System.Data.IsolationLevel.RepeatableRead, nivel));
        var planes = new List<System.Text.Json.JsonElement>();
        foreach (var sql in captura.Sql)
        {
            await using var plan = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + sql, conexion);
            foreach (System.Text.RegularExpressions.Match parametro in System.Text.RegularExpressions.Regex.Matches(sql, "@[A-Za-z0-9_]+"))
                if (!plan.Parameters.Contains(parametro.Value)) plan.Parameters.AddWithValue(parametro.Value, persona);
            var json = (string)(await plan.ExecuteScalarAsync(ct))!;
            using var documento = System.Text.Json.JsonDocument.Parse(json);
            planes.Add(documento.RootElement.Clone());
            Assert.InRange(documento.RootElement[0].GetProperty("Plan").GetProperty("Actual Rows").GetDouble(), 1, 4);
        }
        await File.WriteAllTextAsync(Path.Combine(Raiz(), ".artifacts/portal/perfil-split-sql.txt"), string.Join("\n\n-- siguiente round trip\n\n", captura.Sql), ct);
        await File.WriteAllTextAsync(Path.Combine(Raiz(), ".artifacts/portal/perfil-split-planes.json"), System.Text.Json.JsonSerializer.Serialize(planes), ct);
        Assert.Equal(4, await db.Experiencias.CountAsync(p => p.PerfilId == persona, ct));
    }

    private static string Raiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null) { if (File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) return d.FullName; d = d.Parent; }
        throw new DirectoryNotFoundException();
    }
}

internal sealed class CapturaConsultas : DbCommandInterceptor
{
    public List<string> Sql { get; } = [];
    public List<System.Data.IsolationLevel?> Aislamientos { get; } = [];
    public Func<CancellationToken, Task>? Intercalar { get; set; }
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Sql.Add(command.CommandText);
        Aislamientos.Add(command.Transaction?.IsolationLevel);
        if (Sql.Count == 2 && Intercalar is not null) await Intercalar(cancellationToken);
        return result;
    }
}
