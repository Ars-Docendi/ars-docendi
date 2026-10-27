using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Persistencia;
using Modules.Designaciones.Contracts.Administracion;

namespace ArsDocendi.Host.Administracion;

/// <summary>
/// Administración unificada de personas: identidad, cuenta, roles de cualquier ámbito y
/// designaciones vigentes, guardados en una única transacción por persona.
/// <para>
/// <c>materiasVisibles</c> acota la consulta al ámbito de un Jefe de Cátedra: sólo personas
/// con designación vigente en esas materias, y de ellas sólo lo que corresponde a esas materias.
/// <c>null</c> es sin restricción.
/// </para>
/// </summary>
public sealed class ServicioAdministracionPersonas(
    IRepositorioDocentes repositorio,
    IAdministracionDesignaciones designaciones,
    IUnidadDeTrabajoAdministracion unidadDeTrabajo)
{
    private static readonly HashSet<string> RolesConAsignaciones = ["docente", "jefe_catedra"];

    public async Task<IReadOnlyList<PersonaAdministracionDto>> ListarAsync(
        IReadOnlySet<Guid>? materiasVisibles,
        CancellationToken ct)
    {
        var personas = await repositorio.ListarPersonasAsync(ct);
        var materias = (await repositorio.ListarMateriasAsync(ct)).ToDictionary(m => m.Id);
        var porPersona = (await designaciones.ListarVigentesAsync(ct))
            .Where(d => materiasVisibles is null || materiasVisibles.Contains(d.MateriaId))
            .GroupBy(d => d.PersonaId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        return personas
            .Where(p => materiasVisibles is null || porPersona.ContainsKey(p.Id))
            .Select(p => Mapear(p, porPersona.GetValueOrDefault(p.Id) ?? [], materias, materiasVisibles))
            .OrderBy(p => p.Apellido)
            .ThenBy(p => p.Nombre)
            .ToArray();
    }

    public async Task<PersonaAdministracionDto> ObtenerAsync(
        Guid personaId,
        IReadOnlySet<Guid>? materiasVisibles,
        CancellationToken ct)
    {
        var persona = await repositorio.ObtenerPersonaAsync(personaId, false, ct) ?? throw NoEncontrada();
        var materias = (await repositorio.ListarMateriasAsync(ct)).ToDictionary(m => m.Id);
        var vigentes = (await designaciones.ListarVigentesAsync(ct))
            .Where(d => d.PersonaId == personaId
                && (materiasVisibles is null || materiasVisibles.Contains(d.MateriaId)))
            .ToArray();
        if (materiasVisibles is not null && vigentes.Length == 0) throw NoEncontrada();
        return Mapear(persona, vigentes, materias, materiasVisibles);
    }

    /// <summary>Si la persona está dentro del ámbito; sin ámbito, siempre.</summary>
    public async Task<bool> EstaEnAmbitoAsync(Guid personaId, IReadOnlySet<Guid>? materiasVisibles, CancellationToken ct)
    {
        if (await repositorio.ObtenerPersonaAsync(personaId, false, ct) is null) return false;
        if (materiasVisibles is null) return true;
        return (await designaciones.ListarVigentesAsync(ct))
            .Any(d => d.PersonaId == personaId && materiasVisibles.Contains(d.MateriaId));
    }

    public async Task<CatalogosPersonasDto> ObtenerCatalogosAsync(CancellationToken ct)
    {
        var materias = await repositorio.ListarMateriasAsync(ct);
        var pares = await repositorio.ListarMateriasPlanAsync(ct);
        return new CatalogosPersonasDto(
            (await repositorio.ListarRolesActivosAsync(ct))
                .Select(r => new RolCatalogoDto(r.Id, r.Codigo, r.Nombre, r.Ambito, r.EsSistema)).ToArray(),
            materias.Select(m => new OpcionCatalogoDto(m.Id, m.Codigo, m.Nombre)).ToArray(),
            pares.Where(mp => mp.Plan!.Activo)
                .Select(mp => new OpcionCatalogoDto(
                    mp.MateriaId, mp.Materia!.Codigo, mp.Materia.Nombre,
                    mp.Plan!.CarreraId, mp.Plan.Carrera!.Nombre, mp.Plan.Carrera.Codigo))
                .Distinct()
                .ToArray(),
            (await repositorio.ListarCarrerasAsync(ct))
                .Select(c => new OpcionCatalogoDto(c.Id, c.Codigo, c.Nombre)).ToArray(),
            (await designaciones.ListarCargosAsync(ct)).Where(c => c.Activo).ToArray(),
            (await designaciones.ListarDedicacionesAsync(ct)).Where(d => d.Activo).ToArray());
    }

    /// <summary>El DNI se compara por sus dígitos; <paramref name="exceptoPersonaId"/> es la propia persona al editar.</summary>
    public async Task<DocumentoRegistradoDto> VerificarDocumentoAsync(
        string documento,
        Guid? exceptoPersonaId,
        CancellationToken ct)
    {
        var digitos = SoloDigitos(documento);
        if (digitos.Length == 0) return new DocumentoRegistradoDto(false);
        var persona = await repositorio.BuscarPorDocumentoAsync(digitos, ct);
        if (persona is null || persona.Id == exceptoPersonaId) return new DocumentoRegistradoDto(false);
        return new DocumentoRegistradoDto(
            true, persona.Id, $"{persona.Apellido}, {persona.Nombre}", persona.Legajo, persona.Usuario?.Activo);
    }

    public async Task<PersonaAdministracionDto> GuardarAsync(
        Guid? personaId,
        GuardarPersonaDto datos,
        CancellationToken ct)
    {
        ValidarDatos(datos);
        var existente = personaId is null
            ? null
            : await repositorio.ObtenerPersonaAsync(personaId.Value, true, ct) ?? throw NoEncontrada();
        if (existente?.Usuario is not null && datos.Version is null)
        {
            throw ErrorValidacion("version", "Campo obligatorio.");
        }

        var upn = datos.Upn.Trim().ToLowerInvariant();
        var documento = SoloDigitos(datos.Documento);
        var roles = await ValidarReferenciasAsync(datos, existente, upn, documento, ct);
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

            // La lista de roles se reemplaza completa: es lo que el operador ve en el editor.
            foreach (var asignacion in usuario.Roles.Where(r => r.EliminadoEn is null))
            {
                asignacion.EliminadoEn = DateTimeOffset.UtcNow;
            }
            await repositorio.GuardarAsync(token);

            var ahora = DateTimeOffset.UtcNow;
            foreach (var membresia in datos.Membresias)
            {
                var rol = roles[membresia.RolId];
                var asignacion = new UsuarioRol
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = usuario.Id,
                    RolId = rol.Id,
                    MateriaId = membresia.MateriaId,
                    CarreraId = membresia.CarreraId,
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

        return await ObtenerAsync(id, null, ct);
    }

    public async Task<PersonaAdministracionDto> CambiarEstadoAsync(
        Guid personaId,
        bool activo,
        uint version,
        CancellationToken ct)
    {
        var persona = await repositorio.ObtenerPersonaAsync(personaId, true, ct) ?? throw NoEncontrada();
        var usuario = persona.Usuario ?? throw new ExcepcionAplicacion(
            TipoErrorAplicacion.ReglaDeNegocio,
            "identity-account-required",
            "La persona no tiene una cuenta para cambiar de estado.");
        repositorio.EsperarVersion(usuario, version);
        usuario.Activo = activo;
        await repositorio.GuardarAsync(ct);
        return await ObtenerAsync(personaId, null, ct);
    }

    private async Task<Dictionary<Guid, Rol>> ValidarReferenciasAsync(
        GuardarPersonaDto datos,
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

        var ids = datos.Membresias.Select(m => m.RolId).Distinct().ToArray();
        var roles = (await repositorio.ObtenerRolesActivosAsync(ids, ct)).ToDictionary(r => r.Id);
        if (roles.Count != ids.Length)
        {
            throw ErrorAmbito("Uno de los roles no existe o está inactivo.");
        }
        if (datos.Membresias.Distinct().Count() != datos.Membresias.Count)
        {
            throw ErrorAmbito("No se puede repetir la misma asignación de rol y ámbito.");
        }

        var materias = (await repositorio.ObtenerMateriasAsync(
            datos.Membresias.Where(m => m.MateriaId is not null).Select(m => m.MateriaId!.Value)
                .Concat(datos.Designaciones.Select(d => d.MateriaId))
                .Distinct().ToArray(), ct)).ToDictionary(m => m.Id);
        var carreras = (await repositorio.ObtenerCarrerasAsync(
            datos.Membresias.Where(m => m.CarreraId is not null).Select(m => m.CarreraId!.Value)
                .Distinct().ToArray(), ct)).Select(c => c.Id).ToHashSet();
        // Catálogo informativo materia–carrera: valida que la materia se dicte, vigente, en la
        // carrera elegida. Ninguna FK depende de esto.
        var paresActivos = (await repositorio.ListarMateriasPlanAsync(ct))
            .Where(mp => mp.Activo && mp.Materia!.Activo && mp.Plan!.Activo)
            .Select(mp => (mp.MateriaId, CarreraId: mp.Plan!.CarreraId))
            .ToHashSet();
        foreach (var membresia in datos.Membresias)
        {
            var rol = roles[membresia.RolId];
            if (!AmbitoValido(rol, membresia, materias, carreras, paresActivos))
            {
                throw ErrorAmbito($"El ámbito indicado no corresponde al rol {rol.Nombre}.");
            }
        }

        var requiereAsignaciones = datos.Membresias.Any(m => RolesConAsignaciones.Contains(roles[m.RolId].Codigo));
        if (requiereAsignaciones && datos.Designaciones.Count == 0)
        {
            throw ErrorValidacion(
                "designaciones", "Con rol Docente o Jefe de Cátedra hace falta al menos una asignación.");
        }
        if (datos.Designaciones.Any(d =>
            !materias.ContainsKey(d.MateriaId) || !paresActivos.Contains((d.MateriaId, d.CarreraId))))
        {
            throw ErrorValidacion(
                "designaciones", "Una de las materias no existe, está inactiva o no se dicta en la carrera indicada.");
        }
        return roles;
    }

    /// <summary>
    /// Docente: materia y carrera que de verdad se dicta. Jefe de Cátedra: sólo la materia
    /// canónica. Coordinador y roles de ámbito carrera: sólo la carrera. Roles globales: nada.
    /// </summary>
    private static bool AmbitoValido(
        Rol rol,
        GuardarAsignacionRolDto m,
        IReadOnlyDictionary<Guid, Materia> materias,
        IReadOnlySet<Guid> carreras,
        IReadOnlySet<(Guid MateriaId, Guid CarreraId)> paresActivos) => rol.Codigo switch
        {
            "docente" => m.MateriaId is { } materiaId && m.CarreraId is { } carreraId
                && paresActivos.Contains((materiaId, carreraId)),
            "jefe_catedra" => m.MateriaId is { } mid && m.CarreraId is null && materias.ContainsKey(mid),
            _ => rol.Ambito switch
            {
                "global" => m.MateriaId is null && m.CarreraId is null,
                "materia" => m.MateriaId is { } id && m.CarreraId is null && materias.ContainsKey(id),
                "carrera" => m.CarreraId is { } cid && m.MateriaId is null && carreras.Contains(cid),
                _ => false,
            },
        };

    private static Persona NuevaPersona(GuardarPersonaDto datos, string documento) => new()
    {
        Id = Guid.NewGuid(),
        Documento = documento,
        Nombre = datos.Nombre.Trim(),
        Apellido = datos.Apellido.Trim(),
        CreadoEn = DateTimeOffset.UtcNow,
    };

    // Sin cuenta Microsoft: se vincula en el primer ingreso, encontrándola por UPN.
    private static Usuario NuevoUsuario(Persona persona, string upn) => new()
    {
        Id = Guid.NewGuid(),
        Upn = upn,
        NombreParaMostrar = $"{persona.Nombre} {persona.Apellido}",
        Activo = true,
        PersonaId = persona.Id,
        Persona = persona,
        CreadoEn = DateTimeOffset.UtcNow,
    };

    private static void ActualizarPersona(Persona persona, GuardarPersonaDto datos, string documento)
    {
        persona.Nombre = datos.Nombre.Trim();
        persona.Apellido = datos.Apellido.Trim();
        persona.Documento = documento;
        persona.Legajo = NormalizarOpcional(datos.Legajo);
        persona.Cuil = NormalizarOpcional(datos.Cuil);
        persona.FechaNacimiento = datos.FechaNacimiento;
        persona.Telefono = NormalizarOpcional(datos.Telefono);
    }

    private static void ValidarDatos(GuardarPersonaDto datos)
    {
        var errores = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(datos.Nombre)) errores["nombre"] = ["Campo obligatorio."];
        if (string.IsNullOrWhiteSpace(datos.Apellido)) errores["apellido"] = ["Campo obligatorio."];
        if (SoloDigitos(datos.Documento).Length == 0) errores["documento"] = ["Campo obligatorio."];
        if (string.IsNullOrWhiteSpace(datos.Legajo)) errores["legajo"] = ["Campo obligatorio."];
        if (datos.FechaNacimiento is null) errores["fechaNacimiento"] = ["Campo obligatorio."];
        if (string.IsNullOrWhiteSpace(datos.Upn)) errores["upn"] = ["Campo obligatorio."];
        if (datos.Membresias.Count == 0) errores["membresias"] = ["Toda persona necesita al menos un rol con su ámbito."];
        if (errores.Count > 0)
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Validacion, "validation", "Revisá los datos de la persona.", errores);
        }
    }

    private static IReadOnlyList<RolResumenDto> Roles(IEnumerable<UsuarioRol> asignaciones) =>
        asignaciones
            .GroupBy(r => r.RolId)
            .Select(g => new RolResumenDto(g.Key, g.First().Rol!.Codigo, g.First().Rol!.Nombre))
            .OrderBy(r => r.Nombre)
            .ToArray();

    private static PersonaAdministracionDto Mapear(
        Persona persona,
        IReadOnlyList<DesignacionVigenteDto> designaciones,
        IReadOnlyDictionary<Guid, Materia> materias,
        IReadOnlySet<Guid>? materiasVisibles)
    {
        // En el ámbito de un Jefe de Cátedra sólo se ve lo de sus materias.
        var membresias = (persona.Usuario?.Roles ?? [])
            .Where(r => r.Rol is not null && r.EliminadoEn is null
                && (materiasVisibles is null || r.MateriaId is Guid materiaId && materiasVisibles.Contains(materiaId)))
            .ToArray();
        return new(
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
            Roles(membresias),
            membresias.Select(r => new AsignacionRolDto(
                r.Id, r.RolId, r.Rol!.Codigo, r.Rol.Nombre, r.Rol.Ambito, r.MateriaId, r.CarreraId)).ToArray(),
            designaciones.Select(d => new AsignacionDocenteDto(
                d.Id,
                d.MateriaId,
                materias.GetValueOrDefault(d.MateriaId)?.Codigo ?? string.Empty,
                materias.GetValueOrDefault(d.MateriaId)?.Nombre ?? string.Empty,
                d.CarreraId,
                d.CargoId,
                d.CargoNombre,
                d.CargoAbreviatura,
                d.Dedicacion,
                d.Horas,
                d.DedicacionId, d.HorasInvestigacion, d.HorasExternas)).ToArray());
    }

    private static string SoloDigitos(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static ExcepcionAplicacion ErrorValidacion(string campo, string mensaje) => new(
        TipoErrorAplicacion.Validacion,
        "validation",
        "Revisá los datos de la persona.",
        new Dictionary<string, string[]> { [campo] = [mensaje] });

    private static ExcepcionAplicacion ErrorAmbito(string mensaje) => new(
        TipoErrorAplicacion.ReglaDeNegocio, "identity-role-scope-conflict", mensaje);

    private static ExcepcionAplicacion NoEncontrada() => new(
        TipoErrorAplicacion.NoEncontrado,
        "resource-not-found",
        "No se encontró la persona solicitada.");
}
