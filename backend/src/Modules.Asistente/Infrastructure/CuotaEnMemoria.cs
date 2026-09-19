using Microsoft.Extensions.Options;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Cupo por actor con ventana deslizante, en memoria (RF-20).
/// </summary>
/// <remarks>
/// <b>Vive en memoria, y el costo de eso hay que decirlo</b>: un redespliegue le
/// devuelve el cupo a todo el mundo.
///
/// Se aceptó por el modelo de amenaza. Son unas treinta personas autenticadas con
/// credenciales institucionales, ninguna puede forzar un redespliegue, y el techo
/// de gasto duro no vive acá sino en la consola del proveedor, por ambiente
/// (RNF-12). Esta cuota es un mecanismo de <b>equidad entre usuarios</b>, no la
/// última línea contra una factura.
///
/// Persistirla exigiría una tabla escrita en el camino caliente de cada llamada,
/// con su propia retención de datos personales indirectos —quién consultó, cuándo y
/// cuánto— sobre un sistema que decidió no persistir ni el hilo conversacional.
/// </remarks>
internal sealed class CuotaEnMemoria(IOptions<OpcionesAsistente> opciones, TimeProvider reloj)
    : ICuotaDelActor
{
    private readonly Dictionary<Guid, Queue<Consumo>> _porActor = [];
    private readonly Lock _candado = new();

    public bool HayCupo(Guid actor)
    {
        var valores = opciones.Value;
        if (valores.CupoDeLlamadasPorActor <= 0)
        {
            return true;
        }

        lock (_candado)
        {
            return Consumidas(actor, valores) < valores.CupoDeLlamadasPorActor;
        }
    }

    public void Anotar(Guid actor, int llamadas)
    {
        if (llamadas <= 0 || opciones.Value.CupoDeLlamadasPorActor <= 0)
        {
            return;
        }

        lock (_candado)
        {
            if (!_porActor.TryGetValue(actor, out var cola))
            {
                cola = new Queue<Consumo>();
                _porActor[actor] = cola;
            }

            cola.Enqueue(new Consumo(reloj.GetUtcNow(), llamadas));
        }
    }

    public DateTimeOffset? CupoVuelveA(Guid actor)
    {
        var valores = opciones.Value;
        if (valores.CupoDeLlamadasPorActor <= 0)
        {
            return null;
        }

        lock (_candado)
        {
            if (Consumidas(actor, valores) < valores.CupoDeLlamadasPorActor)
            {
                return null;
            }

            // El más viejo de la ventana es el primero que sale de ella, así que es
            // el momento exacto en que vuelve a haber al menos una llamada de cupo.
            return _porActor[actor].Peek().Cuando + Ventana(valores);
        }
    }

    /// <summary>Suma lo consumido dentro de la ventana, descartando lo que salió.</summary>
    /// <remarks>
    /// Purga al leer, así que la memoria de un actor que dejó de consultar se libera
    /// sola la próxima vez que se lo mire. Un actor que nunca vuelve deja una entrada
    /// vacía; con el orden de magnitud de usuarios de este sistema, no vale una
    /// pasada de barrido.
    /// </remarks>
    private int Consumidas(Guid actor, OpcionesAsistente valores)
    {
        if (!_porActor.TryGetValue(actor, out var cola))
        {
            return 0;
        }

        var corte = reloj.GetUtcNow() - Ventana(valores);

        while (cola.Count > 0 && cola.Peek().Cuando <= corte)
        {
            cola.Dequeue();
        }

        return cola.Sum(consumo => consumo.Llamadas);
    }

    private static TimeSpan Ventana(OpcionesAsistente valores) =>
        TimeSpan.FromMinutes(valores.VentanaDeCuotaMinutos);

    private readonly record struct Consumo(DateTimeOffset Cuando, int Llamadas);
}
