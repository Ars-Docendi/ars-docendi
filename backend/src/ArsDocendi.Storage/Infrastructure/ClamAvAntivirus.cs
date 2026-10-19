using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArsDocendi.Storage.Infrastructure;

internal sealed class ClamAvAntivirus(
    IOptions<AlmacenamientoOptions> opciones,
    ILogger<ClamAvAntivirus> logger) : IAntivirusArchivos
{
    // Un clamd que acepta la conexión y no contesta dejaría colgada la confirmación.
    private static readonly TimeSpan TiempoMaximo = TimeSpan.FromSeconds(30);

    public async Task<ResultadoAntivirus> AnalizarAsync(Stream contenido, CancellationToken ct)
    {
        if (contenido.CanSeek) contenido.Position = 0;
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TiempoMaximo);
        var token = limite.Token;
        try
        {
            using var cliente = new TcpClient();
            await cliente.ConnectAsync(opciones.Value.ClamAvHost, opciones.Value.ClamAvPort, token);
            await using var red = cliente.GetStream();
            await red.WriteAsync("zINSTREAM\0"u8.ToArray(), token);
            var buffer = new byte[64 * 1024];
            int leidos;
            while ((leidos = await contenido.ReadAsync(buffer, token)) > 0)
            {
                var longitud = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(longitud, leidos);
                await red.WriteAsync(longitud, token);
                await red.WriteAsync(buffer.AsMemory(0, leidos), token);
            }
            await red.WriteAsync(new byte[4], token);
            // La respuesta puede llegar partida: se lee hasta el NUL final o el cierre.
            var respuesta = new byte[4096];
            var cantidad = 0;
            int recibidos;
            while (cantidad < respuesta.Length
                && (recibidos = await red.ReadAsync(respuesta.AsMemory(cantidad), token)) > 0)
            {
                cantidad += recibidos;
                if (Array.IndexOf(respuesta, (byte)0, 0, cantidad) >= 0) break;
            }
            var texto = Encoding.ASCII.GetString(respuesta, 0, cantidad)
                .TrimEnd('\0', ' ', '\t', '\r', '\n');
            if (texto.EndsWith("OK", StringComparison.OrdinalIgnoreCase))
                return new ResultadoAntivirus(true, true, null);

            logger.LogWarning("ClamAV rechazó el stream");
            return new ResultadoAntivirus(false, false, "El análisis antivirus rechazó el archivo.");
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo contactar el antivirus interno");
            if (opciones.Value.RechazarSiAntivirusNoDisponible)
                return new ResultadoAntivirus(false, false, "El análisis antivirus no está disponible.");
            return new ResultadoAntivirus(true, true, "Análisis antivirus omitido por configuración no productiva.");
        }
        finally
        {
            if (contenido.CanSeek) contenido.Position = 0;
        }
    }
}
