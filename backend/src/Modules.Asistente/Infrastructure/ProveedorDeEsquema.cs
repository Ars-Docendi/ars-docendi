using System.Security.Cryptography;
using System.Text;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Construye el prefijo del prompt de sistema y lo cachea por rol.
/// </summary>
/// <remarks>
/// <b>Perezoso a propósito.</b> Construirlo al arrancar exigiría una conexión a
/// la base durante el arranque del Host, y el invariante #3 pide que
/// <c>GET /api/asistente/ping</c> responda con la base detenida.
///
/// <b>No se invalida solo.</b> Una migración de esquema exige reiniciar el
/// proceso para que el prefijo se recalcule. Es lo correcto para lo que se
/// optimiza: un prefijo que se invalidara por su cuenta podría cambiar entre dos
/// turnos consecutivos —lo que RNF-14 prohíbe— y cada invalidación pagaría
/// escritura de caché sobre el bloque más grande del prompt. El despliegue ya
/// reinicia el proceso, y el hash del prefijo va sellado en cada reporte de
/// evaluación, así que una corrida contra un esquema viejo queda registrada como
/// tal en lugar de pasar desapercibida.
/// </remarks>
internal sealed class ProveedorDeEsquema(
    CadenaSoloLectura cadenaBasica,
    CadenaSoloLecturaPii cadenaConDatosPersonales) : IProveedorDeEsquema
{
    private readonly SemaphoreSlim _turnoDeCalculo = new(1, 1);
    private EsquemaParaPrompt? _basico;
    private EsquemaParaPrompt? _conDatosPersonales;

    /// <summary>Veces que se consultó la base. Existe para los tests del caché.</summary>
    internal int Lecturas { get; private set; }

    public async Task<EsquemaParaPrompt> ObtenerAsync(bool conDatosPersonales, CancellationToken ct)
    {
        var cacheado = conDatosPersonales ? _conDatosPersonales : _basico;
        if (cacheado is not null)
        {
            return cacheado;
        }

        // El semáforo evita que varios turnos concurrentes lo calculen a la vez
        // durante un arranque en frío. Se vuelve a mirar el caché adentro porque
        // el que esperó puede encontrarlo ya listo.
        await _turnoDeCalculo.WaitAsync(ct);
        try
        {
            cacheado = conDatosPersonales ? _conDatosPersonales : _basico;
            if (cacheado is not null)
            {
                return cacheado;
            }

            var construido = await ConstruirAsync(conDatosPersonales, ct);

            if (conDatosPersonales)
            {
                _conDatosPersonales = construido;
            }
            else
            {
                _basico = construido;
            }

            return construido;
        }
        finally
        {
            _turnoDeCalculo.Release();
        }
    }

    private async Task<EsquemaParaPrompt> ConstruirAsync(bool conDatosPersonales, CancellationToken ct)
    {
        var cadena = conDatosPersonales ? cadenaConDatosPersonales.Valor : cadenaBasica.Valor;

        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);

        var columnas = await LectorDeCatalogo.LeerColumnasAsync(conexion, ct);
        var referencias = await LectorDeCatalogo.LeerReferenciasAsync(conexion, ct);
        Lecturas++;

        var prefijo = RenderizadorDeEsquema.Renderizar(columnas, referencias);
        return new EsquemaParaPrompt(prefijo, Huella(prefijo));
    }

    /// <summary>
    /// Huella estable del prefijo.
    /// </summary>
    /// <remarks>
    /// SHA-256 y no <c>string.GetHashCode()</c>: en .NET el hash de string está
    /// aleatorizado por proceso. Un reporte de evaluación sellado con un valor
    /// que cambia en cada arranque no sella nada.
    /// </remarks>
    internal static string Huella(string prefijo) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prefijo)));
}
