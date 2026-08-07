using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Repositories;

/// <summary>
/// Persistencia de pedidos de designación. Interno al módulo: sólo lo consumen los
/// servicios, nunca un controller (Controller → Service → Repository).
/// </summary>
internal interface IRepositorioPedidos
{
    /// <summary>Trae el pedido con sus adjuntos e historial, o <c>null</c> si no existe.</summary>
    Task<Pedido?> ObtenerPorIdAsync(Guid pedidoId, CancellationToken ct);

    /// <summary>Trae el pedido sin colecciones asociadas. Para transiciones de estado.</summary>
    Task<Pedido?> ObtenerLivianoAsync(Guid pedidoId, CancellationToken ct);

    /// <summary>
    /// ¿La persona ya tiene un pedido que ocupa cupo en el período?
    /// [BR-designaciones-001]. Es la validación previa que produce el mensaje de
    /// error del spec; la autoridad real es el índice único parcial.
    /// </summary>
    Task<bool> ExisteVivoParaPersonaEnPeriodoAsync(Guid periodoId, Guid personaId, CancellationToken ct);

    /// <summary>Pedidos de las materias indicadas dentro del período. Vista del Jefe de Cátedra.</summary>
    Task<IReadOnlyList<Pedido>> ListarPorMateriasAsync(
        Guid periodoId, IReadOnlyCollection<Guid> materiaIds, CancellationToken ct);

    /// <summary>
    /// Pedidos del período acotados a un conjunto de carreras. Vista del Coordinador.
    /// La carrera se deriva de la materia; el pedido no la desnormaliza.
    /// </summary>
    Task<IReadOnlyList<Pedido>> ListarPorCarrerasAsync(
        Guid periodoId, IReadOnlyCollection<Guid> carreraIds, CancellationToken ct);

    /// <summary>Todos los pedidos del período. Vista depto-wide (Secretaría, Decanato, Administrativo).</summary>
    Task<IReadOnlyList<Pedido>> ListarDelPeriodoAsync(Guid periodoId, CancellationToken ct);

    /// <summary>Carrera a la que pertenece la materia del pedido, derivada de identity.</summary>
    Task<Guid> ObtenerCarreraDelPedidoAsync(Guid pedidoId, CancellationToken ct);

    /// <summary>Reserva el próximo número de trámite legible (formato <c>AAAA-NNNN</c>).</summary>
    Task<string> SiguienteNumeroAsync(CancellationToken ct);

    void Agregar(Pedido pedido);

    void Eliminar(Pedido pedido);

    /// <summary>
    /// Persiste los cambios pendientes.
    /// <para>
    /// Traduce la violación del índice <c>pedidos_uno_por_docente_periodo</c> a
    /// <see cref="ErrorPedidoDuplicado"/>, para que el duplicado que gana la carrera
    /// de concurrencia produzca el mismo error de dominio que la validación previa y
    /// no un 500 con detalle de PostgreSQL.
    /// </para>
    /// </summary>
    Task GuardarCambiosAsync(CancellationToken ct);
}
