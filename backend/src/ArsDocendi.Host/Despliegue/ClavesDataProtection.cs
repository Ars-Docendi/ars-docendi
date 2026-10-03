using Microsoft.AspNetCore.DataProtection;

namespace ArsDocendi.Host.Despliegue;

/// <summary>
/// Las claves de Data Protection cifran la cookie de sesión y los tokens
/// anti-falsificación. En un contenedor viven en su sistema de archivos y se
/// perderían en cada redeploy, cerrando todas las sesiones: en los despliegues se
/// guardan en un volumen por ambiente (<c>DataProtection:DirectorioClaves</c>).
/// </summary>
public static class ClavesDataProtection
{
    public const string Seccion = "DataProtection";

    public static IServiceCollection AddClavesDataProtection(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        var dataProtection = servicios.AddDataProtection().SetApplicationName("ars-docendi");
        var directorio = configuracion[$"{Seccion}:DirectorioClaves"];
        if (!string.IsNullOrWhiteSpace(directorio))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(directorio));
        }

        return servicios;
    }
}
