using ArsDocendi.IntegrationTests.Infraestructura;
using System.Net.Http.Json;
using Modules.Tareas.Application;

namespace ArsDocendi.IntegrationTests.Tareas;

public sealed class IntegridadTareasHttpTests(PostgresFixture postgres) : TareasHttpBase(postgres)
{
    [Fact]
    public async Task Listado_solo_resuelve_identidades_y_relaciones_visibles()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = new FabricaLecturas(Cadena);
        using var cliente = Cliente(host, Docente, "docente");
        var tareas = await cliente.GetFromJsonAsync<TareaDto[]>("/api/tareas", ct);
        Assert.NotEmpty(tareas!);
        Assert.DoesNotContain(host.Captura.Sql, sql => sql.Contains("FROM identity.users", StringComparison.Ordinal) && !sql.Contains("WHERE", StringComparison.Ordinal));
        Assert.DoesNotContain(host.Captura.Sql, sql => sql.Contains("FROM tareas.tarea_relaciones", StringComparison.Ordinal) && !sql.Contains("WHERE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Busqueda_http_filtra_mas_de_cincuenta_elegibles_antes_del_limite()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        var objetivo = Guid.NewGuid();
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new Npgsql.NpgsqlCommand("""
            INSERT INTO identity.users(upn,display_name)
              SELECT 'candidato-extra-'||n||'@example.invalid','AAA candidato '||n FROM generate_series(1,60) n;
            INSERT INTO identity.user_roles(user_id,role_id)
              SELECT u.id,r.id FROM identity.users u CROSS JOIN identity.roles r
              WHERE u.upn LIKE 'candidato-extra-%' AND r.code='administrativo';
            INSERT INTO identity.users(id,upn,display_name)
              VALUES (@objetivo,'objetivo-singular@example.invalid','ZZ Objetivo singular');
            INSERT INTO identity.user_roles(user_id,role_id)
              SELECT @objetivo,id FROM identity.roles WHERE code='administrativo';
            """, conexion))
        {
            comando.Parameters.AddWithValue("objetivo", objetivo);
            await comando.ExecuteNonQueryAsync(ct);
        }

        using var host = new FabricaLecturas(Cadena);
        using var cliente = Cliente(host, Decanato, "decanato");
        var resultado = await cliente.GetFromJsonAsync<CandidatoResponsableDto[]>(
            "/api/tareas/candidatos?q=objetivo%20singular", ct);
        Assert.Equal(objetivo, Assert.Single(resultado!).Id);
        var sql = host.Captura.Sql.Where(consulta => consulta.Contains("FROM identity.users", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Contains(sql, consulta => consulta.Contains("regexp_split_to_table", StringComparison.OrdinalIgnoreCase)
            && consulta.Contains("strpos", StringComparison.OrdinalIgnoreCase)
            && consulta.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Mutacion_sin_version_se_rechaza_y_no_agrega_historial()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var tarea = await Crear(cliente, NuevaTarea(Secretaria), ct);
        await Esperar(cliente.PostAsJsonAsync($"/api/tareas/{tarea.Id}/estado", new { estado = "cancelada" }, ct), System.Net.HttpStatusCode.BadRequest);
        Assert.Equal(tarea.Historial.Count, (await cliente.GetFromJsonAsync<TareaDto>($"/api/tareas/{tarea.Id}", ct))!.Historial.Count);
    }

    [Fact]
    public async Task Hija_rechazada_si_proyecto_efectivo_esta_cerrado()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var padre = await Crear(cliente, NuevaTarea(Secretaria) with { ProyectoId = ProyectoTesting }, ct);
        await Esperar(cliente.PostVersionadoAsync($"/api/tareas/proyectos/{ProyectoTesting}/estado", new { estado = "finalizado" }, ct), System.Net.HttpStatusCode.OK);
        await Esperar(cliente.PostAsJsonAsync("/api/tareas", NuevaTarea(Secretaria) with { TareaPadreId = padre.Id }, ct), System.Net.HttpStatusCode.UnprocessableEntity);
    }

}
