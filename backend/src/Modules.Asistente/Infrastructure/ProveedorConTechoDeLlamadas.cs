using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Envuelve al proveedor y le cobra al contador del turno cada llamada (RNF-10).
/// </summary>
/// <remarks>
/// Es un decorador y no un chequeo dentro de cada capa a propósito: el techo tiene
/// que ser global del turno. Repartido por capa, cada una respeta su límite y el
/// total se multiplica igual — que es exactamente el modo de falla del que este
/// requisito nace.
///
/// Como todo el pipeline pide <see cref="IProveedorDeModelo"/> por inyección,
/// ninguna capa puede saltearse el contador sin dejar de usar el proveedor.
/// </remarks>
internal sealed class ProveedorConTechoDeLlamadas(
    IProveedorDeModelo interno,
    ContadorDeLlamadasDelTurno contador) : IProveedorDeModelo
{
    public string Nombre => interno.Nombre;

    public bool EsSimulado => interno.EsSimulado;

    public async Task<RespuestaDelModelo> CompletarAsync(
        SolicitudAlModelo solicitud, CancellationToken ct)
    {
        // Se reserva ANTES de llamar. Contar después dejaría pasar una llamada de
        // más cada vez que el proveedor falla, que es justo cuando más se reintenta.
        contador.Reservar();

        var respuesta = await interno.CompletarAsync(solicitud, ct);

        // Los tokens, en cambio, se contabilizan DESPUÉS y solo si hubo respuesta:
        // una llamada que falló no facturó nada, y sumarle los tokens de la anterior
        // haría que el registro operativo informara un consumo que no existió.
        contador.Contabilizar(respuesta);

        return respuesta;
    }
}

/// <summary>
/// El proveedor elegido por configuración, todavía sin el techo del turno.
/// </summary>
/// <remarks>
/// Existe solo para la registración: el proveedor concreto vive como singleton y
/// el decorador con el contador es scoped, así que hacen falta dos entradas y esta
/// es la de adentro.
/// </remarks>
internal sealed record ProveedorBase(IProveedorDeModelo Valor);
