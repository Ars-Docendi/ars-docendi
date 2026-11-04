using ArsDocendi.IntegrationTests.Infraestructura;
using System.Net;
using System.Net.Http.Json;
using Modules.Tareas.Application;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Tareas;

public sealed class ConcurrenciaTareasHttpTests(PostgresFixture postgres) : TareasHttpBase(postgres)
{
    [Fact]
    public async Task Relaciones_concurrentes_mismo_par_son_idempotentes()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var a = await Crear(cliente, NuevaTarea(Secretaria), ct);
        var b = await Crear(cliente, NuevaTarea(Secretaria), ct);
        await using var bloqueo = await AbrirConexionAsync();
        await using (var comando = new NpgsqlCommand("""
            CREATE FUNCTION tareas.barrera_test() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(520054); RETURN NEW; END $$;
            CREATE TRIGGER barrera_test BEFORE INSERT ON tareas.tarea_relaciones FOR EACH ROW EXECUTE FUNCTION tareas.barrera_test();
            SELECT pg_advisory_lock(520054);
            """, bloqueo)) await comando.ExecuteNonQueryAsync(ct);
        var primera = cliente.PostAsJsonAsync($"/api/tareas/{a.Id}/relaciones", new { otraTareaId = b.Id }, ct);
        var segunda = cliente.PostAsJsonAsync($"/api/tareas/{b.Id}/relaciones", new { otraTareaId = a.Id }, ct);
        try { await EsperarBloqueadasAsync(2, ct); }
        finally
        {
            await using var liberar = new NpgsqlCommand("SELECT pg_advisory_unlock(520054)", bloqueo);
            await liberar.ExecuteNonQueryAsync(ct);
        }
        await Esperar(primera, HttpStatusCode.NoContent);
        await Esperar(segunda, HttpStatusCode.NoContent);
        Assert.Single((await cliente.GetFromJsonAsync<TareaDto>($"/api/tareas/{a.Id}", ct))!.TareasRelacionadasIds);
        await Esperar(cliente.DeleteAsync($"/api/tareas/{a.Id}/relaciones/{b.Id}", ct), HttpStatusCode.NoContent);
        await Esperar(cliente.DeleteAsync($"/api/tareas/{a.Id}/relaciones/{b.Id}", ct), HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Alta_http_protege_responsable_hasta_commit_y_conserva_historia_inactiva()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        await using var bloqueo = await AbrirConexionAsync();
        await using (var comando = new NpgsqlCommand("""
            CREATE FUNCTION tareas.barrera_destino_test() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(520055); RETURN NEW; END $$;
            CREATE TRIGGER barrera_destino_test BEFORE INSERT ON tareas.tareas FOR EACH ROW EXECUTE FUNCTION tareas.barrera_destino_test();
            SELECT pg_advisory_lock(520055);
            """, bloqueo)) await comando.ExecuteNonQueryAsync(ct);
        var alta = cliente.PostAsJsonAsync("/api/tareas", NuevaTarea(Secretaria), ct);
        await using var mantenimiento = await AbrirConexionAsync();
        await EsperarBloqueadasAsync(1, ct);
        await using var desactivar = new NpgsqlCommand("UPDATE identity.users SET is_active=false WHERE id=@id", mantenimiento);
        desactivar.Parameters.AddWithValue("id", Secretaria);
        var cambio = desactivar.ExecuteNonQueryAsync(ct);
        using var esperaCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var espera = EsperarBloqueadasAsync(2, esperaCt.Token);
        try
        {
            Assert.Same(espera, await Task.WhenAny(cambio, espera));
            await espera;
            Assert.False(cambio.IsCompleted);
        }
        finally
        {
            esperaCt.Cancel();
            try { await espera; } catch (OperationCanceledException) { }
            await using var liberar = new NpgsqlCommand("SELECT pg_advisory_unlock(520055)", bloqueo);
            await liberar.ExecuteNonQueryAsync(ct);
        }
        using var respuesta = await alta;
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creada = (await respuesta.Content.ReadFromJsonAsync<TareaDto>(ct))!;
        Assert.Equal(1, await cambio);
        var historica = (await cliente.GetFromJsonAsync<TareaDto>($"/api/tareas/{creada.Id}", ct))!;
        Assert.Equal(creada.Responsable.Id, historica.Responsable.Id);
        Assert.Equal(creada.Responsable.Nombre, historica.Responsable.Nombre);
        Assert.NotEmpty(historica.Historial);
    }

