namespace ArsDocendi.Shared.Identity;

/// <summary>
/// Los consumidores deciden la jerarquía: admite un rol del conjunto y excluye
/// usuarios con cualquiera de los roles superiores. Incluidos permite excepciones
/// explícitas (actor/responsable); no elude actividad ni rol vigente de sistema.
/// Null en admitidos permite cualquier rol de sistema; colección vacía ninguno.
/// </summary>
public sealed record FiltroCandidatosIdentity(
    string? Busqueda = null,
    int Limite = 50,
    IReadOnlyCollection<string>? CodigosRolesAdmitidos = null,
    IReadOnlyCollection<string>? CodigosRolesExcluidos = null,
    IReadOnlyCollection<Guid>? UsuarioIdsIncluidos = null);
