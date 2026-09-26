namespace ArsDocendi.Shared.Auth;

public static class Permisos
{
    public const string Claim = "permiso";
    public const string UsuariosVer = "usuarios.ver";
    public const string UsuariosAdministrar = "usuarios.administrar";
    public const string RolesVer = "roles.ver";
    public const string RolesAdministrar = "roles.administrar";
    public const string RolesGestionarMembresia = "roles.gestionar_membresia";
    public const string PeriodosAdministrar = "periodos.administrar";
    public const string DesignacionesVer = "designaciones.ver";
    public const string DesignacionesGestionar = "designaciones.gestionar";
    public const string DesignacionesAprobarCoordinacion = "designaciones.aprobar_coordinacion";
    public const string DesignacionesAprobarSecretaria = "designaciones.aprobar_secretaria";
    public const string DesignacionesAprobarDecanato = "designaciones.aprobar_decanato";
    public const string DesignacionesRevisar = "designaciones.revisar";
    public const string DocentesVer = "docentes.ver";

    /// <summary>
    /// Admisión al asistente conversacional. Se administra desde la membresía de
    /// roles, sin desplegar.
    /// </summary>
    public const string AsistenteConsultar = "asistente.consultar";

    /// <summary>
    /// Ver la consulta que el asistente generó para responder.
    /// </summary>
    /// <remarks>
    /// Superficie de diagnóstico, no de uso: el <c>WHERE</c> de una consulta
    /// generada puede llevar un documento. Se siembra <b>sin concedérselo a ningún
    /// rol</b> y se otorga desde la administración de membresías, sin desplegar.
    /// </remarks>
    public const string AsistenteVerConsulta = "asistente.ver_consulta";

    /// <summary>
    /// Leer el historial de conversaciones de OTRO usuario con el asistente,
    /// para soporte.
    /// </summary>
    /// <remarks>
    /// No es lo mismo que <see cref="AsistenteConsultar"/> (admite al asistente)
    /// ni que <see cref="AsistenteVerConsulta"/> (muestra la SQL de la propia
    /// consulta): éste habilita leer preguntas, SQL, resultado y momentos del
    /// historial de OTRO usuario, con razón obligatoria y auditoría permanente.
    /// Se siembra <b>sin concedérselo a ningún rol, ni siquiera <c>sys_admin</c></b>
    /// y se otorga desde la administración de membresías, sin desplegar.
    /// </remarks>
    public const string AsistenteLeerHistorialAjeno = "asistente.leer_historial_ajeno";

    /// <summary>
    /// Administrar el uso del asistente: presupuestos, tope organizacional,
    /// modo mantenimiento y el panel de uso.
    /// </summary>
    /// <remarks>
    /// A diferencia de <see cref="AsistenteVerConsulta"/> y
    /// <see cref="AsistenteLeerHistorialAjeno"/>, que se siembran sin
    /// concedérselas a ningún rol, éste se siembra directamente a
    /// <c>sys_admin</c>: gobierna controles operativos sobre disponibilidad y
    /// gasto del módulo entero, no una superficie de diagnóstico o soporte
    /// sobre datos de otra persona.
    /// </remarks>
    public const string AsistenteAdministrar = "asistente.administrar";

    public static readonly string[] Todos =
    [
        UsuariosVer,
        UsuariosAdministrar,
        RolesVer,
        RolesAdministrar,
        RolesGestionarMembresia,
        PeriodosAdministrar,
        DesignacionesVer,
        DesignacionesGestionar,
        DesignacionesAprobarCoordinacion,
        DesignacionesAprobarSecretaria,
        DesignacionesAprobarDecanato,
        DesignacionesRevisar,
        DocentesVer,
        AsistenteConsultar,
        AsistenteVerConsulta,
        AsistenteLeerHistorialAjeno,
        AsistenteAdministrar,
    ];
}
