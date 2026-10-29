using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Modules.Designaciones.Services.Integridad;

/// <summary>
/// Claves HMAC por versión, en base64 y de al menos 32 bytes. Viven en los secretos del
/// host (<c>Integridad__ClavesSellado__1</c>), nunca en la base [BR-designaciones-019].
/// </summary>
public sealed class IntegridadOptions
{
    public const string Seccion = "Integridad";

    public Dictionary<string, string> ClavesSellado { get; set; } = [];
    public short VersionVigente { get; set; } = 1;
}

public sealed class ClavesSellado
{
    // Fuera de producción alcanza una clave conocida: no protege nada real y evita
    // exigir secretos para desarrollo y tests.
    private static readonly byte[] ClaveDesarrollo =
        SHA256.HashData(Encoding.UTF8.GetBytes("ars-docendi:clave-sellado-desarrollo-no-productiva"));

    private readonly Dictionary<short, byte[]> claves;

    public ClavesSellado(IOptions<IntegridadOptions> opciones, IHostEnvironment ambiente)
    {
        VersionVigente = opciones.Value.VersionVigente;
        claves = Parsear(opciones.Value);
        if (claves.Count == 0 && !ambiente.IsProduction())
            claves[VersionVigente] = ClaveDesarrollo;
    }

    public short VersionVigente { get; }

    public byte[] Firmar(byte[] hash) => Firmar(VersionVigente, hash)
        ?? throw new InvalidOperationException($"Falta la clave de sellado vigente en {IntegridadOptions.Seccion}.");

    /// <summary>Null si la versión no está configurada: la firma no puede verificarse.</summary>
    public byte[]? Firmar(short version, byte[] hash) =>
        claves.TryGetValue(version, out var clave) ? HMACSHA256.HashData(clave, hash) : null;

    /// <summary>Compose interpola una variable no definida como vacía: cuenta como ausente.</summary>
    internal static IEnumerable<KeyValuePair<string, string>> Definidas(IntegridadOptions opciones) =>
        opciones.ClavesSellado.Where(par => !string.IsNullOrWhiteSpace(par.Value));

    internal static IEnumerable<string> Errores(IntegridadOptions opciones)
    {
        foreach (var (version, valor) in Definidas(opciones))
        {
            if (!short.TryParse(version, out _))
                yield return $"{IntegridadOptions.Seccion}:ClavesSellado tiene una versión no numérica.";
            else if (Decodificar(valor) is null)
                yield return $"{IntegridadOptions.Seccion}:ClavesSellado:{version} debe ser base64 de al menos 32 bytes.";
        }
        if (!Definidas(opciones).Any(par => par.Key == opciones.VersionVigente.ToString()))
            yield return $"{IntegridadOptions.Seccion}:ClavesSellado no define la versión vigente.";
    }

    private static Dictionary<short, byte[]> Parsear(IntegridadOptions opciones) =>
        Definidas(opciones)
            .Select(par => (Ok: short.TryParse(par.Key, out var version), version, Clave: Decodificar(par.Value)))
            .Where(par => par.Ok && par.Clave is not null)
            .ToDictionary(par => par.version, par => par.Clave!);

    private static byte[]? Decodificar(string valor)
    {
        try
        {
            var bytes = Convert.FromBase64String(valor);
            return bytes.Length >= 32 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>En producción la aplicación no arranca sin clave vigente válida. Los mensajes nunca incluyen valores.</summary>
internal sealed class ValidacionIntegridadOptions(IHostEnvironment ambiente) : IValidateOptions<IntegridadOptions>
{
    public ValidateOptionsResult Validate(string? name, IntegridadOptions opciones)
    {
        if (!ambiente.IsProduction() && !ClavesSellado.Definidas(opciones).Any()) return ValidateOptionsResult.Success;
        var errores = ClavesSellado.Errores(opciones).ToList();
        return errores.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errores);
    }
}
