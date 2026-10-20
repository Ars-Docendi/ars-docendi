using System.Net.Http.Json;
using Modules.Tareas.Application;

namespace ArsDocendi.IntegrationTests.Tareas;

public sealed partial class TareasHttpTests
{
    [Fact]
    public async Task Ping_del_modulo_es_publico_y_responde_estado_ok()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        using var respuesta = await cliente.GetAsync("/api/tareas/ping", ct);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("\"module\":\"tareas\"", cuerpo, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"ok\"", cuerpo, StringComparison.Ordinal);
    }

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
    public async Task Listado_estable_con_usuarios_y_relaciones_ajenos_crecientes()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = new FabricaLecturas(Cadena);
        using var cliente = Cliente(host, Docente, "docente");
        var antes = await cliente.GetFromJsonAsync<TareaDto[]>("/api/tareas", ct);
        Assert.NotEmpty(antes!);

        await using (var conexion = await AbrirConexionAsync())
        await using (var expandir = new Npgsql.NpgsqlCommand("""
            INSERT INTO identity.users(upn,display_name)
              SELECT 'ajeno-'||n||'@example.invalid','0000 Ajeno '||n FROM generate_series(1,500) n;
            WITH base AS (
              SELECT * FROM tareas.tareas WHERE id=@base
            ), creadas AS (
              INSERT INTO tareas.tareas(id,titulo,descripcion,fecha_inicio,fecha_fin,prioridad_id,tipo_id,estado_id,
                                        porcentaje_avance,solucion,responsable_id,creado_por_id,proyecto_id,tarea_padre_id)
              SELECT gen_random_uuid(),'Tarea ajena '||n,b.descripcion,b.fecha_inicio,b.fecha_fin,b.prioridad_id,b.tipo_id,
                     b.estado_id,b.porcentaje_avance,b.solucion,@responsable,@creador,NULL,NULL
              FROM generate_series(1,120) n CROSS JOIN base b
              RETURNING id
            ), numeradas AS (
              SELECT id,row_number() OVER (ORDER BY id) AS numero FROM creadas
            )
            INSERT INTO tareas.tarea_relaciones(tarea_id,relacionada_id)
            SELECT a.id,b.id FROM numeradas a JOIN numeradas b ON b.numero=a.numero+1 WHERE a.numero % 2 = 1;
            """, conexion))
        {
            expandir.Parameters.AddWithValue("base", TareaSemilla);
            expandir.Parameters.AddWithValue("responsable", Secretaria);
            expandir.Parameters.AddWithValue("creador", Decanato);
            await expandir.ExecuteNonQueryAsync(ct);
        }

        while (host.Captura.Sql.TryDequeue(out _)) { }
        var despues = await cliente.GetFromJsonAsync<TareaDto[]>("/api/tareas", ct);
        Assert.Equal(antes!.Select(t => t.Id), despues!.Select(t => t.Id));
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(antes),
            System.Text.Json.JsonSerializer.Serialize(despues));

        var sql = host.Captura.Sql.ToArray();
        Assert.DoesNotContain(sql, consulta => consulta.Contains("FROM identity.users", StringComparison.OrdinalIgnoreCase)
            && !consulta.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(sql, consulta => consulta.Contains("FROM tareas.tarea_relaciones", StringComparison.OrdinalIgnoreCase)
            && !consulta.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sql, consulta => consulta.Contains("FROM identity.users", StringComparison.OrdinalIgnoreCase)
            && consulta.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sql, consulta => consulta.Contains("FROM tareas.tarea_relaciones", StringComparison.OrdinalIgnoreCase)
            && consulta.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task Trasladar_raiz_propaga_proyecto_a_cuatro_niveles_y_NULL()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var raiz = await Crear(cliente, NuevaTarea(Secretaria) with { ProyectoId = ProyectoTesting }, ct);
        var anterior = raiz;
        var descendientes = new List<TareaDto>();
        for (var nivel = 0; nivel < 3; nivel++)
        {
            anterior = await Crear(cliente, NuevaTarea(JefeCatedra) with { TareaPadreId = anterior.Id }, ct);
            descendientes.Add(anterior);
        }
        foreach (var proyecto in new Guid?[] { null, ProyectoTesting })
        {
            await ReadAsync<TareaDto>(await cliente.PutVersionadoAsync($"/api/tareas/{raiz.Id}",
                EdicionDe(Secretaria) with { ProyectoId = proyecto }, ct), ct);
            foreach (var original in descendientes)
            {
                var actual = await cliente.GetFromJsonAsync<TareaDto>($"/api/tareas/{original.Id}", ct);
                Assert.Equal(proyecto, actual!.ProyectoId);
                Assert.Equal(original.Responsable, actual.Responsable);
                Assert.Equal(original.Estado, actual.Estado);
                Assert.Equal(original.PorcentajeAvance, actual.PorcentajeAvance);
            }
        }
    }
}
