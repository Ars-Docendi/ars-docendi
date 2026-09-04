using Modules.Asistente.Application;

namespace ArsDocendi.Evaluacion.Nucleo.Runner;

/// <summary>
/// Envuelve al proveedor y mide lo que consumió cada ítem.
/// </summary>
/// <remarks>
/// <b>El eje social afirma «cero tokens de entrada», y eso no se puede afirmar
/// mirando el resultado del turno</b>: el contrato expone llamadas al modelo, no
/// tokens. Cero llamadas implica cero tokens, pero no al revés — un proveedor que
/// devuelve vacío consumió entrada igual.
///
/// Así que el número sale del transporte. El llamador envuelve el proveedor real con
/// esta clase y le pasa <b>la misma instancia</b> al runner, que la reinicia antes de
/// cada ítem y la lee después.
/// </remarks>
public sealed class MedidorDeConsumo(IProveedorDeModelo interno) : IProveedorDeModelo
{
    private readonly Lock _candado = new();

    public string Nombre => interno.Nombre;

    public bool EsSimulado => interno.EsSimulado;

    /// <summary>Tokens de entrada facturados desde el último reinicio.</summary>
    public int TokensDeEntrada { get; private set; }

    /// <summary>Tokens de salida facturados desde el último reinicio.</summary>
    public int TokensDeSalida { get; private set; }

    /// <summary>Llamadas emitidas desde el último reinicio.</summary>
    public int Llamadas { get; private set; }

    /// <summary>Pone el medidor en cero. Se llama antes de cada ítem.</summary>
    public void Reiniciar()
    {
        lock (_candado)
        {
            TokensDeEntrada = 0;
            TokensDeSalida = 0;
            Llamadas = 0;
        }
    }

    public async Task<RespuestaDelModelo> CompletarAsync(
        SolicitudAlModelo solicitud, CancellationToken ct)
    {
        // Se cuenta la llamada ANTES: una que falla también consumió transporte, y
        // para el assert de «el enrutador no capturó esta pregunta» lo que importa es
        // que haya salido, no que haya vuelto.
        lock (_candado)
        {
            Llamadas++;
        }

        var respuesta = await interno.CompletarAsync(solicitud, ct);

        lock (_candado)
        {
            TokensDeEntrada += respuesta.TokensDeEntrada;
            TokensDeSalida += respuesta.TokensDeSalida;
        }

        return respuesta;
    }
}
