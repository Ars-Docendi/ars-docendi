using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Guarda lo que se le mandó a registrar, sin tocar la base.
/// </summary>
/// <remarks>
/// Sirve para dos cosas distintas. La obvia es no necesitar tablas en los tests que
/// miden otra cosa. La que importa es poder afirmar <b>qué se le entregó al
/// registro</b>: la separación en dos filas ocurre adentro del escritor, así que
/// verificar la entrada es lo único que prueba que la capa no le está pasando de más
/// —una fila, una consulta generada— antes de que el escritor decida qué guardar.
/// </remarks>
public sealed class RegistroEnMemoria : IRegistroDelTurno
{
    private readonly List<TurnoParaRegistrar> _turnos = [];

    /// <summary>Todo lo que se mandó a registrar, en orden.</summary>
    public IReadOnlyList<TurnoParaRegistrar> Turnos => _turnos;

    public Task RegistrarAsync(TurnoParaRegistrar turno, CancellationToken ct)
    {
        _turnos.Add(turno);
        return Task.CompletedTask;
    }
}
