using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Notificaciones.Contracts;

namespace ArsDocendi.IntegrationTests.Notificaciones;

public sealed class PushEntregaTests(PostgresFixture postgres) : PushTestBase(postgres, "push_entrega")
{
    [Fact]
    public async Task El_push_lleva_el_texto_real_y_el_enlace_y_respeta_los_modulos_que_el_usuario_desactivo()
    {
        await SembrarAsync();
        var enviador = new EnviadorFalso();
        using var host = CrearHostPush(enviador);
        var endpointDocente = EndpointFcm + "docente";
        var endpointJefe = EndpointApple + "jefe";
        await InsertarSuscripcionAsync(Docente, endpointDocente);
        await InsertarSuscripcionAsync(Jefe, endpointJefe);
        await EjecutarAsync(
            "INSERT INTO notificaciones.preferencias_push(usuario_id, modulo, habilitado) VALUES (@u, 'AULAS', false)",
            30, ("u", Jefe));
        var solicitud = Guid.NewGuid();

        await EmitirAsync(host, TiposEventoNotificacion.AulaAsignada, TiposEntidadNotificacion.SolicitudAula, solicitud, Docente, Jefe);
        // Another event type of the same module is silenced too: the switch is per module.
        await EmitirAsync(host, TiposEventoNotificacion.AulaActualizada, TiposEntidadNotificacion.SolicitudAula, solicitud, Jefe);
        // A module the Jefe did not switch off still reaches them.
        var tarea = Guid.NewGuid();
        await EmitirAsync(host, TiposEventoNotificacion.TareaAsignada, TiposEntidadNotificacion.Tarea, tarea, Jefe);
        await enviador.EsperarAsync(2);
        await Task.Delay(300); // a wrongly sent third push would arrive about now

        Assert.Equal(2, enviador.Enviados.Count);
        var aDocente = Assert.Single(enviador.Enviados, e => e.Endpoint == endpointDocente);
        var carga = JsonDocument.Parse(aDocente.Carga).RootElement;
        Assert.Equal("Aula asignada: Aula 204", carga.GetProperty("titulo").GetString());
        Assert.Equal("Se te asignó Aula 204 para tu solicitud del 01/10/2026.", carga.GetProperty("mensaje").GetString());
        Assert.Equal($"/aulas/solicitudes/{solicitud}", carga.GetProperty("url").GetString());
        var notificacion = (await NotificacionesAsync(Docente)).Single();
        Assert.Equal(notificacion.Id, carga.GetProperty("id").GetGuid());

        var aJefe = Assert.Single(enviador.Enviados, e => e.Endpoint == endpointJefe);
        Assert.Equal($"/tareas/{tarea}", JsonDocument.Parse(aJefe.Carga).RootElement.GetProperty("url").GetString());

        // Switching a module off only silences the push: the in-app notices are always created.
        Assert.Equal(3, (await NotificacionesAsync(Jefe)).Count);
    }

    [Fact]
    public async Task Notificar_no_espera_al_servicio_de_push()
    {
        await SembrarAsync();
        var enviador = new EnviadorFalso { Bloqueo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var host = CrearHostPush(enviador);
        await InsertarSuscripcionAsync(Docente, EndpointFcm + "colgado");

        await EmitirAsync(host, TiposEventoNotificacion.TareaAsignada, TiposEntidadNotificacion.Tarea, Guid.NewGuid(), Docente)
            .WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.Single(await NotificacionesAsync(Docente));
        await enviador.EsperarAsync(1); // the worker did pick it up, and is stuck there
        Assert.Single(enviador.Enviados);
        enviador.Bloqueo.SetResult();
    }

    [Fact]
    public async Task Una_suscripcion_que_el_servicio_da_por_caducada_se_elimina_y_las_demas_siguen()
    {
        await SembrarAsync();
        var vieja = EndpointFcm + "vieja";
        var vigente = EndpointApple + "vigente";
        var enviador = new EnviadorFalso
        {
            Resultado = endpoint => endpoint == vieja
                ? Modules.Notificaciones.Services.ResultadoEnvioPush.Caducada
                : Modules.Notificaciones.Services.ResultadoEnvioPush.Enviado,
        };
        using var host = CrearHostPush(enviador);
        await InsertarSuscripcionAsync(Docente, vieja);
        await InsertarSuscripcionAsync(Docente, vigente);

        await EmitirAsync(host, TiposEventoNotificacion.TareaAsignada, TiposEntidadNotificacion.Tarea, Guid.NewGuid(), Docente);
        await enviador.EsperarAsync(2);
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (await PropietarioAsync(vieja) is not null && DateTime.UtcNow < limite) await Task.Delay(50);

        Assert.Null(await PropietarioAsync(vieja));
        Assert.Equal(Docente, await PropietarioAsync(vigente));
    }
}
