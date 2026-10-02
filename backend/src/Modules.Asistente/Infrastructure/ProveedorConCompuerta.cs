using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Pide lugar en la <see cref="CompuertaDelModelo"/> antes de llamar al modelo
/// (asistente-proveedor-local, design.md D4).
/// </summary>
/// <remarks>
/// Va POR FUERA del breaker y de su timeout, y ese es el punto: la espera en la
/// cola no consume el tiempo de la llamada, y una cola larga no se cuenta como
/// fallo del proveedor. Sin esto, una GPU ocupada —sana— acumula timeouts, abre
/// el breaker y apaga el asistente para todos.
///
/// La prioridad sale del contador del turno: el techo ya reservó ESTA llamada
/// antes de llegar acá, así que más de una reservada significa que el turno ya
/// había llamado al modelo antes.
/// </remarks>
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
