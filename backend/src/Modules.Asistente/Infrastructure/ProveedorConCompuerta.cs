using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Pide lugar en la <see cref="CompuertaDelModelo"/> antes de llamar al modelo
/// (asistente-proveedor-local, design.md D4).
/// </summary>
internal sealed class ProveedorConCompuerta(
    IProveedorDeModelo interno,
    CompuertaDelModelo compuerta,
    TimeSpan esperaMaxima,
    ContadorDeLlamadasDelTurno contador) : IProveedorDeModelo
{
    public string Nombre => interno.Nombre;

    public bool EsSimulado => interno.EsSimulado;

    public async Task<RespuestaDelModelo> CompletarAsync(
        SolicitudAlModelo solicitud, CancellationToken ct)
    {
        if (!await compuerta.EntrarAsync(contador.Llamadas > 1, esperaMaxima, ct))
        {
            throw new ProveedorSaturado(esperaMaxima);
        }

        try
        {
            return await interno.CompletarAsync(solicitud, ct);
        }
        finally
        {
            compuerta.Salir();
        }
    }
}
