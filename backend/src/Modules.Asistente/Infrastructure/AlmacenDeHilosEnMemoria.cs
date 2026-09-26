using Microsoft.Extensions.Options;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Almacén de hilos en memoria del proceso, con expiración por inactividad.
/// </summary>
/// <remarks>
/// <b>No se persiste, y es una decisión tomada.</b> El hilo se pierde en cada
/// redespliegue y eso se acepta: persistirlo agregaría tabla, migración y una
/// política de retención de datos personales indirectos sin mover ninguna métrica
/// del proyecto. Se revisa si aparece evidencia medida de que la pérdida molesta.
///
/// Consecuencia que hay que tener presente: con más de una instancia del Host, dos
/// turnos del mismo hilo pueden caer en procesos distintos y el segundo arranca sin
/// historial. Degrada el seguimiento; no rompe el turno.
/// </remarks>
internal sealed class AlmacenDeHilosEnMemoria(
    IOptions<OpcionesAsistente> opciones,
    TimeProvider reloj) : IAlmacenDeHilos
{
    private readonly Dictionary<Guid, HiloConversacional> _hilos = [];
    private readonly Lock _candado = new();

    public HiloConversacional Resolver(Guid? hilo, Guid actor)
    {
        var ahora = reloj.GetUtcNow();
        var vigencia = TimeSpan.FromMinutes(opciones.Value.VigenciaDelHiloMinutos);

        lock (_candado)
        {
            Purgar(ahora, vigencia);

            if (hilo is { } identificador && _hilos.TryGetValue(identificador, out var existente))
            {
                if (existente.Actor != actor)
                {
                    throw new HiloAjeno(identificador);
                }

                existente.Tocar(ahora);
                return existente;
            }

            // Inexistente o vencido: hilo nuevo, sin error. Perder el hilo degrada
            // el seguimiento pero una pregunta autocontenida sigue funcionando.
            var nuevo = new HiloConversacional(Guid.NewGuid(), actor);
            nuevo.Tocar(ahora);
            _hilos[nuevo.Id] = nuevo;
            return nuevo;
        }
    }

    public HiloConversacional Sembrar(Guid actor, Guid hiloHistorico, IReadOnlyList<TurnoDelHilo> turnos)
    {
        ArgumentNullException.ThrowIfNull(turnos);

        var ahora = reloj.GetUtcNow();
        var nuevo = new HiloConversacional(Guid.NewGuid(), actor) { HiloHistorico = hiloHistorico };

        foreach (var turno in turnos)
        {
            // `turnoHistoricoId` y no `claveDelCliente`: un turno sembrado por
            // «Reanudar» no tiene una Idempotency-Key de esta sesión, así que
            // un reemplazo sobre él se nombra por su id de turno_historico
            // (design.md D9 de asistente-rediseno-v3).
            nuevo.Agregar(
                turno.Pregunta, turno.Cuando, turno.SqlEjecutado, turnoHistoricoId: turno.TurnoHistoricoId);
        }

        if (turnos.Count > 0)
        {
            // EL ÚLTIMO TURNO SEMBRADO TIENE QUE PODER REEMPLAZARSE DE
            // ENTRADA, SEA CUAL SEA SU ESTADO ORIGINAL (design.md D9): a
            // diferencia de un turno vivo, Reanudar no distingue por carril
            // —cada fila de `turno_historico` ya se cargó arriba, incluida
            // ésta—, así que alcanza con nombrar la última. Sin token de
            // retroalimentación: el que tuvo en vivo ya no está vigente en
            // este proceso, y revocar uno inexistente no hace nada.
            var ultimo = turnos[^1];
            nuevo.MarcarUltimoRegistrado(
                claveDelCliente: null,
                ultimo.TurnoHistoricoId,
                claveDeRetroalimentacion: null,
                inicioDeSegmentoAntes: 0,
                aclaracionPendienteAntes: null);
        }

        // Se toca DESPUÉS de cargar los turnos: `Agregar` deja `UltimaActividad`
        // en el momento del ÚLTIMO turno persistido, que puede ser de hace
        // semanas. Sin este `Tocar`, un hilo recién sembrado podría nacer ya
        // vencido si esa conversación estuvo inactiva más que la vigencia.
        nuevo.Tocar(ahora);

        lock (_candado)
        {
            Purgar(ahora, TimeSpan.FromMinutes(opciones.Value.VigenciaDelHiloMinutos));
            _hilos[nuevo.Id] = nuevo;
        }

        return nuevo;
    }

    /// <summary>Cuántos hilos hay vivos. Existe para los tests de expiración.</summary>
    internal int Vivos
    {
        get
        {
            lock (_candado)
            {
                return _hilos.Count;
            }
        }
    }

    /// <summary>
    /// Saca los hilos vencidos.
    /// </summary>
    /// <remarks>
    /// Se purga al resolver y no con un temporizador de fondo: sin tráfico no hay
    /// nada que purgar, y un temporizador sería un hilo más corriendo para no hacer
    /// nada la mayor parte del tiempo.
    /// </remarks>
    private void Purgar(DateTimeOffset ahora, TimeSpan vigencia)
    {
        var vencidos = _hilos
            .Where(par => ahora - par.Value.UltimaActividad > vigencia)
            .Select(par => par.Key)
            .ToArray();

        foreach (var vencido in vencidos)
        {
            _hilos.Remove(vencido);
        }
    }
}
