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
internal sealed class ProveedorDeEsquema(AperturaDeLectura apertura) : IProveedorDeEsquema
{
    private readonly ValorPerezosoPorRol<EsquemaParaPrompt> _porRol = new();

    /// <summary>Veces que se consultó la base. Existe para los tests del caché.</summary>
    internal int Lecturas => _porRol.Calculos;

    public Task<EsquemaParaPrompt> ObtenerAsync(bool conDatosPersonales, CancellationToken ct) =>
        _porRol.ObtenerAsync(
            conDatosPersonales, token => ConstruirAsync(conDatosPersonales, token), ct);

    private async Task<EsquemaParaPrompt> ConstruirAsync(bool conDatosPersonales, CancellationToken ct)
    {
        await using var conexion = await apertura.AbrirAsync(conDatosPersonales, ct);

        var columnas = await LectorDeCatalogo.LeerColumnasAsync(conexion, ct);
        var referencias = await LectorDeCatalogo.LeerReferenciasAsync(conexion, ct);

        // Los valores de los catálogos cerrados viajan con el esquema y se cachean
        // igual: son tan estables como los nombres de las columnas, y sin ellos el
        // modelo tiene que adivinar cómo está escrito «Ingeniería en Informática».
        var vocabularios = await LectorDeValoresDeCatalogo.LeerAsync(conexion, ct);

        var prefijo = RenderizadorDeEsquema.Renderizar(columnas, referencias, vocabularios);
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
