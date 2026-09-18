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
    public async Task<ResultadoAntivirus> AnalizarAsync(Stream contenido, CancellationToken ct)
    {
        if (contenido.CanSeek) contenido.Position = 0;
        try
        {
            using var cliente = new TcpClient();
            await cliente.ConnectAsync(opciones.Value.ClamAvHost, opciones.Value.ClamAvPort, ct);
            await using var red = cliente.GetStream();
            await red.WriteAsync("zINSTREAM\0"u8.ToArray(), ct);
            var buffer = new byte[64 * 1024];
            int leidos;
            while ((leidos = await contenido.ReadAsync(buffer, ct)) > 0)
            {
                var longitud = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(longitud, leidos);
                await red.WriteAsync(longitud, ct);
                await red.WriteAsync(buffer.AsMemory(0, leidos), ct);
            }
            await red.WriteAsync(new byte[4], ct);
            var respuesta = new byte[4096];
            var cantidad = await red.ReadAsync(respuesta, ct);
            var texto = Encoding.ASCII.GetString(respuesta, 0, cantidad)
                .TrimEnd('\0', ' ', '\t', '\r', '\n');
            if (texto.EndsWith("OK", StringComparison.OrdinalIgnoreCase))
                return new ResultadoAntivirus(true, true, null);

            logger.LogWarning("ClamAV rechazó el stream con respuesta {Respuesta}", texto);
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
