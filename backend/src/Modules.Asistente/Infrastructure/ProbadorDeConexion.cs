using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>Stable codes of the administrator's connection test (asistente-modelo-en-caliente, task 3.2).</summary>
internal static class CodigosDePrueba
{
    public const string ClaveInvalida = "clave-invalida";
    public const string Inalcanzable = "inalcanzable";
    public const string SinCredito = "sin-credito";
    public const string ModeloInexistente = "modelo-inexistente";
    public const string Saturado = "saturado";
}

/// <summary>Outcome of one connection test: a latency, or exactly one failure code.</summary>
internal sealed record ResultadoDePrueba(bool Exito, int LatenciaMs, string? Codigo)
{
    public static ResultadoDePrueba Fallo(string codigo) => new(false, 0, codigo);
}

/// <summary>The local servers the deployment configures: the only ones that may be selected.</summary>
internal static class ServidoresLocales
{
    /// <summary>Identifiers of the configured servers, in display order. Never a URL.</summary>
    public static IReadOnlyList<string> Configurados(OpcionesAsistente asistente, OpcionesInferencia inferencia)
    {
        var configurados = new List<string>(2);
        if (inferencia.Habilitada)
        {
            configurados.Add(ProveedorOllama.Clave);
        }

        if (!string.IsNullOrWhiteSpace(asistente.UrlDelProveedorLocal))
        {
            configurados.Add(ProveedorLocal.Clave);
        }

        return configurados;
    }

    public static bool EsLocal(string empresa) =>
        string.Equals(empresa, ProveedorOllama.Clave, StringComparison.Ordinal)
        || string.Equals(empresa, ProveedorLocal.Clave, StringComparison.Ordinal);

    public static bool EstaConfigurado(string empresa, OpcionesAsistente asistente, OpcionesInferencia inferencia) =>
        Configurados(asistente, inferencia).Contains(empresa);
}

/// <summary>
/// One minimal real call to a company and model, through the same adapter path a turn would use,
/// that persists nothing (asistente-modelo-en-caliente, task 3.2).
/// </summary>
internal sealed class ProbadorDeConexion(
    IServiceProvider servicios,
    RepositorioDeCredenciales credenciales,
    CompuertaDelModelo compuerta,
    IOptions<OpcionesAsistente> asistente,
    IOptions<OpcionesInferencia> inferencia,
    TimeProvider reloj)
{
    private static readonly SolicitudAlModelo Solicitud = new()
    {
        PrefijoEstable = "Answer with the single word: ok.",
        Mensaje = "ping",
        Temperatura = 0m,
        Esfuerzo = EsfuerzoDelModelo.Minimo,
        MaximoDeTokens = 64,
    };

    /// <param name="claveNueva">A key not saved yet, for a cloud company. Null: the stored key.</param>
    /// <exception cref="ClaveMaestraNoDisponibleException">The stored key is needed and no master key is usable.</exception>
    /// <exception cref="InvalidOperationException">A local server that is not configured (the caller rejects it first).</exception>
    public async Task<ResultadoDePrueba> ProbarAsync(
        string empresa, string modelo, string? claveNueva, CancellationToken ct)
    {
        var valores = asistente.Value;
        var local = ServidoresLocales.EsLocal(empresa);

        if (local && !ServidoresLocales.EstaConfigurado(empresa, valores, inferencia.Value))
        {
            // Defensive: the service rejects this before asking. No adapter is built.
            throw new InvalidOperationException($"El servidor local '{empresa}' no está configurado.");
        }

        IProveedorDeModelo proveedor;
        IDisposable? dueno = null;

        if (local)
        {
            IProveedorDeModelo crudo = empresa == ProveedorOllama.Clave
                ? ModuleExtensions.ArmarOllama(servicios, valores, modelo)
                : ModuleExtensions.ArmarLocal(servicios, valores, modelo);
            dueno = crudo as IDisposable;
            proveedor = valores.MaximoDeLlamadasConcurrentes > 0
                ? new ProveedorConCompuerta(
                    crudo, compuerta, TimeSpan.Zero, new ContadorDeLlamadasDelTurno(valores.MaximoDeLlamadasPorTurno))
                : crudo;
        }
        else
        {
            var clave = claveNueva;
            if (clave is null)
            {
                try
                {
                    clave = (await credenciales.AbrirAsync(empresa, ct))?.Clave;
                }
                catch (CredencialIlegibleException)
                {
                    return ResultadoDePrueba.Fallo(CodigosDePrueba.ClaveInvalida);
                }
            }

            if (clave is null)
            {
                return ResultadoDePrueba.Fallo(CodigosDePrueba.ClaveInvalida);
            }

            var cliente = ClientesDeChat.Crear(
                empresa,
                modelo,
                clave,
                servicios.GetRequiredService<IHttpClientFactory>().CreateClient(ModuleExtensions.ClienteDelProveedor));
            var chat = new ProveedorChat(
                empresa, modelo, cliente.Cliente, cliente.Constructor,
                servicios.GetRequiredService<ILogger<ProveedorChat>>(), cliente.Dueno);
            dueno = chat;
            proveedor = chat;
        }

        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, valores.TimeoutDeLlamadaSegundos)));

            var inicio = reloj.GetTimestamp();
            try
            {
                await proveedor.CompletarAsync(Solicitud, limite.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception excepcion)
            {
                return ResultadoDePrueba.Fallo(Clasificar(excepcion));
            }

            return new ResultadoDePrueba(
                true, (int)Math.Min(int.MaxValue, reloj.GetElapsedTime(inicio).TotalMilliseconds), null);
        }
        finally
        {
            dueno?.Dispose();
        }
    }

    /// <summary>
    /// The one code a provider failure becomes. Only the type and the status are read:
    /// no message of a provider is ever copied out.
    /// </summary>
    internal static string Clasificar(Exception excepcion)
    {
        if (excepcion is ProveedorSaturado)
        {
            return CodigosDePrueba.Saturado;
        }

        if (excepcion is not HttpRequestException { StatusCode: { } estado } http)
        {
            // Transport failure, timeout, or anything unexpected: the provider did not answer.
            return CodigosDePrueba.Inalcanzable;
        }

        var sinCredito = http.Data.Contains(ProveedorChat.IndicioDeSinCredito);

        return estado switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => CodigosDePrueba.ClaveInvalida,
            HttpStatusCode.PaymentRequired => CodigosDePrueba.SinCredito,
            _ when sinCredito => CodigosDePrueba.SinCredito,
            HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
                => CodigosDePrueba.ModeloInexistente,
            HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or (HttpStatusCode)529
                => CodigosDePrueba.Saturado,
            _ => CodigosDePrueba.Inalcanzable,
        };
    }
}