    [Fact]
    public async Task Alta_http_de_proyecto_protege_responsable_hasta_commit_y_conserva_historia_inactiva()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        await using var bloqueo = await AbrirConexionAsync();
        await using (var comando = new NpgsqlCommand("""
            CREATE FUNCTION tareas.barrera_proyecto_destino_test() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(520056); RETURN NEW; END $$;
            CREATE TRIGGER barrera_proyecto_destino_test BEFORE INSERT ON tareas.proyectos
                FOR EACH ROW EXECUTE FUNCTION tareas.barrera_proyecto_destino_test();
            SELECT pg_advisory_lock(520056);
            """, bloqueo)) await comando.ExecuteNonQueryAsync(ct);
        var alta = cliente.PostVersionadoAsync("/api/tareas/proyectos", new
        {
            nombre = "Proyecto con responsable concurrente",
            descripcion = "Sintético",
            fechaInicio = "2026-01-01",
            fechaFin = "2026-12-31",
            responsableId = Secretaria,
        }, ct);
        await EsperarBloqueadasAsync(1, ct);
        await using var mantenimiento = await AbrirConexionAsync();
        await using var desactivar = new NpgsqlCommand(
            "UPDATE identity.users SET is_active=false WHERE id=@id", mantenimiento);
        desactivar.Parameters.AddWithValue("id", Secretaria);
        var cambio = desactivar.ExecuteNonQueryAsync(ct);
        using var esperaCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var espera = EsperarBloqueadasAsync(2, esperaCt.Token);
        try
        {
            Assert.Same(espera, await Task.WhenAny(cambio, espera));
            await espera;
            Assert.False(cambio.IsCompleted);
        }
        finally
        {
            esperaCt.Cancel();
            try { await espera; } catch (OperationCanceledException) { }
            await using var liberar = new NpgsqlCommand("SELECT pg_advisory_unlock(520056)", bloqueo);
            await liberar.ExecuteNonQueryAsync(ct);
        }

        using var respuesta = await alta;
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = (await respuesta.Content.ReadFromJsonAsync<ProyectoTareasDto>(ct))!;
        Assert.Equal(1, await cambio);
        Assert.Equal(Secretaria, creado.Responsable.Id);
        var historico = (await cliente.GetFromJsonAsync<ProyectoTareasDto[]>("/api/tareas/proyectos", ct))!
            .Single(proyecto => proyecto.Id == creado.Id);
        Assert.Equal(creado.Responsable.Id, historico.Responsable.Id);
        Assert.Equal(creado.Responsable.Nombre, historico.Responsable.Nombre);
    }

    [Fact]
    public async Task Dos_ediciones_con_version_observada_confirman_solo_una()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var cliente = Cliente(host, Decanato, "decanato");
        var tarea = await Crear(cliente, NuevaTarea(Secretaria), ct);
        var ruta = $"/api/tareas/{tarea.Id}";
        await using var bloqueo = await AbrirConexionAsync();
        await using var transaccion = await bloqueo.BeginTransactionAsync(ct);
        await using (var comando = new NpgsqlCommand($"SELECT pg_advisory_xact_lock(hashtextextended('{tarea.Id}',52005))", bloqueo, transaccion))
            await comando.ExecuteNonQueryAsync(ct);
        var datos = new { titulo = "Edición", descripcion = "", fechaInicio = "2026-01-01", fechaFin = "2026-12-31", prioridad = "media", tipo = "administrativa", responsableId = Secretaria, version = tarea.Version };
        var primera = cliente.PutAsJsonAsync(ruta, datos, ct);
        var segunda = cliente.PutAsJsonAsync(ruta, datos with { titulo = "Otra" }, ct);
        await EsperarBloqueadasAsync(2, ct);
        await transaccion.CommitAsync(ct);
        using var a = await primera;
        using var b = await segunda;
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, new[] { a.StatusCode, b.StatusCode }.Order());
        var actual = await cliente.GetFromJsonAsync<TareaDto>(ruta, ct);
        Assert.Equal(tarea.Historial.Count + 1, actual!.Historial.Count);
        Assert.NotEqual(tarea.Version, actual.Version);
    }
}
