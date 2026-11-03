using System.Net;
using System.Net.Http.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Modules.Aulas.Api;
using Modules.Notificaciones.Contracts;

namespace ArsDocendi.IntegrationTests.Notificaciones;

public sealed class EmisionAulasTests(PostgresFixture postgres)
    : BaseNotificaciones(postgres, "emision_aulas")
{
    private const string AdministrativoId = "a1000000-0000-4000-8000-000000000006";
    private static readonly Guid PersonaSinUsuario = Guid.Parse("d0000000-0000-4000-8000-000000000010");

    private static async Task<SolicitudReservaAulaDto> CrearAsync(HttpClient docente)
    {
        var respuesta = await docente.PostAsJsonAsync("/api/aulas/solicitudes", new
        {
            dia = "2026-10-01",
            horarioDesde = "08:00:00",
            horarioHasta = "10:00:00",
            cantidadAlumnosAprox = 30,
            materiaId = MateriaDelDocente,
            comision = "K3001",
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<SolicitudReservaAulaDto>(Ct))!;
    }

    private static Task<HttpResponseMessage> AsignarAsync(HttpClient administrativo, Guid id, string aula) =>
        administrativo.PostAsJsonAsync($"/api/aulas/solicitudes/{id}/asignar", new { aulaAsignada = aula }, Ct);

    [Fact]
    public async Task Asignar_un_aula_avisa_al_docente_dueno_con_el_aula_y_bajo_su_perfil()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var docente = ClienteDe(host, Docente, "docente");
        using var admin = ClienteDe(host, Administrativo, "administrativo");
        var solicitud = await CrearAsync(docente);
        Assert.Empty(await NotificacionesAsync());

        Assert.Equal(HttpStatusCode.OK, (await AsignarAsync(admin, solicitud.Id, "Aula 204")).StatusCode);

        var aviso = Assert.Single(await NotificacionesAsync());
        Assert.Equal(Docente, aviso.Destinatario);
        Assert.Equal("docente", aviso.RolDestino);
        Assert.Equal("AULA_ASIGNADA", aviso.TipoEvento);
        Assert.Equal("SOLICITUD_AULA", aviso.TipoEntidad);
        Assert.Equal(solicitud.Id, aviso.EntidadId);
        Assert.Equal("Aula asignada", aviso.Titulo);
        Assert.StartsWith("Se te asignó Aula 204 para tu solicitud del 01/10/2026 (", aviso.Mensaje);
        Assert.EndsWith(", K3001).", aviso.Mensaje);
        Assert.Equal("Aula 204", ContextoDe(aviso).GetProperty("aula").GetString());
    }

    [Fact]
    public async Task Cambiar_el_aula_de_una_aprobada_avisa_pero_repetir_la_misma_no()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var docente = ClienteDe(host, Docente, "docente");
        using var admin = ClienteDe(host, Administrativo, "administrativo");
        var solicitud = await CrearAsync(docente);
        await AsignarAsync(admin, solicitud.Id, "Aula 204");

        await AsignarAsync(admin, solicitud.Id, "Aula 204");
        Assert.Single(await NotificacionesAsync()); // still only "Aula asignada"

        await AsignarAsync(admin, solicitud.Id, "Aula 305");
        var avisos = await NotificacionesAsync();
        Assert.Equal(2, avisos.Count);
        var actualizada = avisos.Single(a => a.TipoEvento == "AULA_ACTUALIZADA");
        Assert.Equal("Aula actualizada", actualizada.Titulo);
        Assert.Contains("cambió de Aula 204 a Aula 305.", actualizada.Mensaje);
    }

    [Fact]
    public async Task Rechazar_avisa_con_el_motivo()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var docente = ClienteDe(host, Docente, "docente");
        using var admin = ClienteDe(host, Administrativo, "administrativo");
        var solicitud = await CrearAsync(docente);

        await admin.PostAsJsonAsync($"/api/aulas/solicitudes/{solicitud.Id}/rechazar", new { motivo = "No hay cupo" }, Ct);

        var aviso = Assert.Single(await NotificacionesAsync());
        Assert.Equal("Solicitud rechazada", aviso.Titulo);
        Assert.Equal(Docente, aviso.Destinatario);
        Assert.EndsWith("K3001). Motivo: No hay cupo", aviso.Mensaje);
    }

    [Fact]
    public async Task Cancelar_una_reserva_aprobada_avisa_con_el_motivo_y_el_aula_liberada_leida_antes_de_borrarla()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var docente = ClienteDe(host, Docente, "docente");
        using var admin = ClienteDe(host, Administrativo, "administrativo");
        var solicitud = await CrearAsync(docente);
        await AsignarAsync(admin, solicitud.Id, "Aula 204");

        await admin.PostAsJsonAsync($"/api/aulas/solicitudes/{solicitud.Id}/cancelar-reserva", new { motivo = "Mantenimiento" }, Ct);

        var aviso = (await NotificacionesAsync()).Single(a => a.TipoEvento == "RESERVA_CANCELADA");
        Assert.Equal("Reserva cancelada", aviso.Titulo);
        Assert.Contains("; Aula 204 quedó liberada. Motivo: Mantenimiento", aviso.Mensaje);
        Assert.Equal("Aula 204", ContextoDe(aviso).GetProperty("aulaLiberada").GetString());
    }

    [Fact]
    public async Task Si_el_docente_cancela_avisan_administrativo_y_secretaria_con_su_propio_perfil_y_nunca_al_actor()
    {
        await SembrarAsync();
        // The teacher also holds the administrativo profile: as the actor she must not be notified.
        await EjecutarAsync($"INSERT INTO identity.user_roles(user_id, role_id) VALUES ('{Docente}', '{AdministrativoId}')");
        using var host = CrearHost();
        using var docente = ClienteDe(host, Docente, "docente");
        var solicitud = await CrearAsync(docente);

        Assert.Equal(HttpStatusCode.NoContent,
            (await docente.PostAsync($"/api/aulas/solicitudes/{solicitud.Id}/cancelar", null, Ct)).StatusCode);

        var avisos = await NotificacionesAsync();
        Assert.Equal(2, avisos.Count);
        Assert.All(avisos, a => Assert.Equal("SOLICITUD_CANCELADA_DOCENTE", a.TipoEvento));
        var aAdministrativo = avisos.Single(a => a.Destinatario == Administrativo);
        var aSecretaria = avisos.Single(a => a.Destinatario == Secretaria);
        Assert.Equal("administrativo", aAdministrativo.RolDestino);
        Assert.Equal("secretaria", aSecretaria.RolDestino);
        Assert.Equal("Solicitud cancelada por el docente", aSecretaria.Titulo);
        Assert.StartsWith("López, Carla canceló su solicitud del 01/10/2026 (", aSecretaria.Mensaje);
        Assert.EndsWith("Si ya pediste un aula a Secretaría Académica, hay que liberarla.", aSecretaria.Mensaje);
        Assert.DoesNotContain(avisos, a => a.Destinatario == Docente);
    }

    [Fact]
    public async Task Una_solicitud_anterior_sin_rol_solicitante_avisa_bajo_cualquier_perfil()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var docente = ClienteDe(host, Docente, "docente");
        using var admin = ClienteDe(host, Administrativo, "administrativo");
        var solicitud = await CrearAsync(docente);
        await EjecutarAsync($"UPDATE aulas.solicitudes_reserva SET rol_solicitante = NULL WHERE id = '{solicitud.Id}'");

        await AsignarAsync(admin, solicitud.Id, "Aula 1");

        Assert.Null(Assert.Single(await NotificacionesAsync()).RolDestino);
    }

    [Fact]
    public async Task Si_la_persona_no_tiene_usuario_no_se_avisa_y_la_accion_se_confirma()
    {
        await SembrarAsync();
        var id = Guid.NewGuid();
        await EjecutarAsync($"""
            INSERT INTO aulas.solicitudes_reserva(id, docente_id, dia, horario_desde, horario_hasta, cantidad_alumnos_aprox, materia_id, comision)
            VALUES ('{id}', '{PersonaSinUsuario}', DATE '2026-10-01', '08:00', '10:00', 10, '{MateriaDelDocente}', 'K1')
            """);
        using var host = CrearHost();
        using var admin = ClienteDe(host, Administrativo, "administrativo");

        Assert.Equal(HttpStatusCode.OK, (await AsignarAsync(admin, id, "Aula 9")).StatusCode);

        Assert.Empty(await NotificacionesAsync());
        Assert.Equal(1, await EscalarAsync("SELECT count(*) FROM aulas.solicitudes_reserva WHERE id = @id AND estado = 'aprobada'", ("id", id)));
    }

    [Fact]
    public async Task Si_la_emision_falla_la_accion_de_negocio_queda_confirmada()
    {
        await SembrarAsync();
        using var host = CrearHost();
        using var docente = ClienteDe(host, Docente, "docente");
        using var admin = ClienteDe(host, Administrativo, "administrativo");
        var solicitud = await CrearAsync(docente);
        await EjecutarAsync("DROP TABLE notificaciones.notificaciones");

        var respuesta = await AsignarAsync(admin, solicitud.Id, "Aula 204");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("aprobada", (await respuesta.Content.ReadFromJsonAsync<SolicitudReservaAulaDto>(Ct))!.Estado);
        Assert.Equal(1, await EscalarAsync("SELECT count(*) FROM aulas.solicitudes_reserva WHERE id = @id AND estado = 'aprobada' AND aula_asignada = 'Aula 204'", ("id", solicitud.Id)));
    }

    [Fact]
    public async Task Un_notificador_que_lanza_tampoco_deshace_la_accion()
    {
        await SembrarAsync();
        using var host = CrearHost(servicios =>
        {
            servicios.AddScoped<INotificador, NotificadorQueLanza>();
        });
        using var docente = ClienteDe(host, Docente, "docente");
        using var admin = ClienteDe(host, Administrativo, "administrativo");
        var solicitud = await CrearAsync(docente);

        var rechazo = await admin.PostAsJsonAsync($"/api/aulas/solicitudes/{solicitud.Id}/rechazar", new { motivo = "Sin cupo" }, Ct);

        Assert.Equal(HttpStatusCode.OK, rechazo.StatusCode);
        Assert.Equal("rechazada", (await rechazo.Content.ReadFromJsonAsync<SolicitudReservaAulaDto>(Ct))!.Estado);
        Assert.Equal(1, await EscalarAsync("SELECT count(*) FROM aulas.solicitudes_reserva WHERE id = @id AND estado = 'rechazada'", ("id", solicitud.Id)));
        Assert.Empty(await NotificacionesAsync()); // the throwing notifier really was the one in use
    }

    private sealed class NotificadorQueLanza : INotificador
    {
        public Task NotificarAsync(AvisoNuevo aviso, IReadOnlyCollection<DestinoNotificacion> destinos, CancellationToken ct) =>
            throw new InvalidOperationException("boom with data: lopez@example.test");
    }
}
