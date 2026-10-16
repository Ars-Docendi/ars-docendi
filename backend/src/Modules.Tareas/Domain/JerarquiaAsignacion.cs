namespace Modules.Tareas.Domain;

/// <summary>
/// Actor que ejecuta una operación: usuario, roles de sistema de la sesión, el nombre del
/// rol de mayor jerarquía (para registrar en historial y comentarios) y si ve todas las
/// tareas (`tareas.gestionar`) o solo las que tiene asignadas.
/// </summary>
public sealed record ActorTareas(Guid UsuarioId, IReadOnlySet<string> Roles, string RolNombre, bool VeTodas)
{
    /// <summary>Nivel de mayor autoridad entre sus roles (0 = Administrador de Sistemas); <c>null</c> si ninguno está en la escala.</summary>
    public int? Nivel => JerarquiaAsignacion.MejorNivel(Roles);

    /// <summary>Id por el que se restringen las consultas cuando el actor no ve todas las tareas.</summary>
    public Guid? SoloResponsableId => VeTodas ? null : UsuarioId;
}

/// <summary>
/// Escala de autoridad para asignar Responsables: Administrador de Sistemas &gt; Decanato &gt; Secretaría Académica &gt;
/// Administrativo &gt; Coordinador de Carrera &gt; Jefe de Cátedra &gt; Docente. Quien asigna
/// solo puede elegir a alguien de su mismo nivel o inferior, nunca superior. Un rol fuera
/// de la escala no asigna ni es asignable. El Administrador de Sistemas es la máxima
/// jerarquía: puede asignar a cualquiera y nadie puede asignarle a él.
/// </summary>
public static class JerarquiaAsignacion
{
    public const string Administrador = "sys_admin";

    private static readonly string[] Orden =
    [
        Administrador,
        "decanato",
        "secretaria",
        "administrativo",
        "coordinador_carrera",
        "jefe_catedra",
        "docente",
    ];

    public static int? Nivel(string codigoRol)
    {
        var indice = Array.IndexOf(Orden, codigoRol);
        return indice < 0 ? null : indice;
    }

    /// <summary>Mejor (menor) nivel entre los roles dados, o <c>null</c> si ninguno está en la escala.</summary>
    public static int? MejorNivel(IEnumerable<string> roles) =>
        roles.Select(Nivel).Where(n => n.HasValue).Select(n => n!.Value).Cast<int?>().Min();

    /// <summary>Código del rol de mayor jerarquía, o el primero disponible si ninguno está en la escala.</summary>
    public static string? RolPrincipal(IEnumerable<string> roles) =>
        roles.OrderBy(r => Nivel(r) ?? int.MaxValue).FirstOrDefault();

    public static bool PuedeAsignar(int? nivelActor, IEnumerable<string> rolesCandidato)
    {
        var nivelCandidato = MejorNivel(rolesCandidato);
        return nivelActor is not null && nivelCandidato is not null && nivelCandidato >= nivelActor;
    }

    /// <summary>El Responsable de un Proyecto es siempre Decanato o Secretaría Académica.</summary>
    public static bool EsResponsableDeProyecto(IEnumerable<string> rolesCandidato) =>
        rolesCandidato.Any(r => r is "decanato" or "secretaria");
}
