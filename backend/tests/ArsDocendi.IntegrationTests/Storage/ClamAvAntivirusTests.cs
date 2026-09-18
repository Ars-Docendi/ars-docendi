using System.Net;
using System.Net.Sockets;
using System.Text;
using ArsDocendi.Storage;
using ArsDocendi.Storage.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed class ClamAvAntivirusTests
{
    [Fact]
    public async Task Respuesta_ok_con_terminador_nul_se_interpreta_como_archivo_limpio()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        var ct = TestContext.Current.CancellationToken;
        var servidor = Task.Run(async () =>
        {
            using var cliente = await listener.AcceptTcpClientAsync(ct);
            await using var red = cliente.GetStream();
            var comando = new byte[10];
            await red.ReadExactlyAsync(comando, ct);

            while (true)
            {
                var longitudBytes = new byte[4];
                await red.ReadExactlyAsync(longitudBytes, ct);
                var longitud = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(longitudBytes);
                if (longitud == 0) break;
                var contenido = new byte[longitud];
                await red.ReadExactlyAsync(contenido, ct);
            }

            await red.WriteAsync("stream: OK\0"u8.ToArray(), ct);
        }, ct);

        var antivirus = new ClamAvAntivirus(
            Options.Create(new AlmacenamientoOptions
            {
                ClamAvHost = "127.0.0.1",
                ClamAvPort = puerto,
                RechazarSiAntivirusNoDisponible = true,
            }),
            NullLogger<ClamAvAntivirus>.Instance);

        var resultado = await antivirus.AnalizarAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("contenido de prueba")),
            ct);
        await servidor;

        Assert.True(resultado.Limpio);
        Assert.True(resultado.Disponible);
        Assert.Null(resultado.Motivo);
    }
}
