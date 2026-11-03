using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Notificaciones.Services;

namespace ArsDocendi.IntegrationTests.Notificaciones;

public sealed class PushSuscripcionesTests(PostgresFixture postgres) : PushTestBase(postgres, "push_suscripciones")
{
    [Fact]
    public async Task Un_dispositivo_pasa_al_usuario_de_la_sesion_y_solo_su_dueno_lo_da_de_baja()
    {
        await SembrarAsync();
        var enviador = new EnviadorFalso();
        using var host = CrearHostPush(enviador);
        using var docente = ClienteDe(host, Docente, "docente");
        using var jefe = ClienteDe(host, Jefe, "jefe_catedra");
        var endpoint = EndpointFcm + Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await SuscribirAsync(docente, endpoint)).StatusCode);
        Assert.Equal(Docente, await PropietarioAsync(endpoint));

        // Someone else cannot remove it.
        Assert.Equal(HttpStatusCode.NoContent, (await CancelarAsync(jefe, endpoint)).StatusCode);
        Assert.Equal(Docente, await PropietarioAsync(endpoint));

        // Logging in on the same device reassigns it: one row, new owner.
        Assert.Equal(HttpStatusCode.NoContent, (await SuscribirAsync(jefe, endpoint)).StatusCode);
        Assert.Equal(Jefe, await PropietarioAsync(endpoint));
        Assert.Equal(1, await EscalarAsync("SELECT count(*) FROM notificaciones.suscripciones_push"));

        // The previous user can no longer remove it either; the current one can.
        await CancelarAsync(docente, endpoint);
        Assert.Equal(Jefe, await PropietarioAsync(endpoint));
        await CancelarAsync(jefe, endpoint);
        Assert.Null(await PropietarioAsync(endpoint));
    }

    [Fact]
    public async Task Un_endpoint_fuera_de_la_lista_se_rechaza_con_400_y_no_se_guarda()
    {
        await SembrarAsync();
        using var host = CrearHostPush(new EnviadorFalso());
        using var docente = ClienteDe(host, Docente, "docente");

        var respuesta = await SuscribirAsync(docente, "https://intranet.local/hook");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, await EscalarAsync("SELECT count(*) FROM notificaciones.suscripciones_push"));
    }

    [Fact]
    public async Task Sin_claves_VAPID_la_clave_y_el_alta_responden_404_y_con_claves_se_publica_la_clave()
    {
        await SembrarAsync();
        using (var sinClaves = CrearHostPush(new EnviadorFalso(), conClaves: false))
        {
            using var cliente = ClienteDe(sinClaves, Docente, "docente");
            Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/api/notificaciones/push/clave-publica", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await SuscribirAsync(cliente, EndpointFcm + "x")).StatusCode);
        }

        using var conClaves = CrearHostPush(new EnviadorFalso());
        using var docente = ClienteDe(conClaves, Docente, "docente");
        var respuesta = await docente.GetAsync("/api/notificaciones/push/clave-publica", Ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(
            (await respuesta.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("clavePublica").GetString()));
    }

    [Fact]
    public async Task Las_preferencias_arrancan_todas_activas_y_un_tipo_desconocido_se_rechaza_sin_escribir()
    {
        await SembrarAsync();
        using var host = CrearHostPush(new EnviadorFalso());
        using var docente = ClienteDe(host, Docente, "docente");

        var inicial = (await docente.GetFromJsonAsync<PreferenciasPushDto>("/api/notificaciones/push/preferencias", Ct))!;
        Assert.Equal(Modules.Notificaciones.Contracts.TiposEventoNotificacion.Todos, inicial.Tipos.Select(t => t.TipoEvento));
        Assert.All(inicial.Tipos, t => Assert.True(t.Habilitado));

        var guardada = await docente.PutAsJsonAsync("/api/notificaciones/push/preferencias",
            new { tipos = new[] { new { tipoEvento = "TAREA_ASIGNADA", habilitado = false } } }, Ct);
        Assert.Equal(HttpStatusCode.OK, guardada.StatusCode);
        var luego = (await docente.GetFromJsonAsync<PreferenciasPushDto>("/api/notificaciones/push/preferencias", Ct))!;
        Assert.False(luego.Tipos.Single(t => t.TipoEvento == "TAREA_ASIGNADA").Habilitado);
        Assert.True(luego.Tipos.Single(t => t.TipoEvento == "AULA_ASIGNADA").Habilitado);

        var invalida = await docente.PutAsJsonAsync("/api/notificaciones/push/preferencias", new
        {
            tipos = new[] { new { tipoEvento = "AULA_ASIGNADA", habilitado = false }, new { tipoEvento = "INVENTADO", habilitado = false } },
        }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        var intacta = (await docente.GetFromJsonAsync<PreferenciasPushDto>("/api/notificaciones/push/preferencias", Ct))!;
        Assert.True(intacta.Tipos.Single(t => t.TipoEvento == "AULA_ASIGNADA").Habilitado);

        // Each user has their own switches.
        using var jefe = ClienteDe(host, Jefe, "jefe_catedra");
        var deJefe = (await jefe.GetFromJsonAsync<PreferenciasPushDto>("/api/notificaciones/push/preferencias", Ct))!;
        Assert.True(deJefe.Tipos.Single(t => t.TipoEvento == "TAREA_ASIGNADA").Habilitado);
    }

    [Fact]
    public void La_lista_de_tipos_conocidos_incluye_todas_las_constantes_de_eventos()
    {
        var constantes = typeof(Modules.Notificaciones.Contracts.TiposEventoNotificacion).GetFields()
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!);
        Assert.Equal(constantes.Order(), Modules.Notificaciones.Contracts.TiposEventoNotificacion.Todos.Order());
    }
}
