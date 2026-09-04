namespace ArsDocendi.Shared.Identity;

/// <summary>
/// Superficie de LECTURA de identity para los módulos de negocio.
/// <para>
/// Los módulos leen identity para autorizar. Escribir <c>personas</c>, <c>roles</c>,
/// <c>permisos</c> o <c>rol_permisos</c> es exclusivo de la superficie de
/// administración — corolario del invariante #4 enmendado. Esa restricción NO la
/// cubre el invariante #1, porque referenciar <c>ArsDocendi.Shared</c> es legítimo
/// para todos los módulos y no constituye una relación cross-module.
/// </para>
/// <para>
/// Por eso esta interfaz sólo expone consultas: es la barrera que hace incómodo
/// escribir identity desde un módulo, aunque el <c>DbContext</c> esté al alcance.
/// </para>
/// </summary>
public interface IConsultasIdentity
{
    /// <summary>Devuelve la persona, o <c>null</c> si no existe.</summary>
    Task<Persona?> ObtenerPersonaAsync(Guid personaId, CancellationToken ct);

    /// <summary>
    /// Indica si el usuario tiene, vigente, el rol de sistema <paramref name="codigoRol"/>
    /// sobre la materia indicada. Es el guard de ámbito del Jefe de Cátedra
    /// (BR-designaciones-009 y BR-designaciones-013).
    /// </summary>
    Task<bool> TieneRolEnMateriaAsync(Guid usuarioId, string codigoRol, Guid materiaId, CancellationToken ct);

    /// <summary>
    /// Indica si el usuario tiene, vigente, el rol de sistema <paramref name="codigoRol"/>
    /// sobre la carrera indicada. Es el guard de ámbito del Coordinador.
    /// </summary>
    Task<bool> TieneRolEnCarreraAsync(Guid usuarioId, string codigoRol, Guid carreraId, CancellationToken ct);

    /// <summary>
    /// Indica si el usuario tiene, vigente, el rol global de sistema indicado
    /// (Secretaría, Decanato, Administrativo, sys_admin).
    /// </summary>
    Task<bool> TieneRolGlobalAsync(Guid usuarioId, string codigoRol, CancellationToken ct);

    /// <summary>
    /// Códigos de los roles de SISTEMA vigentes del usuario. Los roles creados por
    /// el operador quedan deliberadamente afuera: no participan del circuito de
    /// aprobación, y devolverlos acá invitaría a que la máquina de estados los
    /// tratara como si lo hicieran.
    /// </summary>
    Task<IReadOnlyList<string>> ObtenerCodigosDeRolesDeSistemaAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>Códigos de permiso efectivos del usuario, unión de todos sus roles vigentes.</summary>
    Task<IReadOnlyList<string>> ObtenerCodigosDePermisosAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>
    /// Materias sobre las que el usuario tiene vigente el rol de sistema indicado.
    /// Es el ámbito del Jefe de Cátedra: las cátedras que tiene a cargo.
    /// </summary>
    Task<IReadOnlyList<Guid>> ObtenerMateriasDeRolAsync(
        Guid usuarioId, string codigoRol, CancellationToken ct);

    /// <summary>
    /// Carreras sobre las que el usuario tiene vigente el rol de sistema indicado.
    /// Es el ámbito del Coordinador.
    /// </summary>
    Task<IReadOnlyList<Guid>> ObtenerCarrerasDeRolAsync(
        Guid usuarioId, string codigoRol, CancellationToken ct);

    /// <summary>Carrera a la que pertenece la materia, o <c>null</c> si la materia no existe.</summary>
    Task<Guid?> ObtenerCarreraDeMateriaAsync(Guid materiaId, CancellationToken ct);

    /// <summary>Catálogo activo de materias para superficies autorizadas.</summary>
    Task<IReadOnlyList<Materia>> ListarMateriasActivasAsync(CancellationToken ct);

    /// <summary>Personas canónicas elegibles para operaciones de negocio.</summary>
    Task<IReadOnlyList<Persona>> ListarPersonasAsync(CancellationToken ct);

    /// <summary>Cuentas canónicas para resolver actores en respuestas de auditoría.</summary>
    Task<IReadOnlyList<Usuario>> ListarUsuariosAsync(CancellationToken ct);
}
