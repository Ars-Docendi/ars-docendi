using System.Collections.Concurrent;
using System.Net.Http.Json;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Notificaciones.Domain;
using Modules.Notificaciones.Services;
using WebPush;

namespace ArsDocendi.IntegrationTests.Notificaciones;

/// <summary>Push service double at the real external boundary: records what would go over the wire.</summary>
public sealed class EnviadorFalso : IEnviadorPush
{
    public ConcurrentQueue<(string Endpoint, string Carga)> Enviados { get; } = new();
    public Func<string, ResultadoEnvioPush> Resultado { get; set; } = _ => ResultadoEnvioPush.Enviado;

    /// <summary>When set, every send waits for it: a push service that never answers.</summary>
    public TaskCompletionSource? Bloqueo { get; set; }

    public async Task<ResultadoEnvioPush> EnviarAsync(SuscripcionPush destino, string carga, CancellationToken ct)
    {
        Enviados.Enqueue((destino.Endpoint, carga));
        if (Bloqueo is not null) await Bloqueo.Task.WaitAsync(ct);
        return Resultado(destino.Endpoint);
    }

    public async Task EsperarAsync(int cantidad)
    {
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (Enviados.Count < cantidad && DateTime.UtcNow < limite) await Task.Delay(25);
    }
}

/// <summary>Host with Web Push configured (generated VAPID keys) and the sender replaced by a double.</summary>
public abstract class PushTestBase(PostgresFixture postgres, string prefijo) : BaseNotificaciones(postgres, prefijo)
{
    private static readonly VapidDetails Claves = VapidHelper.GenerateVapidKeys();

    protected const string EndpointFcm = "https://fcm.googleapis.com/fcm/send/dispositivo-";
    protected const string EndpointApple = "https://web.push.apple.com/dispositivo-";

    protected WebApplicationFactory<Program> CrearHostPush(EnviadorFalso enviador, bool conClaves = true) =>
        CrearHost(servicios => servicios.AddSingleton<IEnviadorPush>(enviador))
            .WithWebHostBuilder(builder =>
            {
                // Explicit values either way: a developer's user-secrets must not leak into the tests.
                builder.UseSetting("Notificaciones:WebPush:PublicKey", conClaves ? Claves.PublicKey : "");
                builder.UseSetting("Notificaciones:WebPush:PrivateKey", conClaves ? Claves.PrivateKey : "");
                builder.UseSetting("Notificaciones:WebPush:Subject", conClaves ? "mailto:soporte@unlam.edu.ar" : "");
            });

    protected static object Suscripcion(string endpoint) => new { endpoint, keys = new { p256dh = "clave-p256dh", auth = "clave-auth" } };

    protected static Task<HttpResponseMessage> SuscribirAsync(HttpClient cliente, string endpoint) =>
        cliente.PostAsJsonAsync("/api/notificaciones/push/suscripciones", Suscripcion(endpoint), Ct);

    protected static async Task<HttpResponseMessage> CancelarAsync(HttpClient cliente, string endpoint)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Delete, "/api/notificaciones/push/suscripciones")
        {
            Content = JsonContent.Create(new { endpoint }),
        };
        return await cliente.SendAsync(peticion, Ct);
    }

    /// <summary>Inserts a subscription straight into the table, as the browser's POST would.</summary>
    protected Task InsertarSuscripcionAsync(Guid usuario, string endpoint) => EjecutarAsync(
        "INSERT INTO notificaciones.suscripciones_push(usuario_id, endpoint, p256dh, auth) VALUES (@u, @e, 'p', 'a')", 30,
        ("u", usuario), ("e", endpoint));

    protected async Task<Guid?> PropietarioAsync(string endpoint)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new Npgsql.NpgsqlCommand(
            "SELECT usuario_id FROM notificaciones.suscripciones_push WHERE endpoint = @e", conexion);
        comando.Parameters.AddWithValue("e", endpoint);
        return await comando.ExecuteScalarAsync(Ct) as Guid?;
    }

    protected static async Task EmitirAsync(
        WebApplicationFactory<Program> host, string tipoEvento, string tipoEntidad, Guid entidadId, params Guid[] usuarios)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<Modules.Notificaciones.Contracts.INotificador>().NotificarAsync(
            new Modules.Notificaciones.Contracts.AvisoNuevo(
                tipoEvento, tipoEntidad, entidadId, null, "Aula asignada: Aula 204",
                "Se te asignó Aula 204 para tu solicitud del 01/10/2026.", new Dictionary<string, object?>()),
            usuarios.Select(u => (Modules.Notificaciones.Contracts.DestinoNotificacion)
                new Modules.Notificaciones.Contracts.DestinoNotificacion.Usuario(u)).ToArray(), Ct);
    }
}
