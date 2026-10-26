using ArsDocendi.IntegrationTests.Infraestructura;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modules.Tareas.Application;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Tareas;

public sealed class CierreTareasHttpTests(PostgresFixture postgres) : TareasHttpBase(postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelacion_o_reasignacion_confirmada_durante_mutacion_no_usa_guard_obsoleto(bool reasignar)
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var autoridad = Cliente(host, Decanato, "decanato");
        using var responsable = Cliente(host, Docente, "docente");
        var tarea = await Crear(autoridad, NuevaTarea(Docente), ct);
        var ruta = $"/api/tareas/{tarea.Id}";
        // El escritor ganador mantiene UPDATE sin confirmar; el HTTP perdedor
        // ya salió con la versión observada y queda esperando el guard real.
        await using var ganador = await AbrirConexionAsync();
        await using var tx = await ganador.BeginTransactionAsync(ct);
        await using var cambio = new NpgsqlCommand(reasignar
            ? "UPDATE tareas.tareas SET responsable_id=@responsable WHERE id=@id"
            : "UPDATE tareas.tareas SET estado_id=(SELECT id FROM tareas.estados_tarea WHERE codigo='cancelada') WHERE id=@id", ganador, tx);
        cambio.Parameters.AddWithValue("id", tarea.Id);
        if (reasignar) cambio.Parameters.AddWithValue("responsable", JefeCatedra);
        await cambio.ExecuteNonQueryAsync(ct);
        var pendiente = responsable.PostAsJsonAsync($"{ruta}/estado", new { estado = "en_curso", version = tarea.Version }, ct);
        try { await EsperarBloqueadasAsync(1, ct); }
        finally { await tx.CommitAsync(ct); }
        using var respuesta = await pendiente;
        // Reasignar oculta la entidad a su anterior Responsable: 404 antes de versión.
        Assert.Equal(reasignar ? HttpStatusCode.NotFound : HttpStatusCode.Conflict, respuesta.StatusCode);
        if (!reasignar) Assert.Contains("concurrency-conflict", await respuesta.Content.ReadAsStringAsync(ct));
        var actual = (await autoridad.GetFromJsonAsync<TareaDto>(ruta, ct))!;
        Assert.Equal(reasignar ? JefeCatedra : Docente, actual.Responsable.Id);
        Assert.Equal(reasignar ? "pendiente" : "cancelada", actual.Estado);
        Assert.Equal(tarea.Historial.Count, actual.Historial.Count);
        Assert.NotEqual(tarea.Version, actual.Version);
    }

    [Fact]
    public async Task Cierre_confirmado_mientras_hija_espera_revalida_proyecto_efectivo()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var padre = await Crear(cliente, NuevaTarea(Secretaria) with { ProyectoId = ProyectoTesting }, ct);
        await using var cierre = await AbrirConexionAsync();
        await using var tx = await cierre.BeginTransactionAsync(ct);
        await using var comando = new NpgsqlCommand("UPDATE tareas.proyectos SET estado_id=(SELECT id FROM tareas.estados_proyecto WHERE codigo='finalizado') WHERE id=@id", cierre, tx);
        comando.Parameters.AddWithValue("id", ProyectoTesting);
        await comando.ExecuteNonQueryAsync(ct);
        var alta = cliente.PostAsJsonAsync("/api/tareas", NuevaTarea(Secretaria) with { TareaPadreId = padre.Id }, ct);
        try { await EsperarBloqueadasAsync(1, ct); }
        finally { await tx.CommitAsync(ct); }
        await Esperar(alta, HttpStatusCode.UnprocessableEntity);
        // Conservar proyecto cerrado sigue permitido.
        await Esperar(cliente.PutAsJsonAsync($"/api/tareas/{padre.Id}", new
        {
            titulo = "Conservado", descripcion = "", fechaInicio = "2026-01-01", fechaFin = "2026-12-31",
            prioridad = "media", tipo = "administrativa", responsableId = Secretaria, proyectoId = ProyectoTesting, version = padre.Version,
        }, ct), HttpStatusCode.OK);
        await using var verificar = new NpgsqlCommand("SELECT count(*) FROM tareas.tareas WHERE tarea_padre_id=@id", cierre);
        verificar.Parameters.AddWithValue("id", padre.Id);
        Assert.Equal(0L, await verificar.ExecuteScalarAsync(ct));
    }

    [Fact]
    public async Task Elegibilidad_usa_bandera_de_catalogo_y_no_nombre_de_estado()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("UPDATE tareas.estados_proyecto SET admite_tareas=false WHERE codigo='abierto'; UPDATE tareas.estados_proyecto SET admite_tareas=true WHERE codigo='finalizado'", conexion);
        await comando.ExecuteNonQueryAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        await Esperar(cliente.PostAsJsonAsync("/api/tareas", NuevaTarea(Secretaria) with { ProyectoId = ProyectoTesting }, ct), HttpStatusCode.UnprocessableEntity);
        var padre = await Crear(cliente, NuevaTarea(Secretaria) with { ProyectoId = ProyectoFinalizado }, ct);
        var hija = await Crear(cliente, NuevaTarea(Secretaria) with { TareaPadreId = padre.Id }, ct);
        Assert.Equal(ProyectoFinalizado, hija.ProyectoId);
    }

    [Fact]
    public async Task P_a_Q_y_NULL_preserva_campos_propios_y_versiona_cuatro_niveles()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var q = await ReadAsync<ProyectoTareasDto>(await cliente.PostAsJsonAsync("/api/tareas/proyectos", new
        {
            nombre = "Q", fechaInicio = "2026-01-01", fechaFin = "2026-12-31", responsableId = Secretaria,
        }, ct), ct);
        var raiz = await Crear(cliente, NuevaTarea(Secretaria) with { ProyectoId = ProyectoTesting }, ct);
        var anterior = raiz;
        var originales = new List<TareaDto> { raiz };
        for (var i = 0; i < 3; i++)
        {
            var hija = await Crear(cliente, NuevaTarea(JefeCatedra) with { TareaPadreId = anterior.Id }, ct);
            await Esperar(cliente.PatchVersionadoAsync($"/api/tareas/{hija.Id}/avance", new { porcentajeAvance = 25 + i }, ct), HttpStatusCode.OK);
            hija = await Cambiar(cliente, $"/api/tareas/{hija.Id}", new { estado = "en_curso" }, ct);
            originales.Add(hija);
            anterior = hija;
        }
        foreach (var proyecto in new Guid?[] { q.Id, null })
        {
            await Esperar(cliente.PutVersionadoAsync($"/api/tareas/{raiz.Id}", EdicionDe(Secretaria) with { ProyectoId = proyecto }, ct), HttpStatusCode.OK);
            foreach (var original in originales)
            {
                var actual = (await cliente.GetFromJsonAsync<TareaDto>($"/api/tareas/{original.Id}", ct))!;
                Assert.Equal(proyecto, actual.ProyectoId);
                Assert.Equal(original.Responsable, actual.Responsable);
                Assert.Equal(original.Estado, actual.Estado);
                Assert.Equal(original.PorcentajeAvance, actual.PorcentajeAvance);
                Assert.NotEqual(original.Version, actual.Version);
            }
        }
        var sinProyecto = await Crear(cliente, NuevaTarea(Secretaria), ct);
        Assert.Null((await Crear(cliente, NuevaTarea(JefeCatedra) with { TareaPadreId = sinProyecto.Id }, ct)).ProyectoId);
    }

    [Fact]
    public async Task Fallo_al_commit_del_traslado_revierte_tareas_historial_y_auditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var raiz = await Crear(cliente, NuevaTarea(Secretaria) with { ProyectoId = ProyectoTesting }, ct);
        var hija = await Crear(cliente, NuevaTarea(JefeCatedra) with { TareaPadreId = raiz.Id }, ct);
        await using var conexion = await AbrirConexionAsync();
        const string huellaSql = "SELECT jsonb_build_object('tareas',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM tareas.tareas t),'historial',(SELECT jsonb_agg(to_jsonb(h) ORDER BY id) FROM tareas.tarea_historial h),'audit',(SELECT count(*) FROM audit.change_log))::text";
        await using var huella = new NpgsqlCommand(huellaSql, conexion);
        var antes = await huella.ExecuteScalarAsync(ct);
        await using var inyectar = new NpgsqlCommand("""
            CREATE FUNCTION tareas.fallo_commit_test() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'fallo-inyectado-test' USING ERRCODE='23514'; END $$;
            CREATE CONSTRAINT TRIGGER fallo_commit_test AFTER UPDATE ON tareas.tareas
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION tareas.fallo_commit_test();
            """, conexion);
        await inyectar.ExecuteNonQueryAsync(ct);
        using var respuesta = await cliente.PutVersionadoAsync($"/api/tareas/{raiz.Id}", EdicionDe(Secretaria) with { ProyectoId = null }, ct);
        Assert.False(respuesta.IsSuccessStatusCode);
        Assert.Equal(antes, await huella.ExecuteScalarAsync(ct));
        Assert.Equal(ProyectoTesting, (await cliente.GetFromJsonAsync<TareaDto>($"/api/tareas/{hija.Id}", ct))!.ProyectoId);
    }

    [Fact]
    public async Task Proyecto_rechaza_version_ausente_y_obsoleta_sin_perder_edicion()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var ruta = $"/api/tareas/proyectos/{ProyectoTesting}";
        var original = (await cliente.GetFromJsonAsync<ProyectoTareasDto>(ruta, ct))!;
        await Esperar(cliente.PostAsJsonAsync($"{ruta}/estado", new { estado = "finalizado" }, ct), HttpStatusCode.BadRequest);
        await Esperar(cliente.PutAsJsonAsync(ruta, new
        {
            nombre = "Confirmado", fechaInicio = "2026-01-01", fechaFin = "2026-12-31", responsableId = Secretaria, version = original.Version,
        }, ct), HttpStatusCode.OK);
        using var conflicto = await cliente.PostAsJsonAsync($"{ruta}/estado", new { estado = "finalizado", version = original.Version }, ct);
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        Assert.Contains("concurrency-conflict", await conflicto.Content.ReadAsStringAsync(ct));
        Assert.Equal("Confirmado", (await cliente.GetFromJsonAsync<ProyectoTareasDto>(ruta, ct))!.Nombre);
        Assert.Equal("abierto", (await cliente.GetFromJsonAsync<ProyectoTareasDto>(ruta, ct))!.Estado);
    }

    [Fact]
    public async Task Quitar_relacion_ajena_oculta_ambos_extremos_con_404()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var docente = Cliente(host, Docente, "docente");
        await Esperar(docente.DeleteAsync($"/api/tareas/{TareaDelDocente}/relaciones/{TareaSemilla}", ct), HttpStatusCode.NotFound);
    }
}
