using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Le pone timeout a cada llamada al modelo y alimenta el breaker con el resultado.
/// </summary>
/// <remarks>
/// Las dos cosas van juntas en un solo decorador a propósito: el timeout es una de
/// las dos formas de fallo que el breaker cuenta, y separarlos dejaría al breaker
/// sin ver la mitad de los fallos que le importan.
///
/// El timeout es <b>por llamada</b> y no es la cota del turno. La cota del turno la
/// pone <see cref="PresupuestoDelTurno"/>, encadenada por encima de ésta.
/// </remarks>
internal sealed class ProveedorConBreaker(
    IProveedorDeModelo interno,
    BreakerDelProveedor breaker,
    TimeSpan timeout,
    TimeProvider reloj) : IProveedorDeModelo
{
    public string Nombre => interno.Nombre;

    public bool EsSimulado => interno.EsSimulado;

    public async Task<RespuestaDelModelo> CompletarAsync(
        SolicitudAlModelo solicitud, CancellationToken ct)
    {
        if (!breaker.Permite())
        {
            // No es una excepción de transporte: no hubo transporte. Se distingue
            // para que el turno la resuelva como degradación y no como error.
            throw new ProveedorNoDisponible();
        }

        using var propio = timeout > TimeSpan.Zero
            ? new CancellationTokenSource(timeout, reloj)
            : null;

        using var enlazado = propio is null
            ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : CancellationTokenSource.CreateLinkedTokenSource(ct, propio.Token);

        try
        {
            var respuesta = await interno.CompletarAsync(solicitud, enlazado.Token);
            breaker.Exito();
            return respuesta;
        }
        catch (OperationCanceledException) when (propio is { IsCancellationRequested: true })
        {
            // El proveedor no contestó a tiempo. Cuenta como fallo: para el breaker,
            // un proveedor que tarda de más y uno que no responde son lo mismo.
            breaker.Fallo();
            throw new TimeoutDelProveedor(timeout);
        }
        catch (HttpRequestException)
        {
            breaker.Fallo();
            throw;
        }
    }
}
