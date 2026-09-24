using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Tareas.Application;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Tareas;

public sealed class TareasHttpTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "tareas_http")
{
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid JefeCatedra = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Decanato = Guid.Parse("a0000000-0000-4000-8000-000000000005");
    private static readonly Guid Administrador = Guid.Parse("a0000000-0000-4000-8000-000000000007");
    private static readonly Guid Administrativo = Guid.Parse("a0000000-0000-4000-8000-000000000006");
    private static readonly Guid ProyectoTesting = Guid.Parse("f2000000-0000-4000-8000-000000000001");
    private static readonly Guid TareaSemilla = Guid.Parse("f3000000-0000-4000-8000-000000000001");

    [Fact]
    public async Task Todos_los_roles_ven_las_tareas_pero_solo_las_autoridades_crean()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();

        foreach (var (usuario, rol) in new[]
        {
            (Docente, "docente"), (JefeCatedra, "jefe_catedra"), (Coordinador, "coordinador_carrera"),
            (Secretaria, "secretaria"), (Decanato, "decanato"), (Administrativo, "administrativo"),
        })
        {
            using var cliente = Cliente(host, usuario, rol);
            var tareas = await cliente.GetFromJsonAsync<TareaDto[]>("/api/tareas", ct);
            Assert.NotNull(tareas);
            Assert.NotEmpty(tareas);
            Assert.All(tareas, t => Assert.Empty(t.Historial));
        }

        foreach (var (usuario, rol) in new[]
        {
            (Docente, "docente"), (JefeCatedra, "jefe_catedra"), (Coordinador, "coordinador_carrera"),
        })
        {
            using var cliente = Cliente(host, usuario, rol);
            await Esperar(cliente.PostAsJsonAsync("/api/tareas", NuevaTarea(JefeCatedra), ct), HttpStatusCode.Forbidden);
            await Esperar(cliente.GetAsync("/api/tareas/candidatos", ct), HttpStatusCode.Forbidden);
        }

