namespace ArsDocendi.Shared.Auth;

public static class Permisos
{
    public const string Claim = "permiso";
    public const string RolesVer = "roles.ver";
    public const string RolesAdministrar = "roles.administrar";
    public const string RolesGestionarMembresia = "roles.gestionar_membresia";
    public const string PeriodosAdministrar = "periodos.administrar";
    public const string SistemaEstadoVer = "sistema.estado.ver";
    public const string AuditoriaVer = "auditoria.ver";
    public const string DesignacionesVer = "designaciones.ver";
    public const string DesignacionesGestionar = "designaciones.gestionar";
    public const string DesignacionesAprobarCoordinacion = "designaciones.aprobar_coordinacion";
    public const string DesignacionesAprobarSecretaria = "designaciones.aprobar_secretaria";
    public const string DesignacionesAprobarDecanato = "designaciones.aprobar_decanato";
    public const string DesignacionesRevisar = "designaciones.revisar";
    public const string TareasVer = "tareas.ver";
    public const string TareasGestionar = "tareas.gestionar";
    public const string ProyectosGestionar = "proyectos.gestionar";
    public const string PersonasVer = "personas.ver";
    public const string PersonasVerAmbito = "personas.ver_ambito";
    public const string PersonasAdministrar = "personas.administrar";
    public const string PortalAdministrar = "portal.administrar";

    public static readonly string[] Todos =
    [
        PersonasVer,
        PersonasVerAmbito,
        PersonasAdministrar,
        PortalAdministrar,
        RolesVer,
        RolesAdministrar,
        RolesGestionarMembresia,
        PeriodosAdministrar,
        SistemaEstadoVer,
        AuditoriaVer,
        DesignacionesVer,
        DesignacionesGestionar,
        DesignacionesAprobarCoordinacion,
        DesignacionesAprobarSecretaria,
        DesignacionesAprobarDecanato,
        DesignacionesRevisar,
        TareasVer,
        TareasGestionar,
        ProyectosGestionar,
    ];
}
