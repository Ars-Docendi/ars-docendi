using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>Envuelve al proveedor y le cobra al contador del turno cada llamada (RNF-10).</summary>
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

/// <summary>El proveedor elegido por configuración, todavía sin el techo del turno.</summary>
internal sealed record ProveedorBase(IProveedorDeModelo Valor);

/// <summary>
/// El proveedor base que redacta, que puede ser un modelo distinto del que genera.
/// </summary>
internal sealed record BaseDeRedaccion(IProveedorDeModelo Valor);