        foreach (var (usuario, rol) in new[]
        {
            (Secretaria, "secretaria"), (Decanato, "decanato"), (Administrativo, "administrativo"),
        })
        {
            using var cliente = Cliente(host, usuario, rol);
            await Esperar(cliente.PostAsJsonAsync("/api/tareas", NuevaTarea(Docente), ct), HttpStatusCode.Created);
        }
    }

    [Fact]
    public async Task La_jerarquia_de_asignacion_se_exige_en_el_servidor()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var secretaria = Cliente(host, Secretaria, "secretaria");
        using var decanato = Cliente(host, Decanato, "decanato");
        using var administrativo = Cliente(host, Administrativo, "administrativo");

        var candidatosSecretaria = await secretaria.GetFromJsonAsync<PersonaTareaDto[]>("/api/tareas/candidatos", ct);
        Assert.DoesNotContain(candidatosSecretaria!, c => c.Id == Decanato);
        Assert.Contains(candidatosSecretaria!, c => c.Id == Administrativo && c.Rol == "Administrativo");

        var candidatosDecanato = await decanato.GetFromJsonAsync<PersonaTareaDto[]>("/api/tareas/candidatos", ct);
        Assert.Contains(candidatosDecanato!, c => c.Id == Secretaria);

        var candidatosProyecto = await decanato.GetFromJsonAsync<PersonaTareaDto[]>("/api/tareas/candidatos?para=proyecto", ct);
        Assert.All(candidatosProyecto!, c => Assert.Contains(c.Rol, new[] { "Decanato", "Secretaría Académica" }));

        await Esperar(secretaria.PostAsJsonAsync("/api/tareas", NuevaTarea(Decanato), ct), HttpStatusCode.UnprocessableEntity);
        await Esperar(secretaria.PostAsJsonAsync("/api/tareas", NuevaTarea(Secretaria), ct), HttpStatusCode.Created);
        await Esperar(administrativo.PostAsJsonAsync("/api/tareas", NuevaTarea(Secretaria), ct), HttpStatusCode.UnprocessableEntity);
        await Esperar(decanato.PostAsJsonAsync("/api/tareas", NuevaTarea(Secretaria), ct), HttpStatusCode.Created);

        // Reasignar hacia arriba también se rechaza (tarea sembrada, creada por Secretaría).
        var edicion = EdicionDe(Decanato);
        await Esperar(secretaria.PutAsJsonAsync($"/api/tareas/{TareaSemilla}", edicion, ct), HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task El_ciclo_de_estados_y_los_permisos_del_responsable_y_la_autoridad()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var secretaria = Cliente(host, Secretaria, "secretaria");
        using var responsable = Cliente(host, JefeCatedra, "jefe_catedra");
        using var ajeno = Cliente(host, Coordinador, "coordinador_carrera");

        var creada = await Crear(secretaria, NuevaTarea(JefeCatedra), ct);
        Assert.Equal("pendiente", creada.Estado);
        Assert.Equal(0, creada.PorcentajeAvance);
        Assert.Equal(Secretaria, creada.CreadoPor.Id);
        Assert.Equal("Secretaría Académica", creada.CreadoPor.Rol);
        Assert.Equal("Jefe de Cátedra", creada.Responsable.Rol);
        Assert.Single(creada.Historial);
        var ruta = $"/api/tareas/{creada.Id}";

        await Esperar(ajeno.PostAsJsonAsync($"{ruta}/estado", new { estado = "en_curso" }, ct), HttpStatusCode.Forbidden);
        await Esperar(ajeno.PatchAsJsonAsync($"{ruta}/avance", new { porcentajeAvance = 10 }, ct), HttpStatusCode.Forbidden);

        Assert.Equal("en_curso", (await Cambiar(responsable, ruta, new { estado = "en_curso" }, ct)).Estado);

        await Esperar(responsable.PostAsJsonAsync($"{ruta}/estado", new { estado = "pausa" }, ct), HttpStatusCode.UnprocessableEntity);
        var enPausa = await Cambiar(responsable, ruta, new { estado = "pausa", comentario = "Necesito una consulta" }, ct);
        Assert.Equal("pausa", enPausa.Estado);
        Assert.Contains(enPausa.Comentarios, c => c.Texto == "Necesito una consulta" && c.RolAutor == "Jefe de Cátedra");

        await Esperar(responsable.PostAsJsonAsync($"{ruta}/estado", new { estado = "resuelta" }, ct), HttpStatusCode.UnprocessableEntity);
        await Esperar(responsable.PostAsJsonAsync($"{ruta}/estado", new { estado = "cancelada" }, ct), HttpStatusCode.Forbidden);
        await Esperar(responsable.PutAsJsonAsync(ruta, EdicionDe(JefeCatedra), ct), HttpStatusCode.Forbidden);

        await Esperar(responsable.PatchAsJsonAsync($"{ruta}/avance", new { porcentajeAvance = 120 }, ct), HttpStatusCode.BadRequest);
        var conAvance = await ReadAsync<TareaDto>(await responsable.PatchAsJsonAsync($"{ruta}/avance", new { porcentajeAvance = 60 }, ct), ct);
        Assert.Equal(60, conAvance.PorcentajeAvance);

        var resuelta = await Cambiar(responsable, ruta, new { estado = "resuelta", solucion = "Se resolvió" }, ct);
        Assert.Equal("Se resolvió", resuelta.Solucion);
        Assert.Equal(60, resuelta.PorcentajeAvance);

        await Esperar(responsable.PostAsJsonAsync($"{ruta}/estado", new { estado = "en_curso" }, ct), HttpStatusCode.Forbidden);
        await Esperar(secretaria.PostAsJsonAsync($"{ruta}/estado", new { estado = "cancelada" }, ct), HttpStatusCode.Forbidden);
        Assert.Equal("en_curso", (await Cambiar(secretaria, ruta, new { estado = "en_curso" }, ct)).Estado);
        Assert.Equal("cancelada", (await Cambiar(secretaria, ruta, new { estado = "cancelada" }, ct)).Estado);

        var editada = await ReadAsync<TareaDto>(await secretaria.PutAsJsonAsync(ruta, EdicionDe(JefeCatedra, "Título nuevo"), ct), ct);
        Assert.Equal("Título nuevo", editada.Titulo);

        var comentada = await ReadAsync<TareaDto>(await ajeno.PostAsJsonAsync($"{ruta}/comentarios", new { texto = "Hola" }, ct), ct);
        Assert.Contains(comentada.Comentarios, c => c.Texto == "Hola");
        await Esperar(ajeno.PostAsJsonAsync($"{ruta}/comentarios", new { texto = "  " }, ct), HttpStatusCode.BadRequest);

        var detalle = await secretaria.GetFromJsonAsync<TareaDto>(ruta, ct);
        Assert.Contains(detalle!.Historial, e => e.Accion == "editar_avance" && e.Detalle == "60%");
        Assert.Contains(detalle.Historial, e => e.Accion == "cambiar_estado" && e.Estado == "cancelada");
        await Esperar(secretaria.GetAsync($"/api/tareas/{Guid.NewGuid()}", ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Hijas_relaciones_y_proyectos()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var decanato = Cliente(host, Decanato, "decanato");
        using var secretaria = Cliente(host, Secretaria, "secretaria");
        using var administrativo = Cliente(host, Administrativo, "administrativo");

        // Proyectos: solo Decanato y Secretaría los crean; el Responsable respeta la jerarquía.
        var proyectos = await decanato.GetFromJsonAsync<ProyectoTareasDto[]>("/api/tareas/proyectos", ct);
        Assert.Equal(4, proyectos!.Length);
        Func<Guid, object> datosProyecto = responsable => new
        {
            nombre = "Proyecto nuevo", descripcion = "d",
            fechaInicio = "2026-01-01", fechaFin = "2026-12-31", responsableId = responsable,
        };
        await Esperar(administrativo.PostAsJsonAsync("/api/tareas/proyectos", datosProyecto(Secretaria), ct), HttpStatusCode.Forbidden);
        await Esperar(secretaria.PostAsJsonAsync("/api/tareas/proyectos", datosProyecto(Decanato), ct), HttpStatusCode.UnprocessableEntity);
        await Esperar(decanato.PostAsJsonAsync("/api/tareas/proyectos", datosProyecto(Administrativo), ct), HttpStatusCode.UnprocessableEntity);
        var proyecto = await ReadAsync<ProyectoTareasDto>(
            await decanato.PostAsJsonAsync("/api/tareas/proyectos", datosProyecto(Secretaria), ct), ct);
        Assert.Equal("abierto", proyecto.Estado);
        Assert.Equal("Secretaría Académica", proyecto.Responsable.Rol);
        Assert.True(proyecto.Numero > proyectos.Max(p => p.Numero));

        await Esperar(administrativo.PostAsJsonAsync($"/api/tareas/proyectos/{proyecto.Id}/estado", new { estado = "finalizado" }, ct), HttpStatusCode.Forbidden);
        var finalizado = await ReadAsync<ProyectoTareasDto>(
            await secretaria.PostAsJsonAsync($"/api/tareas/proyectos/{proyecto.Id}/estado", new { estado = "finalizado" }, ct), ct);
        Assert.Equal("finalizado", finalizado.Estado);

        // Hijas: heredan el Proyecto del padre, sin importar lo que envíe el cliente.
        var padre = await Crear(decanato, NuevaTarea(Secretaria) with { ProyectoId = ProyectoTesting }, ct);
        var hija = await Crear(decanato, NuevaTarea(Secretaria) with { ProyectoId = proyecto.Id, TareaPadreId = padre.Id }, ct);
        Assert.Equal(ProyectoTesting, hija.ProyectoId);
        Assert.Equal(padre.Id, hija.TareaPadreId);
        var nieta = await Crear(decanato, NuevaTarea(Secretaria) with { TareaPadreId = hija.Id }, ct);
        Assert.Equal(ProyectoTesting, nieta.ProyectoId);

        // Editar una hija no la desasocia del Proyecto del padre.
        var editada = await ReadAsync<TareaDto>(
            await decanato.PutAsJsonAsync($"/api/tareas/{hija.Id}", EdicionDe(Secretaria) with { ProyectoId = null }, ct), ct);
        Assert.Equal(ProyectoTesting, editada.ProyectoId);
        await Esperar(decanato.PostAsJsonAsync("/api/tareas",
            NuevaTarea(Secretaria) with { TareaPadreId = Guid.NewGuid() }, ct), HttpStatusCode.NotFound);

        // La numeración es correlativa.
        Assert.True(nieta.Numero > hija.Numero && hija.Numero > padre.Numero);

        // Relaciones: idempotentes y visibles desde ambos lados; sin efecto en estado ni avance.
        var rutaRelaciones = $"/api/tareas/{padre.Id}/relaciones";
        await Esperar(decanato.PostAsJsonAsync(rutaRelaciones, new { otraTareaId = padre.Id }, ct), HttpStatusCode.UnprocessableEntity);
        await Esperar(decanato.PostAsJsonAsync(rutaRelaciones, new { otraTareaId = Guid.NewGuid() }, ct), HttpStatusCode.NotFound);
        await Esperar(decanato.PostAsJsonAsync(rutaRelaciones, new { otraTareaId = TareaSemilla }, ct), HttpStatusCode.NoContent);
        await Esperar(decanato.PostAsJsonAsync(rutaRelaciones, new { otraTareaId = TareaSemilla }, ct), HttpStatusCode.NoContent);
        var desdePadre = await decanato.GetFromJsonAsync<TareaDto>($"/api/tareas/{padre.Id}", ct);
        var desdeSemilla = await decanato.GetFromJsonAsync<TareaDto>($"/api/tareas/{TareaSemilla}", ct);
        Assert.Equal([TareaSemilla], desdePadre!.TareasRelacionadasIds);
        Assert.Contains(padre.Id, desdeSemilla!.TareasRelacionadasIds);
        Assert.Equal("pendiente", desdePadre.Estado);

        await Esperar(decanato.DeleteAsync($"{rutaRelaciones}/{TareaSemilla}", ct), HttpStatusCode.NoContent);
        Assert.Empty((await decanato.GetFromJsonAsync<TareaDto>($"/api/tareas/{padre.Id}", ct))!.TareasRelacionadasIds);
    }

    [Fact]
    public async Task El_administrador_de_sistemas_puede_todo_y_es_la_maxima_jerarquia()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        using var host = CrearHost();
        using var administrador = Cliente(host, Administrador, "sys_admin");
        using var decanato = Cliente(host, Decanato, "decanato");

        // Nadie puede asignarle tareas: es la máxima jerarquía.
        var candidatosDecanato = await decanato.GetFromJsonAsync<PersonaTareaDto[]>("/api/tareas/candidatos", ct);
        Assert.DoesNotContain(candidatosDecanato!, c => c.Id == Administrador);
        await Esperar(decanato.PostAsJsonAsync("/api/tareas", NuevaTarea(Administrador), ct), HttpStatusCode.UnprocessableEntity);

        // Él puede asignar a cualquiera, incluido Decanato.
        var candidatosAdministrador = await administrador.GetFromJsonAsync<PersonaTareaDto[]>("/api/tareas/candidatos", ct);
        Assert.Contains(candidatosAdministrador!, c => c.Id == Decanato);
        await Crear(administrador, NuevaTarea(Decanato), ct);

        // Tarea ajena (creada por Secretaría, Responsable Jefe de Cátedra): edita, avanza, cancela y reabre.
        var ruta = $"/api/tareas/{TareaSemilla}";
        await Esperar(administrador.PutAsJsonAsync(ruta, EdicionDe(JefeCatedra, "Editada por el administrador"), ct), HttpStatusCode.OK);
        await Esperar(administrador.PatchAsJsonAsync($"{ruta}/avance", new { porcentajeAvance = 30 }, ct), HttpStatusCode.OK);
        Assert.Equal("cancelada", (await Cambiar(administrador, ruta, new { estado = "cancelada" }, ct)).Estado);
        Assert.Equal("en_curso", (await Cambiar(administrador, ruta, new { estado = "en_curso" }, ct)).Estado);

        // Y puede crear proyectos.
        await Esperar(administrador.PostAsJsonAsync("/api/tareas/proyectos", new
        {
            nombre = "Proyecto del administrador", descripcion = "d",
            fechaInicio = "2026-01-01", fechaFin = "2026-12-31", responsableId = Decanato,
        }, ct), HttpStatusCode.Created);
    }

    // Helpers -----------------------------------------------------------

    private sealed record NuevaTareaJson(
        string Titulo, string Descripcion, string FechaInicio, string FechaFin, string Prioridad,
        string Tipo, Guid ResponsableId, Guid? ProyectoId = null, Guid? TareaPadreId = null);

    private static NuevaTareaJson NuevaTarea(Guid responsable) =>
        new("Tarea de prueba", "Descripción", "2026-10-01", "2026-10-31", "media", "administrativa", responsable);

    private static NuevaTareaJson EdicionDe(Guid responsable, string titulo = "Editada") =>
        new(titulo, "Descripción", "2026-10-01", "2026-10-31", "alta", "posgrado", responsable);

    private WebApplicationFactory<Program> CrearHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", bool.TrueString);
        });

    private static HttpClient Cliente(WebApplicationFactory<Program> host, Guid usuario, string rol)
    {
        var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
        return cliente;
    }

    private static async Task<TareaDto> Crear(HttpClient cliente, NuevaTareaJson datos, CancellationToken ct)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/tareas", datos, ct);
        Assert.True(respuesta.StatusCode == HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync(ct));
        return (await respuesta.Content.ReadFromJsonAsync<TareaDto>(ct))!;
    }

    private static async Task<TareaDto> Cambiar(HttpClient cliente, string ruta, object cuerpo, CancellationToken ct) =>
        await ReadAsync<TareaDto>(await cliente.PostAsJsonAsync($"{ruta}/estado", cuerpo, ct), ct);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage respuesta, CancellationToken ct)
    {
        using (respuesta)
        {
            Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync(ct));
            return (await respuesta.Content.ReadFromJsonAsync<T>(ct))!;
        }
    }

    private static async Task Esperar(Task<HttpResponseMessage> solicitud, HttpStatusCode esperado)
    {
        using var respuesta = await solicitud;
        Assert.True(respuesta.StatusCode == esperado,
            $"Se esperaba {esperado} y fue {respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
    }

    private async Task SembrarAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "AGENTS.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
