using ArsDocendi.Shared.Identity.Administracion;
using Modules.Designaciones.Contracts.Administracion;

namespace ArsDocendi.Host.Administracion;

public sealed class ServicioUsuariosAdministracion(
    ServicioUsuarios usuarios,
    IAdministracionDesignaciones designaciones)
{
    public async Task<IReadOnlyList<UsuarioAdministracionDto>> ListarAsync(CancellationToken ct)
    {
        var usuariosActuales = await usuarios.ListarAsync(ct);
        var vigentes = await designaciones.ListarVigentesAsync(ct);
        return usuariosActuales.Select(usuario => Enriquecer(usuario, vigentes)).ToArray();
    }

    public async Task<UsuarioAdministracionDto> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var usuario = await usuarios.ObtenerAsync(id, ct);
        var vigentes = await designaciones.ListarVigentesAsync(ct);
        return Enriquecer(usuario, vigentes);
    }

    public async Task<UsuarioAdministracionDto> CrearAsync(GuardarUsuarioDto datos, CancellationToken ct) =>
        await EnriquecerAsync(await usuarios.CrearAsync(datos, ct), ct);

    public async Task<UsuarioAdministracionDto> EditarAsync(
        Guid id,
        GuardarUsuarioDto datos,
        CancellationToken ct) => await EnriquecerAsync(await usuarios.EditarAsync(id, datos, ct), ct);

    public async Task<UsuarioAdministracionDto> CambiarEstadoAsync(
        Guid id,
        bool activo,
        uint version,
        CancellationToken ct) => await EnriquecerAsync(await usuarios.CambiarEstadoAsync(id, activo, version, ct), ct);

    private async Task<UsuarioAdministracionDto> EnriquecerAsync(
        UsuarioAdministracionDto usuario,
        CancellationToken ct) => Enriquecer(usuario, await designaciones.ListarVigentesAsync(ct));

    private static UsuarioAdministracionDto Enriquecer(
        UsuarioAdministracionDto usuario,
        IReadOnlyList<DesignacionVigenteDto> vigentes)
    {
        var materias = usuario.Membresias
            .Where(m => m.Codigo is "docente" or "jefe_catedra")
            .Select(m => m.MateriaId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Concat(vigentes.Where(d => d.PersonaId == usuario.PersonaId).Select(d => d.MateriaId))
            .Distinct()
            .Count();
        var esDocente = usuario.PerfilDocente.EsDocente
            || vigentes.Any(d => d.PersonaId == usuario.PersonaId);
        return usuario with { PerfilDocente = new PerfilDocenteDto(esDocente, materias) };
    }
}
