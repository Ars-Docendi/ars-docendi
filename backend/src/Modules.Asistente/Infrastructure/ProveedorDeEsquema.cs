using System.Security.Cryptography;
using System.Text;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>Construye el prefijo del prompt de sistema y lo cachea por rol.</summary>
internal sealed class ProveedorDeEsquema(
    AperturaDeLectura apertura, IOptions<OpcionesAsistente>? opciones = null) : IProveedorDeEsquema
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
        var conGlosario = opciones?.Value.GlosarioEnElPrefijo ?? false;
        var vocabularios = await LectorDeValoresDeCatalogo.LeerAsync(conexion, ct, conGlosario);

        // EL GLOSARIO SE LEE SÓLO CON LA OPCIÓN PRENDIDA: un archivo mal formado no puede tumbar un
        // despliegue con el default.
        var bloqueDeGlosario = conGlosario
            ? RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Vigente.Terminos)
            : null;

        var prefijo = RenderizadorDeEsquema.Renderizar(
            columnas, referencias, vocabularios, opciones?.Value.EsquemaCompacto ?? false, bloqueDeGlosario);
        return new EsquemaParaPrompt(prefijo, Huella(prefijo));
    }

    /// <summary>Huella estable del prefijo.</summary>
    internal static string Huella(string prefijo) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prefijo)));
}
