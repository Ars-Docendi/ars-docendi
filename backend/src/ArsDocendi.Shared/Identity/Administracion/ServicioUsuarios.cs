using ArsDocendi.Shared.Aplicacion;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Shared.Identity.Administracion;

public sealed class ServicioUsuarios(
    IdentityDbContext db,
    IRepositorioUsuarios repositorio)
{
    public async Task<IReadOnlyList<UsuarioAdministracionDto>> ListarAsync(CancellationToken ct) =>
        (await repositorio.ListarAsync(ct)).Select(Mapear).ToArray();

    public async Task<UsuarioAdministracionDto> ObtenerAsync(Guid id, CancellationToken ct) =>
        Mapear(await ObtenerRequeridoAsync(id, ct));

    public Task<CatalogosUsuariosDto> ObtenerCatalogosAsync(CancellationToken ct) =>
        repositorio.ObtenerCatalogosAsync(ct);

    public async Task<UsuarioAdministracionDto> CrearAsync(
        GuardarUsuarioDto datos,
        CancellationToken ct)
    {
        ValidarDatos(datos);
        var upn = NormalizarUpn(datos.Upn);
        var documento = datos.Documento.Trim();
        await ValidarUnicidadAsync(upn, documento, null, null, ct);
        var asignaciones = await ConstruirAsignacionesAsync(datos.Roles, ct);
        var ahora = DateTimeOffset.UtcNow;
        var persona = new Persona
        {
            Id = Guid.NewGuid(),
            Documento = documento,
            Cuil = NormalizarOpcional(datos.Cuil),
            Legajo = NormalizarOpcional(datos.Legajo),
            Nombre = datos.Nombre.Trim(),
            Apellido = datos.Apellido.Trim(),
            FechaNacimiento = datos.FechaNacimiento,
            Telefono = NormalizarOpcional(datos.Telefono),
            CreadoEn = ahora,
        };
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            // OID provisional: el vinculador de primer login lo reemplaza al
            // encontrar esta cuenta por UPN.
            AzureOid = Guid.NewGuid(),
            Upn = upn,
            NombreParaMostrar = $"{persona.Nombre} {persona.Apellido}",
            Activo = true,
            PersonaId = persona.Id,
            Persona = persona,
            CreadoEn = ahora,
        };
        foreach (var asignacion in asignaciones)
        {
            asignacion.UsuarioId = usuario.Id;
            usuario.Roles.Add(asignacion);
        }

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        repositorio.Agregar(persona, usuario);
        await repositorio.GuardarAsync(ct);
        await transaccion.CommitAsync(ct);
        return Mapear(usuario);
    }

    public async Task<UsuarioAdministracionDto> EditarAsync(
        Guid id,
        GuardarUsuarioDto datos,
        CancellationToken ct)
    {
        ValidarDatos(datos);
        var usuario = await ObtenerRequeridoAsync(id, ct);
        if (datos.Version is null)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion,
                "validation",
                "Falta la versión del usuario.",
                new Dictionary<string, string[]> { ["version"] = ["Campo obligatorio."] });
        }
        repositorio.EsperarVersion(usuario, datos.Version.Value);
        var persona = usuario.Persona!;
        var upn = NormalizarUpn(datos.Upn);
        var documento = datos.Documento.Trim();
        await ValidarUnicidadAsync(upn, documento, id, persona.Id, ct);
        var nuevas = await ConstruirAsignacionesAsync(datos.Roles, ct);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        persona.Documento = documento;
        persona.Cuil = NormalizarOpcional(datos.Cuil);
        persona.Legajo = NormalizarOpcional(datos.Legajo);
        persona.Nombre = datos.Nombre.Trim();
        persona.Apellido = datos.Apellido.Trim();
        persona.FechaNacimiento = datos.FechaNacimiento;
        persona.Telefono = NormalizarOpcional(datos.Telefono);
        usuario.Upn = upn;
        usuario.NombreParaMostrar = $"{persona.Nombre} {persona.Apellido}";

        foreach (var actual in usuario.Roles)
        {
            actual.EliminadoEn = DateTimeOffset.UtcNow;
        }
        await repositorio.GuardarAsync(ct);
        foreach (var asignacion in nuevas)
        {
            asignacion.UsuarioId = usuario.Id;
            usuario.Roles.Add(asignacion);
        }
        await repositorio.GuardarAsync(ct);
        await transaccion.CommitAsync(ct);
        return Mapear(usuario);
    }

    public async Task<UsuarioAdministracionDto> CambiarEstadoAsync(
        Guid id,
        bool activo,
        uint version,
        CancellationToken ct)
    {
        var usuario = await ObtenerRequeridoAsync(id, ct);
        repositorio.EsperarVersion(usuario, version);
        usuario.Activo = activo;
        await repositorio.GuardarAsync(ct);
        return Mapear(usuario);
    }

    private async Task<Usuario> ObtenerRequeridoAsync(Guid id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(id, ct)
        ?? throw new ExcepcionAplicacion(
            TipoErrorAplicacion.NoEncontrado,
            "resource-not-found",
            "No se encontró el usuario solicitado.");

    private async Task ValidarUnicidadAsync(
        string upn,
        string documento,
        Guid? usuarioId,
        Guid? personaId,
        CancellationToken ct)
    {
        if (await repositorio.ExisteUpnAsync(upn, usuarioId, ct))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Conflicto,
                "identity-upn-conflict",
                "Ya existe otro usuario con esa UPN.");
        }
        if (await repositorio.ExisteDocumentoAsync(documento, personaId, ct))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Conflicto,
                "identity-document-conflict",
                "Ya existe otra persona con ese documento.");
        }
    }

    private async Task<List<UsuarioRol>> ConstruirAsignacionesAsync(
        IReadOnlyList<GuardarAsignacionRolDto> datos,
        CancellationToken ct)
    {
        var ids = datos.Select(r => r.RolId).Distinct().ToArray();
        var roles = (await repositorio.ObtenerRolesAsync(ids, ct)).ToDictionary(r => r.Id);
        if (datos.Count == 0 || roles.Count != ids.Length)
        {
            throw ErrorRoles("Seleccioná al menos un rol activo y válido.");
        }
        if (datos.Distinct().Count() != datos.Count)
        {
            throw ErrorRoles("No se puede repetir la misma asignación de rol y ámbito.");
        }

        var materiaIds = datos.Where(r => r.MateriaId.HasValue).Select(r => r.MateriaId!.Value).Distinct().ToArray();
        var materias = (await repositorio.ObtenerMateriasAsync(materiaIds, ct)).ToDictionary(m => m.Id);
        if (materias.Count != materiaIds.Length)
        {
            throw ErrorRoles("Una de las materias seleccionadas no existe o está inactiva.");
        }
        var carreraIds = datos.Where(r => r.CarreraId.HasValue).Select(r => r.CarreraId!.Value).Distinct().ToArray();
        var carreras = await repositorio.ObtenerCarrerasAsync(carreraIds, ct);
        if (carreras.Count != carreraIds.Length)
        {
            throw ErrorRoles("Una de las carreras seleccionadas no existe o está inactiva.");
        }

        var ahora = DateTimeOffset.UtcNow;
        var resultado = new List<UsuarioRol>();
        foreach (var dato in datos)
        {
            var rol = roles[dato.RolId];
            var valido = rol.Ambito switch
            {
                "global" => dato.MateriaId is null && dato.CarreraId is null,
                "carrera" => dato.MateriaId is null && dato.CarreraId is not null,
                "materia" => dato.MateriaId is not null
                    && dato.CarreraId is not null
                    && materias[dato.MateriaId.Value].CarreraId == dato.CarreraId,
                _ => false,
            };
            if (!valido)
            {
                throw ErrorRoles($"El ámbito indicado no corresponde al rol {rol.Nombre}.");
            }
            resultado.Add(new UsuarioRol
            {
                Id = Guid.NewGuid(),
                RolId = dato.RolId,
                MateriaId = dato.MateriaId,
                CarreraId = dato.CarreraId,
                OtorgadoEn = ahora,
                CreadoEn = ahora,
                Rol = rol,
            });
        }
        return resultado;
    }

    private static ExcepcionAplicacion ErrorRoles(string mensaje) => new(
        TipoErrorAplicacion.ReglaDeNegocio,
        "identity-role-scope-conflict",
        mensaje);

    private static void ValidarDatos(GuardarUsuarioDto datos)
    {
        var errores = new Dictionary<string, string[]>();
        Requerido(datos.Nombre, "nombre", errores);
        Requerido(datos.Apellido, "apellido", errores);
        Requerido(datos.Documento, "documento", errores);
        Requerido(datos.Legajo, "legajo", errores);
        Requerido(datos.Upn, "upn", errores);
        if (datos.FechaNacimiento is null) errores["fechaNacimiento"] = ["Campo obligatorio."];
        if (datos.Roles.Count == 0) errores["roles"] = ["Seleccioná al menos un rol."];
        if (errores.Count > 0)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion,
                "validation",
                "Revisá los campos obligatorios.",
                errores);
        }
    }

    private static void Requerido(
        string? valor,
        string campo,
        IDictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor)) errores[campo] = ["Campo obligatorio."];
    }

    private static string NormalizarUpn(string valor) => valor.Trim().ToLowerInvariant();
    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static UsuarioAdministracionDto Mapear(Usuario usuario)
    {
        var persona = usuario.Persona
            ?? throw new InvalidOperationException("El usuario administrativo no tiene persona vinculada.");
        var roles = usuario.Roles.Where(r => r.EliminadoEn is null).Select(r => new AsignacionRolDto(
            r.Id,
            r.RolId,
            r.Rol?.Codigo ?? string.Empty,
            r.Rol?.Nombre ?? string.Empty,
            r.Rol?.Ambito ?? string.Empty,
            r.MateriaId,
            r.CarreraId)).ToArray();
        return new UsuarioAdministracionDto(
            usuario.Id, persona.Id, persona.Nombre, persona.Apellido,
            persona.Documento, persona.Legajo, persona.Cuil, persona.FechaNacimiento,
            persona.Telefono, usuario.Upn, usuario.Activo, usuario.Version, roles);
    }
}
