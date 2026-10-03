using System.Runtime.CompilerServices;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Los hosts de prueba corren en Development y por eso leen los user-secrets de
/// quien ejecuta la suite. Las variables de entorno pisan a los user-secrets, así
/// que se fija acá el ingreso con Microsoft apagado: cada prueba que lo necesita lo
/// habilita explícitamente con <c>UseSetting</c>.
/// </summary>
internal static class ConfiguracionLocalAislada
{
    [ModuleInitializer]
    internal static void Aislar() =>
        Environment.SetEnvironmentVariable("AutenticacionMicrosoft__Habilitada", "false");
}
