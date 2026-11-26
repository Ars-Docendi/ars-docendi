using System.Net;
using System.Net.Http.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.IntegrationTests.Tareas;
using Modules.Tareas.Application;

namespace ArsDocendi.IntegrationTests.Notificaciones;

public sealed class EmisionTareasTests(PostgresFixture postgres)
    : BaseNotificaciones(postgres, "emision_tareas")
{
    private static object Datos(Guid responsable, string titulo = "Tarea de prueba") => new
    {
        titulo,
        descripcion = "Descripción",
        fechaInicio = "2026-10-01",
        fechaFin = "2026-10-31",
        prioridad = "media",
        tipo = "administrativa",
        responsableId = responsable,
    };

    private static async Task<TareaDto> CrearAsync(HttpClient creador, Guid responsable, string titulo = "Tarea de prueba")
    {
        var respuesta = await creador.PostVersionadoAsync("/api/tareas", Datos(responsable, titulo), Ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<TareaDto>(Ct))!;
    }

    [Fact]
    public async Task Crear_una_tarea_avisa_al_responsable_sin_perfil_y_con_el_numero()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var secretaria = ClienteDe(host, Secretaria, "secretaria");

        var tarea = await CrearAsync(secretaria, Jefe);

        var aviso = Assert.Single(await NotificacionesAsync());
        Assert.Equal(Jefe, aviso.Destinatario);
        Assert.Null(aviso.RolDestino);
        Assert.Equal("TAREA_ASIGNADA", aviso.TipoEvento);
        Assert.Equal("TAREA", aviso.TipoEntidad);
        Assert.Equal(tarea.Id, aviso.EntidadId);
        Assert.Equal($"Tarea N° {tarea.Numero} asignada", aviso.Titulo);
        Assert.Equal($"Se te asignó la tarea N° {tarea.Numero}: Tarea de prueba.", aviso.Mensaje);
    }

    [Fact]
    public async Task Asignarse_la_tarea_a_uno_mismo_no_avisa()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var secretaria = ClienteDe(host, Secretaria, "secretaria");

        await CrearAsync(secretaria, Secretaria);

        Assert.Empty(await NotificacionesAsync());
    }

    [Fact]
    public async Task Reasignar_avisa_al_nuevo_responsable_pero_editar_sin_cambiar_de_responsable_no()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var secretaria = ClienteDe(host, Secretaria, "secretaria");
        var tarea = await CrearAsync(secretaria, Jefe);
        var ruta = $"/api/tareas/{tarea.Id}";

        Assert.Equal(HttpStatusCode.OK, (await secretaria.PutVersionadoAsync(ruta, Datos(Jefe, "Otro título"), Ct)).StatusCode);
        Assert.Single(await NotificacionesAsync()); // only the creation notice

        Assert.Equal(HttpStatusCode.OK, (await secretaria.PutVersionadoAsync(ruta, Datos(Coordinador, "Otro título"), Ct)).StatusCode);

        var avisos = await NotificacionesAsync();
        Assert.Equal(2, avisos.Count);
        Assert.Equal(Coordinador, avisos[^1].Destinatario);
        Assert.Equal($"Tarea N° {tarea.Numero} asignada", avisos[^1].Titulo);
        Assert.Contains("Otro título", avisos[^1].Mensaje);
    }

    [Fact]
    public async Task Cambiar_el_estado_avisa_al_creador_si_lo_hace_el_responsable_y_al_responsable_si_lo_hace_el_creador()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var secretaria = ClienteDe(host, Secretaria, "secretaria");
        using var jefe = ClienteDe(host, Jefe, "jefe_catedra");
        var tarea = await CrearAsync(secretaria, Jefe);
        var ruta = $"/api/tareas/{tarea.Id}";

        Assert.Equal(HttpStatusCode.OK, (await jefe.PostVersionadoAsync($"{ruta}/estado", new { estado = "en_curso" }, Ct)).StatusCode);
        var alCreador = (await NotificacionesAsync(Secretaria)).Single();
        Assert.Equal($"Tarea N° {tarea.Numero}: cambio de estado", alCreador.Titulo);
        Assert.Equal($"Gustavo Ruiz cambió la tarea N° {tarea.Numero} (Tarea de prueba) de Pendiente a En curso.", alCreador.Mensaje);
        Assert.Null(alCreador.RolDestino);

        Assert.Equal(HttpStatusCode.OK, (await secretaria.PostVersionadoAsync($"{ruta}/estado", new { estado = "cancelada" }, Ct)).StatusCode);
        var alResponsable = (await NotificacionesAsync(Jefe)).Single(a => a.TipoEvento == "TAREA_ESTADO_CAMBIADO");
        Assert.Contains("Lucía Fernández cambió la tarea", alResponsable.Mensaje);
        Assert.EndsWith("de En curso a Cancelada.", alResponsable.Mensaje);
        // The actor of each change was never notified of their own change.
        Assert.DoesNotContain(await NotificacionesAsync(Jefe), a => a.TipoEvento == "TAREA_ESTADO_CAMBIADO" && a.Mensaje.StartsWith("Gustavo"));
    }

    [Fact]
    public async Task Un_tercero_con_autoridad_que_cambia_el_estado_avisa_a_responsable_y_creador()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var secretaria = ClienteDe(host, Secretaria, "secretaria");
        using var admin = ClienteDe(host, SysAdmin, "sys_admin");
        var tarea = await CrearAsync(secretaria, Jefe);

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostVersionadoAsync($"/api/tareas/{tarea.Id}/estado", new { estado = "en_curso" }, Ct)).StatusCode);

        var porEstado = (await NotificacionesAsync()).Where(a => a.TipoEvento == "TAREA_ESTADO_CAMBIADO").ToList();
        Assert.Equal(new[] { Jefe, Secretaria }.OrderBy(x => x), porEstado.Select(a => a.Destinatario).OrderBy(x => x));
    }

    [Fact]
    public async Task Comentar_avisa_a_los_demas_con_el_texto_entre_comillas_y_recortado_a_90()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var secretaria = ClienteDe(host, Secretaria, "secretaria");
        using var jefe = ClienteDe(host, Jefe, "jefe_catedra");
        var tarea = await CrearAsync(secretaria, Jefe);

        var largo = new string('a', 120);
        Assert.Equal(HttpStatusCode.Created,
            (await jefe.PostVersionadoAsync($"/api/tareas/{tarea.Id}/comentarios", new { texto = largo }, Ct)).StatusCode);

        var aviso = (await NotificacionesAsync(Secretaria)).Single(a => a.TipoEvento == "TAREA_COMENTADA");
        Assert.Equal($"Tarea N° {tarea.Numero}: nuevo comentario", aviso.Titulo);
        Assert.StartsWith($"Gustavo Ruiz comentó en la tarea N° {tarea.Numero} (Tarea de prueba): “", aviso.Mensaje);
        Assert.EndsWith($"{new string('a', 89)}…”", aviso.Mensaje);
        Assert.DoesNotContain(await NotificacionesAsync(Jefe), a => a.TipoEvento == "TAREA_COMENTADA");
    }

    [Fact]
    public async Task Si_la_emision_falla_la_accion_de_tareas_queda_confirmada()
    {
        await SembrarAsync();
        await EjecutarAsync("DROP TABLE notificaciones.notificaciones");
        using var host = CrearHost();
        using var secretaria = ClienteDe(host, Secretaria, "secretaria");
        using var jefe = ClienteDe(host, Jefe, "jefe_catedra");

        var tarea = await CrearAsync(secretaria, Jefe);
        var respuesta = await jefe.PostVersionadoAsync($"/api/tareas/{tarea.Id}/comentarios", new { texto = "Hola" }, Ct);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal(1, await EscalarAsync("SELECT count(*) FROM tareas.tarea_comentarios WHERE tarea_id = @id", ("id", tarea.Id)));
    }
}
