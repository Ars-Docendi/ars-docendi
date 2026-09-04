using Modules.Designaciones.Domain;
using Modules.Designaciones.Repositories;

namespace Modules.Designaciones.Services;

/// <summary>
/// Traduce un pedido aprobado a escrituras sobre el estado vigente.
/// <para>
/// Es el punto donde el trámite deja de ser un papel y pasa a ser la realidad del
/// sistema. Cada novedad tiene una traducción distinta, y todas quedan trazables
/// hacia atrás por <c>origen_pedido_id</c>.
/// </para>
/// <para>
/// No abre transacción: lo hace quien lo invoca. La atomicidad importa sobre todo en
/// "Cambio", que cierra una designación y abre otra — si la apertura falla, el cierre
/// tiene que revertirse o el docente queda sin designación vigente.
/// </para>
/// </summary>
internal sealed class MaterializadorDesignaciones(IRepositorioDesignaciones repositorio)
{
    public async Task MaterializarAsync(Pedido pedido, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        switch (pedido.Novedad)
        {
            case Novedades.Alta:
                Abrir(pedido, hoy);
                break;

            case Novedades.Baja:
                await CerrarVigenteAsync(pedido, hoy, ct);
                break;

            case Novedades.CambioDeCargoODedicacion:
                await CerrarVigenteAsync(pedido, hoy, ct);
                Abrir(pedido, hoy);
                break;

            case Novedades.SinNovedad:
                // Deliberadamente sin efecto: "Sin novedad" confirma la continuidad del
                // docente en la cátedra, no modifica su designación vigente.
                break;

            default:
                throw new ErrorDominioPedido($"Novedad no reconocida: \"{pedido.Novedad}\".");
        }
    }

    /// <summary>
    /// Abre la designación que el pedido solicita. La constraint EXCLUDE de la base
    /// rechaza el solapamiento, así que un Alta sobre una cátedra donde el docente ya
    /// está vigente falla acá y no produce un estado imposible.
    /// </summary>
    private void Abrir(Pedido pedido, DateOnly desde)
    {
        if (pedido.CargoSolicitadoId is null)
        {
            throw new ErrorDominioPedido(
                $"El pedido {pedido.Numero} no tiene cargo solicitado: no se puede abrir la designación.");
        }

        if (pedido.Horas is null)
        {
            throw new ErrorDominioPedido(
                $"El pedido {pedido.Numero} no tiene carga horaria: no se puede abrir la designación.");
        }

        repositorio.Agregar(new Designacion
        {
            PersonaId = pedido.PersonaId,
            MateriaId = pedido.MateriaId,
            CargoId = pedido.CargoSolicitadoId.Value,
            Dedicacion = pedido.DedicacionSolicitada,
            Horas = pedido.Horas.Value,
            VigenteDesde = desde,
            OrigenPedidoId = pedido.Id,
        });
    }

    /// <summary>
    /// Cierra la designación vigente del docente en la cátedra del pedido. Cerrar es
    /// fijar <c>vigente_hasta</c>, nunca borrar la fila: el historial de designaciones
    /// es parte del registro.
    /// </summary>
    private async Task CerrarVigenteAsync(Pedido pedido, DateOnly hasta, CancellationToken ct)
    {
        var vigente = await repositorio.ObtenerVigenteAsync(pedido.PersonaId, pedido.MateriaId, ct)
            ?? throw new ErrorDominioPedido(
                $"El pedido {pedido.Numero} ({pedido.Novedad}) no tiene una designación vigente que cerrar.");

        // La constraint designaciones_vigencia_coherente exige hasta > desde. Una baja
        // el mismo día que el alta cierra al día siguiente en vez de fallar.
        vigente.VigenteHasta = hasta > vigente.VigenteDesde
            ? hasta
            : vigente.VigenteDesde.AddDays(1);
    }
}
