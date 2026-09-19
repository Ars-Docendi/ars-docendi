using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Proveedor simulado y determinista. No hace ninguna llamada de red.
/// </summary>
/// <remarks>
/// Es el default de todos los ambientes: usar un proveedor real exige
/// configuración explícita. Los ambientes efímeros de PR corren siempre con este,
/// porque su workflow ejecuta un script que viene del propio pull request en un
/// job que tiene los secrets del environment.
///
/// La respuesta dice que es simulada, en el texto y en la bandera. Un proveedor de
/// mentira que devolviera algo verosímil sería peor que uno que falla: la métrica
/// del asistente es corrección con abstención, y un texto inventado presentado
/// como respuesta real es justo lo que esa métrica prohíbe.
/// </remarks>
internal sealed class ProveedorSimulado : IProveedorDeModelo
{
    /// <summary>Nombre de configuración de este proveedor.</summary>
    public const string Clave = "simulado";

    /// <summary>Separador de los campos al armar la huella.</summary>
    private const char Separador = '|';

    public string Nombre => Clave;

    public bool EsSimulado => true;

    public Task<RespuestaDelModelo> CompletarAsync(
        SolicitudAlModelo solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ct.ThrowIfCancellationRequested();

        var huella = Huella(solicitud);
        var texto =
            $"[respuesta simulada {huella}] El asistente está corriendo con el proveedor "
            + "simulado: este texto no proviene de ningún modelo y no debe presentarse como "
            + "una respuesta del sistema.";

        return Task.FromResult(new RespuestaDelModelo(
            texto,
            TokensAproximados(solicitud.PrefijoEstable.Length + solicitud.Mensaje.Length),
            TokensAproximados(texto.Length),
            EsSimulada: true));
    }

    /// <summary>
    /// Huella estable de la solicitud: la misma entrada devuelve siempre el mismo
    /// texto, en cualquier proceso y en cualquier máquina.
    /// </summary>
    /// <remarks>
    /// SHA-256 y no <c>string.GetHashCode()</c>: en .NET el hash de string está
    /// aleatorizado por proceso, así que usarlo daría un resultado distinto en cada
    /// arranque y el cliente dejaría de ser determinista sin que nada fallara.
    /// </remarks>
    private static string Huella(SolicitudAlModelo solicitud)
    {
        var material = string.Join(
            Separador,
            solicitud.PrefijoEstable,
            solicitud.Mensaje,
            solicitud.Temperatura.ToString(CultureInfo.InvariantCulture),
            solicitud.MaximoDeTokens.ToString(CultureInfo.InvariantCulture));

        var resumen = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexStringLower(resumen)[..8];
    }

    /// <summary>
    /// Estimación grosera para que el registro operativo tenga un número estable.
    /// No pretende coincidir con el tokenizador de ningún proveedor.
    /// </summary>
    private static int TokensAproximados(int caracteres) => Math.Max(1, caracteres / 4);
}
