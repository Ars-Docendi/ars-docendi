using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ArsDocendi.Shared.Persistencia;

/// <summary>
/// Cadena de conexión del rol DUEÑO de la base: el que crea el schema, migra y
/// escribe. Es la única con la que el sistema muta datos.
/// </summary>
public sealed class CadenaDuena
{
    /// <summary>Clave de configuración de la cadena del dueño.</summary>
    public const string Clave = "ArsDocendi";

    public CadenaDuena(string valor) => Valor = CadenasDeConexion.Validar(valor, nameof(CadenaDuena));

    /// <summary>La cadena, para entregársela al driver. Contiene la contraseña.</summary>
    public string Valor { get; }

    /// <summary>Usuario de base de datos al que apunta.</summary>
    public string Usuario => CadenasDeConexion.Usuario(Valor);

    /// <summary>Lee la cadena del dueño de <c>ConnectionStrings:ArsDocendi</c>.</summary>
    public static CadenaDuena Desde(IConfiguration configuracion) =>
        new(configuracion.GetConnectionString(Clave)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión '{Clave}' en la configuración del ambiente."));

    /// <inheritdoc cref="CadenasDeConexion.Redactar" />
    public override string ToString() => CadenasDeConexion.Redactar(Valor);
}

/// <summary>
/// Cadena de conexión de solo lectura del asistente, SIN acceso a las columnas
/// de datos personales. El límite lo impone el motor: el rol al que apunta no
/// tiene privilegio sobre esas columnas.
/// </summary>
public sealed class CadenaSoloLectura
{
    public CadenaSoloLectura(string valor) =>
        Valor = CadenasDeConexion.Validar(valor, nameof(CadenaSoloLectura));

    public string Valor { get; }

    public string Usuario => CadenasDeConexion.Usuario(Valor);

    public override string ToString() => CadenasDeConexion.Redactar(Valor);
}

/// <summary>
/// Cadena de conexión de solo lectura del asistente CON acceso a las columnas de
/// datos personales. Se usa únicamente cuando el actor del turno tiene el permiso
/// que la habilita.
/// </summary>
public sealed class CadenaSoloLecturaPii
{
    public CadenaSoloLecturaPii(string valor) =>
        Valor = CadenasDeConexion.Validar(valor, nameof(CadenaSoloLecturaPii));

    public string Valor { get; }

    public string Usuario => CadenasDeConexion.Usuario(Valor);

    public override string ToString() => CadenasDeConexion.Redactar(Valor);
}

/// <summary>
/// Utilidades compartidas por las tres cadenas.
/// </summary>
/// <remarks>
/// Los tres tipos son independientes a propósito: no comparten clase base ni
/// tienen conversiones entre sí. Una base común dejaría escribir un parámetro del
/// tipo base y volvería a aceptar cualquiera de las tres, que es exactamente el
/// error que estos tipos existen para impedir. La duplicación es el precio de que
/// «pedí la cadena equivocada» sea un error de compilación y no un incidente.
/// </remarks>
public static class CadenasDeConexion
{
    /// <summary>
    /// Deriva una cadena con OTRO usuario y contraseña, conservando host, puerto y
    /// base de la cadena del dueño.
    /// </summary>
    /// <remarks>
    /// Derivar y no configurar tres cadenas sueltas es deliberado: las tres tienen
    /// que apuntar a la MISMA base. Con tres cadenas independientes, un typo en el
    /// nombre de la base haría que el asistente leyera el ambiente equivocado sin
    /// que nada fallara.
    /// </remarks>
    public static string Derivar(CadenaDuena duena, string usuario, string password)
    {
        ArgumentNullException.ThrowIfNull(duena);
        if (string.IsNullOrWhiteSpace(usuario))
        {
            throw new ArgumentException("El usuario de la cadena derivada está vacío.", nameof(usuario));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("La contraseña de la cadena derivada está vacía.", nameof(password));
        }

        return new NpgsqlConnectionStringBuilder(duena.Valor)
        {
            Username = usuario,
            Password = password,
        }.ConnectionString;
    }

    /// <summary>Usuario de base de datos de una cadena.</summary>
    public static string Usuario(string valor) =>
        new NpgsqlConnectionStringBuilder(valor).Username ?? string.Empty;

    /// <summary>
    /// Devuelve la cadena sin la contraseña, para poder loguearla o mostrarla en
    /// un mensaje de error sin filtrar el secreto.
    /// </summary>
    public static string Redactar(string valor) =>
        new NpgsqlConnectionStringBuilder(valor) { Password = null }.ConnectionString;

    internal static string Validar(string valor, string tipo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException($"La cadena de conexión de {tipo} está vacía.", nameof(valor));
        }

        return valor;
    }
}
