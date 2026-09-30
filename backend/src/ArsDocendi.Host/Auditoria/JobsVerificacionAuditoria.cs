using ArsDocendi.Shared.Auditing;
using Npgsql;

namespace ArsDocendi.Host.Auditoria;

/// <summary>Jobs de sólo lectura. Salida 2 significa no verificado; nunca abre listener.</summary>
public static class JobsVerificacionAuditoria
{
    public static async Task<int?> EjecutarAsync(string[] args, IConfiguration config, ILogger logger, CancellationToken ct)
    {
        var comandos = args.Where(a => a is "--verificar-auditoria" or "--verificar-restauracion"
            or "--verificar-estado-auditoria" or "--sondear-auditoria").ToArray();
        if (comandos.Length == 0) return null;
        if (comandos.Length != 1) throw new InvalidOperationException("Elegir un solo job de verificación.");
        string Exigir(string clave) => config[clave] is { Length: > 0 } valor ? valor
            : throw new InvalidOperationException($"Falta {clave}.");
        try
        {
            var ambiente = Exigir("AuditoriaVerificacion:Ambiente");
            if (comandos[0] == "--verificar-estado-auditoria")
            {
                await using var ds = NpgsqlDataSource.Create(Exigir("ConnectionStrings:AuditVerify"));
                var resultado = await new VerificadorEstadoAuditoria(ds).VerificarAsync(ambiente,
                    Guid.NewGuid(), config.GetValue<long?>("AuditoriaVerificacion:DesdeCursor"), ct);
                logger.LogInformation("Estado observado: {@EstadoAuditoria}", resultado);
                // El inventario es parcial y el digest todavía no tiene ancla externa.
                return 2;
            }
            using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
                { Timeout = TimeSpan.FromSeconds(30) };
            var claves = config.GetSection("AuditoriaVerificacion:ClavesPublicasPem").Get<Dictionary<string, string>>()
                ?? throw new InvalidOperationException("Faltan claves públicas fijadas por el operador.");
            var remoto = new VerificadorTestigosRemotos(http, new(
                new Uri(Exigir("AuditoriaVerificacion:PrimarioEndpoint")), Exigir("AuditoriaVerificacion:PrimarioToken"),
                new Uri(Exigir("AuditoriaVerificacion:SecundarioEndpoint")), Exigir("AuditoriaVerificacion:SecundarioToken"), claves,
                config.GetSection("AuditoriaVerificacion:Transiciones").Get<List<TransicionClaveFirma>>()));
            if (comandos[0] == "--sondear-auditoria")
            {
                // La sonda externa no conoce el cursor de PostgreSQL ni certifica su contenido.
                var testigos = await remoto.VerificarAsync(ambiente, long.MaxValue, ct);
                var sonda = SondaFrescuraAuditoria.Evaluar(testigos,
                    config.GetValue<DateTimeOffset?>("AuditoriaVerificacion:ReporteLocalRecibidoEn"), DateTimeOffset.UtcNow,
                    TimeSpan.FromSeconds(config.GetValue<int>("AuditoriaVerificacion:MaximoAtrasoSegundos")),
                    config.GetValue<bool>("AuditoriaVerificacion:ReporteLocalValido"));
                logger.LogInformation("Sonda de frescura (no certifica contenido): {@SondaAuditoria}", sonda);
                return sonda.Saludable ? 0 : 2;
            }
            await using var datos = NpgsqlDataSource.Create(Exigir("ConnectionStrings:AuditVerify"));
            var anclado = await new VerificadorAuditoriaAnclada(datos, remoto).VerificarAsync(ambiente, ct);
            logger.LogInformation("Verificación de historial anclado: {@AuditoriaAnclada}", anclado);
            return anclado.Verificado ? 0 : 2;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // No registrar cuerpos HTTP, cadenas de conexión, tokens ni snapshots.
            logger.LogError("Auditoría NO VERIFICADA. TipoFallo={TipoFallo}", error.GetType().Name);
            return 2;
        }
    }
}
