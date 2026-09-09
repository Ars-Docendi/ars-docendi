using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Persistencia;
using Modules.Designaciones.Contracts.Administracion;

namespace ArsDocendi.Host.Administracion;

public sealed class ServicioDocentes(
    IRepositorioDocentes repositorio,
    IAdministracionDesignaciones designaciones,
    IUnidadDeTrabajoAdministracion unidadDeTrabajo)
{
    private static readonly HashSet<string> RolesPermitidos = ["docente", "jefe_catedra"];

    public async Task<IReadOnlyList<DocenteAdministracionDto>> ListarAsync(
        string? busqueda,
        Guid? materiaId,
        string? rol,
        bool? activo,
        CancellationToken ct) =>
        await ListarAsync(busqueda, materiaId, rol, activo, null, ct);

    public async Task<IReadOnlyList<DocenteAdministracionDto>> ListarAsync(
        string? busqueda,
        Guid? materiaId,
        string? rol,
        bool? activo,
        IReadOnlySet<Guid>? materiasVisibles,
        CancellationToken ct)
    {
        var personas = await repositorio.ListarPersonasAsync(ct);
        var vigentes = await designaciones.ListarVigentesAsync(ct);
        var materias = (await repositorio.ListarMateriasAsync(ct)).ToDictionary(m => m.Id);
        var porPersona = vigentes.GroupBy(d => d.PersonaId).ToDictionary(g => g.Key, g => g.ToArray());

        var resultado = personas
            .Where(p => porPersona.ContainsKey(p.Id) || RolesDocentes(p.Usuario).Count > 0)
            .Select(p => Mapear(p, porPersona.GetValueOrDefault(p.Id) ?? [], materias))
            .Where(d => materiasVisibles is null
                || d.Asignaciones.Any(a => materiasVisibles.Contains(a.MateriaId)))
            .Where(d => materiaId is null || d.Asignaciones.Any(a => a.MateriaId == materiaId))
            .Where(d => rol is null || d.Roles.Any(r =>
                string.Equals(r.Codigo, rol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Nombre, rol, StringComparison.OrdinalIgnoreCase)))
            .Where(d => activo is null || d.Activo == activo)
            .Where(d => CoincideBusqueda(d, busqueda))
            .OrderBy(d => d.Apellido)
            .ThenBy(d => d.Nombre)
            .ToArray();
        return resultado;
    }

    public async Task<DocenteAdministracionDto> ObtenerAsync(Guid personaId, CancellationToken ct)
        => await ObtenerAsync(personaId, null, ct);

    public async Task<DocenteAdministracionDto> ObtenerAsync(
        Guid personaId,
        IReadOnlySet<Guid>? materiasVisibles,
        CancellationToken ct)
    {
        var persona = await repositorio.ObtenerPersonaAsync(personaId, false, ct)
            ?? throw NoEncontrado();
        var materias = (await repositorio.ListarMateriasAsync(ct)).ToDictionary(m => m.Id);
        var vigentes = (await designaciones.ListarVigentesAsync(ct))
            .Where(d => d.PersonaId == personaId)
            .ToArray();
        if (vigentes.Length == 0 && RolesDocentes(persona.Usuario).Count == 0) throw NoEncontrado();
        if (materiasVisibles is not null
            && !vigentes.Any(d => materiasVisibles.Contains(d.MateriaId))) throw NoEncontrado();
        return Mapear(persona, vigentes, materias);
    }

    public async Task<CatalogosDocentesDto> ObtenerCatalogosAsync(CancellationToken ct)
        => await ObtenerCatalogosAsync(null, ct);

    public async Task<CatalogosDocentesDto> ObtenerCatalogosAsync(
        IReadOnlySet<Guid>? materiasVisibles,
        CancellationToken ct)
    {
        var personas = await repositorio.ListarPersonasAsync(ct);
        var docentes = (await designaciones.ListarVigentesAsync(ct)).Select(d => d.PersonaId).ToHashSet();
        var materias = await repositorio.ListarMateriasAsync(ct);
        var roles = await repositorio.ListarRolesDocentesAsync(ct);
        var cargos = await designaciones.ListarCargosAsync(ct);
        var elegibles = personas
            .Where(p => materiasVisibles is null
                && !docentes.Contains(p.Id)
                && RolesDocentes(p.Usuario).Count == 0)
            .Select(p => new PersonaElegibleDto(
                p.Id, p.Nombre, p.Apellido, p.Documento, p.Legajo, p.Cuil,
                p.FechaNacimiento, p.Telefono, p.Usuario?.Upn, p.Usuario?.Version))
            .ToArray();
        return new CatalogosDocentesDto(
            roles.OrderBy(r => r.Nombre)
                .Select(r => new RolCatalogoDto(r.Id, r.Codigo, r.Nombre, r.Ambito, r.EsSistema)).ToArray(),
            materias.Where(m => materiasVisibles is null || materiasVisibles.Contains(m.Id))
                .Select(m => new OpcionCatalogoDto(m.Id, m.Codigo, m.Nombre, m.CarreraId)).ToArray(),
            cargos.Where(c => c.Activo).ToArray(),
            elegibles,
            (await designaciones.ListarDedicacionesAsync(ct)).Where(d => d.Activo).ToArray());
    }

    public async Task<DocenteAdministracionDto> GuardarAsync(
        Guid? personaIdRuta,
        GuardarDocenteDto datos,
        CancellationToken ct)
    {
        ValidarDatos(datos);
        if (personaIdRuta is not null && datos.PersonaId is not null && personaIdRuta != datos.PersonaId)
        {
            throw ErrorValidacion("personaId", "El identificador no coincide con la ruta.");
        }
        var personaId = personaIdRuta ?? datos.PersonaId;
        var existente = personaId is null
            ? null
            : await repositorio.ObtenerPersonaAsync(personaId.Value, true, ct)
                ?? throw NoEncontrado();
        if (existente?.Usuario is not null && datos.Version is null)
        {
            throw ErrorValidacion("version", "Campo obligatorio.");
        }

        var upn = datos.Upn.Trim().ToLowerInvariant();
        var documento = datos.Documento.Trim();
        await ValidarReferenciasAsync(datos, existente, upn, documento, ct);
        var roles = (await repositorio.ObtenerRolesDocentesAsync(
            datos.Membresias.Select(m => m.RolId).Distinct().ToArray(), ct))
            .ToDictionary(r => r.Id);
        var materias = (await repositorio.ObtenerMateriasAsync(
            datos.Membresias.Select(m => m.MateriaId)
                .Concat(datos.Designaciones.Select(d => (Guid?)d.MateriaId))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct().ToArray(), ct))
            .ToDictionary(m => m.Id);

        var id = await unidadDeTrabajo.EjecutarAsync(async token =>
        {
            var persona = existente ?? NuevaPersona(datos, documento);
            ActualizarPersona(persona, datos, documento);
            var usuario = persona.Usuario;
            if (usuario is null)
            {
                usuario = NuevoUsuario(persona, upn);
                persona.Usuario = usuario;
                repositorio.Agregar(existente is null ? persona : null, usuario);
            }
            else
            {
                repositorio.EsperarVersion(usuario, datos.Version!.Value);
                usuario.Upn = upn;
                usuario.NombreParaMostrar = $"{persona.Nombre} {persona.Apellido}";
            }

            foreach (var asignacion in usuario.Roles.Where(r =>
                r.Rol is not null && RolesPermitidos.Contains(r.Rol.Codigo)))
            {
                asignacion.EliminadoEn = DateTimeOffset.UtcNow;
            }
            await repositorio.GuardarAsync(token);

            var ahora = DateTimeOffset.UtcNow;
            foreach (var membresia in datos.Membresias)
            {
                var rol = roles[membresia.RolId];
                var materia = materias[membresia.MateriaId!.Value];
                var asignacion = new UsuarioRol
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = usuario.Id,
                    RolId = rol.Id,
                    MateriaId = materia.Id,
                    CarreraId = materia.CarreraId,
                    OtorgadoEn = ahora,
                    CreadoEn = ahora,
                    Rol = rol,
                };
                usuario.Roles.Add(asignacion);
                repositorio.AgregarAsignacion(asignacion);
            }
            await repositorio.GuardarAsync(token);
            await designaciones.ReemplazarVigentesAsync(persona.Id, datos.Designaciones, token);
            return persona.Id;
        }, ct);

        return await ObtenerAsync(id, ct);
    }

    public async Task<DocenteAdministracionDto> CambiarEstadoAsync(
        Guid personaId,
        bool activo,
        uint version,
        CancellationToken ct)
    {
        var persona = await repositorio.ObtenerPersonaAsync(personaId, true, ct)
            ?? throw NoEncontrado();
        var usuario = persona.Usuario ?? throw new ExcepcionAplicacion(
            TipoErrorAplicacion.ReglaDeNegocio,
            "identity-account-required",
            "La persona no tiene una cuenta para cambiar de estado.");
        repositorio.EsperarVersion(usuario, version);
        usuario.Activo = activo;
        await repositorio.GuardarAsync(ct);
        return await ObtenerAsync(personaId, ct);
    }

    private async Task ValidarReferenciasAsync(
        GuardarDocenteDto datos,
        Persona? existente,
        string upn,
        string documento,
        CancellationToken ct)
    {
        await designaciones.ValidarReemplazoAsync(existente?.Id, datos.Designaciones, ct);
        if (await repositorio.ExisteUpnAsync(upn, existente?.Usuario?.Id, ct))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Conflicto, "identity-upn-conflict", "Ya existe otro usuario con esa UPN.");
        }
        if (await repositorio.ExisteDocumentoAsync(documento, existente?.Id, ct))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Conflicto, "identity-document-conflict", "Ya existe otra persona con ese documento.");
        }
        var idsRoles = datos.Membresias.Select(m => m.RolId).ToArray();
        var roles = await repositorio.ObtenerRolesDocentesAsync(idsRoles.Distinct().ToArray(), ct);
        if (datos.Membresias.Distinct().Count() != datos.Membresias.Count
            || roles.Count != idsRoles.Distinct().Count())
        {
            throw ErrorValidacion("membresias", "Sólo se admiten membresías docentes válidas y sin duplicados.");
        }
        var rolesPorId = roles.ToDictionary(r => r.Id);
        var materias = (await repositorio.ObtenerMateriasAsync(
            datos.Membresias.Select(m => m.MateriaId).Where(id => id.HasValue).Select(id => id!.Value)
                .Distinct().ToArray(), ct)).ToDictionary(m => m.Id);
        if (datos.Membresias.Any(m => m.MateriaId is null
                || m.CarreraId is null
                || !rolesPorId.TryGetValue(m.RolId, out var rol)
                || rol.Ambito != "materia"
                || !materias.TryGetValue(m.MateriaId.Value, out var materia)
                || materia.CarreraId != m.CarreraId))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.ReglaDeNegocio,
                "identity-role-scope-conflict",
                "Cada membresía docente debe vincular un rol con una materia de su carrera.");
        }
        var materiaIds = datos.Designaciones.Select(d => d.MateriaId).Distinct().ToArray();
        if ((await repositorio.ObtenerMateriasAsync(materiaIds, ct)).Count != materiaIds.Length)
        {
            throw ErrorValidacion("designaciones", "Una de las materias no existe o está inactiva.");
        }
    }

    private static Persona NuevaPersona(GuardarDocenteDto datos, string documento) => new()
    {
        Id = Guid.NewGuid(),
        Documento = documento,
        Nombre = datos.Nombre.Trim(),
        Apellido = datos.Apellido.Trim(),
        CreadoEn = DateTimeOffset.UtcNow,
    };

    private static Usuario NuevoUsuario(Persona persona, string upn) => new()
    {
        Id = Guid.NewGuid(),
        AzureOid = Guid.NewGuid(),
        Upn = upn,
        NombreParaMostrar = $"{persona.Nombre} {persona.Apellido}",
        Activo = true,
        PersonaId = persona.Id,
        Persona = persona,
        CreadoEn = DateTimeOffset.UtcNow,
    };

    private static void ActualizarPersona(Persona persona, GuardarDocenteDto datos, string documento)
    {
        persona.Nombre = datos.Nombre.Trim();
        persona.Apellido = datos.Apellido.Trim();
        persona.Documento = documento;
        persona.Legajo = NormalizarOpcional(datos.Legajo);
        persona.Cuil = NormalizarOpcional(datos.Cuil);
        persona.FechaNacimiento = datos.FechaNacimiento;
        persona.Telefono = NormalizarOpcional(datos.Telefono);
    }

    private static void ValidarDatos(GuardarDocenteDto datos)
    {
        var errores = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(datos.Nombre)) errores["nombre"] = ["Campo obligatorio."];
        if (string.IsNullOrWhiteSpace(datos.Apellido)) errores["apellido"] = ["Campo obligatorio."];
        if (string.IsNullOrWhiteSpace(datos.Documento)) errores["documento"] = ["Campo obligatorio."];
        if (string.IsNullOrWhiteSpace(datos.Upn)) errores["upn"] = ["Campo obligatorio."];
        if (datos.Membresias.Count == 0) errores["membresias"] = ["Seleccioná al menos una membresía docente."];
        if (datos.Designaciones.Count == 0) errores["designaciones"] = ["Agregá al menos una designación."];
        if (errores.Count > 0)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion, "validation", "Revisá los datos del docente.", errores);
        }
    }

    private static bool CoincideBusqueda(DocenteAdministracionDto docente, string? busqueda)
    {
        if (string.IsNullOrWhiteSpace(busqueda)) return true;
        var texto = busqueda.Trim();
        return docente.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
            || docente.Apellido.Contains(texto, StringComparison.OrdinalIgnoreCase)
            || docente.Documento.Contains(texto, StringComparison.OrdinalIgnoreCase)
            || (docente.Upn?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static IReadOnlyList<RolResumenDto> RolesDocentes(Usuario? usuario) =>
        usuario?.Roles
            .Where(r => r.Rol is not null && RolesPermitidos.Contains(r.Rol.Codigo))
            .GroupBy(r => r.RolId)
            .Select(g => new RolResumenDto(g.Key, g.First().Rol!.Codigo, g.First().Rol!.Nombre))
            .OrderBy(r => r.Nombre)
            .ToArray() ?? [];

    private static IReadOnlyList<AsignacionRolDto> MembresiasDocentes(Usuario? usuario) =>
        usuario?.Roles
            .Where(r => r.Rol is not null && RolesPermitidos.Contains(r.Rol.Codigo))
            .Select(r => new AsignacionRolDto(
                r.Id,
                r.RolId,
                r.Rol!.Codigo,
                r.Rol.Nombre,
                r.Rol.Ambito,
                r.MateriaId,
                r.CarreraId))
            .ToArray() ?? [];

    private static DocenteAdministracionDto Mapear(
        Persona persona,
        IReadOnlyList<DesignacionVigenteDto> designaciones,
        IReadOnlyDictionary<Guid, Materia> materias) => new(
            persona.Id,
            persona.Usuario?.Id,
            persona.Nombre,
            persona.Apellido,
            persona.Documento,
            persona.Legajo,
            persona.Cuil,
            persona.FechaNacimiento,
            persona.Telefono,
            persona.Usuario?.Upn,
            persona.Usuario is not null,
            persona.Usuario?.Activo ?? false,
            persona.Usuario?.Version,
            RolesDocentes(persona.Usuario),
            MembresiasDocentes(persona.Usuario),
            designaciones.Select(d => new AsignacionDocenteDto(
                d.Id,
                d.MateriaId,
                materias.GetValueOrDefault(d.MateriaId)?.Codigo ?? string.Empty,
                materias.GetValueOrDefault(d.MateriaId)?.Nombre ?? string.Empty,
                d.CargoId,
                d.CargoNombre,
                d.CargoAbreviatura,
                d.Dedicacion,
                d.Horas,
                d.DedicacionId, d.HorasInvestigacion, d.HorasExternas)).ToArray());

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static ExcepcionAplicacion ErrorValidacion(string campo, string mensaje) => new(
        TipoErrorAplicacion.Validacion,
        "validation",
        "Revisá los datos del docente.",
        new Dictionary<string, string[]> { [campo] = [mensaje] });

    private static ExcepcionAplicacion NoEncontrado() => new(
        TipoErrorAplicacion.NoEncontrado,
        "resource-not-found",
        "No se encontró el docente solicitado.");
}
