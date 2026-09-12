using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>En qué estado está el proveedor según los últimos intentos.</summary>
public enum EstadoDelBreaker
{
    /// <summary>El proveedor responde: las llamadas pasan.</summary>
    Cerrado,

    /// <summary>Viene fallando: no se lo llama.</summary>
    Abierto,

    /// <summary>Se lo está probando con una sola llamada.</summary>
    EnPrueba,
}

/// <summary>
/// Corta las llamadas al proveedor cuando viene fallando, y las restablece solo.
/// </summary>
/// <remarks>
/// Cuenta <b>fallos de transporte y de timeout</b>, nunca rechazos semánticos. Un
/// modelo que devuelve una respuesta que el validador descarta está sano: cortarle
/// las llamadas por eso apagaría el asistente cada vez que alguien hace una
/// pregunta difícil.
///
/// En prueba deja pasar <b>una sola</b> llamada, no una por turno. Con varios
/// turnos concurrentes, «una por turno» significa una avalancha contra un proveedor
/// que recién se está recuperando, que es exactamente el escenario que el breaker
/// existe para evitar.
///
/// Es singleton: el estado del proveedor es del proceso, no del request.
/// </remarks>
internal sealed class BreakerDelProveedor(
    IOptions<OpcionesAsistente> opciones,
    TimeProvider reloj,
    ILogger<BreakerDelProveedor> log)
{
    private readonly Lock _candado = new();

    private int _fallosSeguidos;
    private DateTimeOffset _abiertoHasta;
    private bool _pruebaEnCurso;

    /// <summary>Estado actual, resuelto contra el reloj.</summary>
    public EstadoDelBreaker Estado
    {
        get
        {
            lock (_candado)
            {
                return EstadoInterno();
            }
        }
    }

    /// <summary>
    /// Si esta llamada puede salir. En prueba, se la concede a una sola.
    /// </summary>
    public bool Permite()
    {
        if (opciones.Value.FallosParaAbrirElBreaker <= 0)
        {
            return true;
        }

        lock (_candado)
        {
            switch (EstadoInterno())
            {
                case EstadoDelBreaker.Cerrado:
                    return true;

                case EstadoDelBreaker.EnPrueba when !_pruebaEnCurso:
                    _pruebaEnCurso = true;
                    return true;

                default:
                    return false;
            }
        }
    }

    /// <summary>Una llamada funcionó: el proveedor está sano.</summary>
    public void Exito()
    {
        lock (_candado)
        {
            if (_fallosSeguidos > 0 || _pruebaEnCurso)
            {
                log.LogInformation("El proveedor del modelo volvió a responder: se cierra el corte.");
            }

            _fallosSeguidos = 0;
            _abiertoHasta = default;
            _pruebaEnCurso = false;
        }
    }

    /// <summary>Una llamada falló por transporte o por timeout.</summary>
    public void Fallo()
    {
        var valores = opciones.Value;
        if (valores.FallosParaAbrirElBreaker <= 0)
        {
            return;
        }

        lock (_candado)
        {
            // Un fallo durante la prueba reabre de una y reinicia la espera: no hace
            // falta volver a acumular el umbral entero para descubrir lo que se
            // acaba de comprobar.
            if (_pruebaEnCurso)
            {
                _pruebaEnCurso = false;
                Abrir(valores);
                return;
            }

            _fallosSeguidos++;

            if (_fallosSeguidos >= valores.FallosParaAbrirElBreaker)
            {
                Abrir(valores);
            }
        }
    }

    private void Abrir(OpcionesAsistente valores)
    {
        _abiertoHasta = reloj.GetUtcNow() + TimeSpan.FromSeconds(valores.EsperaDelBreakerSegundos);

        log.LogWarning(
            "Se corta el paso al proveedor del modelo por {Fallos} fallos seguidos; se reintenta en {Espera}s.",
            _fallosSeguidos,
            valores.EsperaDelBreakerSegundos);
    }

    private EstadoDelBreaker EstadoInterno()
    {
        if (_abiertoHasta == default)
        {
            return EstadoDelBreaker.Cerrado;
        }

        return reloj.GetUtcNow() < _abiertoHasta
            ? EstadoDelBreaker.Abierto
            : EstadoDelBreaker.EnPrueba;
    }
}
