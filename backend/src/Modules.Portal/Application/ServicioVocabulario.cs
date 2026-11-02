using ArsDocendi.Shared.Aplicacion;
using Modules.Portal.Api;
using Modules.Portal.Repositories;

namespace Modules.Portal.Application;

/// <summary>Curaduría del vocabulario compartido de habilidades e intereses.</summary>
public sealed class ServicioVocabulario(RepositorioVocabulario repositorio)
{
    public Task<IReadOnlyList<string>> ListarOfrecidosAsync(CancellationToken ct) =>
        repositorio.ListarOfrecidosAsync(ct);

    public Task<IReadOnlyList<TerminoVocabularioDto>> ListarAsync(CancellationToken ct) =>
        repositorio.ListarAsync(ct);

    /// <summary>BR-portal-003: un término sugerido se ofrece recién al incorporarlo.</summary>
    public async Task IncorporarAsync(Guid id, CancellationToken ct)
    {
        var termino = await ObtenerVigenteAsync(id, ct);
        termino.Sugerido = false;
        await repositorio.GuardarAsync(ct);
    }

    public async Task RenombrarAsync(Guid id, RenombrarTerminoDto datos, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(datos.Termino))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion, "validation", "Indicá el término.",
                new Dictionary<string, string[]> { ["termino"] = ["Campo obligatorio."] });
        }
        var termino = await ObtenerVigenteAsync(id, ct);
        termino.Termino = datos.Termino.Trim();
        // BR-portal-002: el índice único de termino_norm rechaza un nombre que ya existe.
        termino.TerminoNorm = RepositorioPortal.Normalizar(datos.Termino);
        await repositorio.GuardarAsync(ct);
    }

    /// <summary>BR-portal-004: los usos de <paramref name="id"/> pasan a <c>destinoId</c>.</summary>
    public async Task FusionarAsync(Guid id, FusionarTerminoDto datos, CancellationToken ct)
    {
        if (id == datos.DestinoId) throw Conflicto("No se puede fusionar un término consigo mismo.");
        await ObtenerVigenteAsync(id, ct);
        var destino = await repositorio.ObtenerAsync(datos.DestinoId, ct) ?? throw NoEncontrado();
        if (destino.CanonicaId is not null) throw Conflicto("El destino ya está fusionado en otro término.");
        // Así las cadenas tienen un solo salto y guardar una variante resuelve con una sola lectura.
        if (await repositorio.EsCanonicoDeOtrosAsync(id, ct))
            throw Conflicto("Otros términos ya se fusionaron en este; fusioná el destino en él.");
        await repositorio.FusionarAsync(id, datos.DestinoId, ct);
    }

    private async Task<Domain.Habilidad> ObtenerVigenteAsync(Guid id, CancellationToken ct)
    {
        var termino = await repositorio.ObtenerAsync(id, ct) ?? throw NoEncontrado();
        if (termino.CanonicaId is not null) throw Conflicto("El término ya está fusionado.");
        return termino;
    }

    private static ExcepcionAplicacion NoEncontrado() =>
        new(TipoErrorAplicacion.NoEncontrado, "termino-not-found", "No se encontró el término.");

    private static ExcepcionAplicacion Conflicto(string mensaje) =>
        new(TipoErrorAplicacion.Conflicto, "termino-no-fusionable", mensaje);
}
