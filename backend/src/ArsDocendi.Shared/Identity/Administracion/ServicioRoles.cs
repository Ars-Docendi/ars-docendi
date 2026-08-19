using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ArsDocendi.Shared.Aplicacion;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Shared.Identity.Administracion;

public sealed partial class ServicioRoles(
    IdentityDbContext db,
    IRepositorioRoles repositorio)
{
    private static readonly HashSet<string> Ambitos = ["global", "materia", "carrera"];

    public async Task<IReadOnlyList<RolAdministracionDto>> ListarAsync(CancellationToken ct) =>
        (await repositorio.ListarAsync(ct)).Select(Mapear).ToArray();

    public async Task<RolAdministracionDto> ObtenerAsync(Guid id, CancellationToken ct) =>
        Mapear(await ObtenerRequeridoAsync(id, false, ct));

    public async Task<IReadOnlyList<PermisoAdministracionDto>> ListarPermisosAsync(CancellationToken ct) =>
        (await repositorio.ListarPermisosAsync(ct)).Select(Mapear).ToArray();

    public async Task<RolAdministracionDto> CrearAsync(CrearRolDto datos, CancellationToken ct)
    {
        Validar(datos.Nombre, datos.Ambito);
        var codigo = GenerarCodigo(datos.Nombre);
        if (await repositorio.ExisteCodigoAsync(codigo, null, ct))
        {
            throw ConflictoCodigo();
        }

        Rol? baseRol = null;
        if (datos.RolBaseId is { } baseId)
        {
            baseRol = await repositorio.ObtenerAsync(baseId, false, ct)
                ?? throw NoEncontrado("No se encontró el rol base solicitado.");
        }
        var ahora = DateTimeOffset.UtcNow;
        var rol = new Rol
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            Nombre = datos.Nombre.Trim(),
            Descripcion = NormalizarOpcional(datos.Descripcion),
            Ambito = datos.Ambito,
            EsSistema = false,
            Activo = true,
            CreadoEn = ahora,
        };
        foreach (var permisoId in baseRol?.Permisos.Select(p => p.PermisoId) ?? [])
        {
            rol.Permisos.Add(new RolPermiso
            {
                RolId = rol.Id,
                PermisoId = permisoId,
                CreadoEn = ahora,
            });
        }

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        repositorio.Agregar(rol);
        await repositorio.GuardarAsync(ct);
        await transaccion.CommitAsync(ct);
        return await ObtenerAsync(rol.Id, ct);
    }

    public async Task<RolAdministracionDto> EditarAsync(
        Guid id,
        EditarRolDto datos,
        CancellationToken ct)
    {
        Validar(datos.Nombre, datos.Ambito);
        var rol = await ObtenerRequeridoAsync(id, true, ct);
        if (rol.EsSistema && rol.Ambito != datos.Ambito)
        {
            throw Protegido("El ámbito de un rol de sistema es inmutable.");
        }
        repositorio.EsperarVersion(rol, datos.Version);
        rol.Nombre = datos.Nombre.Trim();
        rol.Descripcion = NormalizarOpcional(datos.Descripcion);
        if (!rol.EsSistema) rol.Ambito = datos.Ambito;
        await repositorio.GuardarAsync(ct);
        return Mapear(rol);
    }

    public async Task<IReadOnlyList<PermisoAdministracionDto>> ReemplazarPermisosAsync(
        Guid id,
        ReemplazarPermisosDto datos,
        CancellationToken ct)
    {
        if (datos.PermisoIds.Distinct().Count() != datos.PermisoIds.Count)
        {
            throw PermisosInvalidos("No se pueden repetir permisos.");
        }
        var permisos = await repositorio.ObtenerPermisosAsync(datos.PermisoIds, ct);
        if (permisos.Count != datos.PermisoIds.Count)
        {
            throw PermisosInvalidos("Uno de los permisos no existe.");
        }
        var rol = await ObtenerRequeridoAsync(id, true, ct);
        repositorio.EsperarVersion(rol, datos.Version);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        repositorio.ReemplazarPermisos(rol, datos.PermisoIds);
        repositorio.MarcarCambioDeMembresia(rol);
        await repositorio.GuardarAsync(ct);
        await transaccion.CommitAsync(ct);
        return permisos.OrderBy(p => p.Nombre).Select(Mapear).ToArray();
    }

    private async Task<Rol> ObtenerRequeridoAsync(Guid id, bool tracking, CancellationToken ct) =>
        await repositorio.ObtenerAsync(id, tracking, ct)
        ?? throw NoEncontrado("No se encontró el rol solicitado.");

    private static void Validar(string nombre, string ambito)
    {
        var errores = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(nombre)) errores["nombre"] = ["Campo obligatorio."];
        if (!Ambitos.Contains(ambito)) errores["ambito"] = ["Ámbito inválido."];
        if (errores.Count > 0)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion,
                "validation",
                "Revisá los datos del rol.",
                errores);
        }
    }

    private static string GenerarCodigo(string nombre)
    {
        var normalizado = nombre.Trim().Normalize(NormalizationForm.FormD);
        var sinDiacriticos = new string(normalizado
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return Separadores().Replace(sinDiacriticos.ToLowerInvariant(), "_").Trim('_');
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static ExcepcionAplicacion NoEncontrado(string mensaje) => new(
        TipoErrorAplicacion.NoEncontrado, "resource-not-found", mensaje);

    private static ExcepcionAplicacion ConflictoCodigo() => new(
        TipoErrorAplicacion.Conflicto,
        "identity-role-code-conflict",
        "Ya existe un rol con ese código.");

    private static ExcepcionAplicacion Protegido(string mensaje) => new(
        TipoErrorAplicacion.ReglaDeNegocio, "identity-protected-role", mensaje);

    private static ExcepcionAplicacion PermisosInvalidos(string mensaje) => new(
        TipoErrorAplicacion.ReglaDeNegocio, "identity-permission-invalid", mensaje);

    private static RolAdministracionDto Mapear(Rol rol) => new(
        rol.Id,
        rol.Codigo,
        rol.Nombre,
        rol.Descripcion,
        rol.Ambito,
        rol.EsSistema,
        rol.Activo,
        rol.Version,
        rol.Permisos
            .Where(rp => rp.Permiso is not null)
            .Select(rp => Mapear(rp.Permiso!))
            .OrderBy(p => p.Nombre)
            .ToArray());

    private static PermisoAdministracionDto Mapear(Permiso permiso) => new(
        permiso.Id, permiso.Codigo, permiso.Nombre, permiso.Descripcion);

    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private static partial Regex Separadores();
}
