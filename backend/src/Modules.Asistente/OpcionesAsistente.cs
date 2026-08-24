namespace Modules.Asistente;

/// <summary>
/// Configuración del módulo del asistente, por ambiente.
/// </summary>
public sealed class OpcionesAsistente
{
    /// <summary>Sección de configuración: <c>Asistente</c>.</summary>
    public const string Seccion = "Asistente";

    /// <summary>
    /// Rol de PostgreSQL con el que el asistente lee sin datos personales.
    /// Lleva sufijo de ambiente (<c>asistente_ro_prod</c>, <c>asistente_ro_pr_123</c>);
    /// lo crea <c>infra/scripts/provision-db.sh</c>.
    /// </summary>
    public string RolSoloLectura { get; set; } = string.Empty;

    /// <summary>
    /// Rol de PostgreSQL con el que el asistente lee incluyendo datos personales.
    /// </summary>
    public string RolSoloLecturaPii { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña del rol de solo lectura. La inyecta el ambiente en runtime; nunca
    /// se loguea ni se escribe al repositorio.
    /// </summary>
    public string PasswordSoloLectura { get; set; } = string.Empty;

    /// <summary>Contraseña del rol de solo lectura con datos personales.</summary>
    public string PasswordSoloLecturaPii { get; set; } = string.Empty;
}
