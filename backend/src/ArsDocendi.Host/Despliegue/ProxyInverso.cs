using Microsoft.AspNetCore.HttpOverrides;

namespace ArsDocendi.Host.Despliegue;

/// <summary>
/// En los despliegues Cloudflare termina TLS y Traefik le habla al backend por http.
/// Sin estos headers el backend creería que el request es http: armaría la URL de
/// retorno del ingreso con <c>http://</c> (Microsoft responde AADSTS50011) y las
/// cookies anti-falsificación saldrían sin <c>Secure</c>.
/// </summary>
public static class ProxyInverso
{
    /// <summary>
    /// Mismo rango en el que confía Traefik (infra/traefik/traefik.yml): la red
    /// interna de Docker. Un request directo desde otra IP no puede fingir https.
    /// </summary>
    public const string RedInternaDocker = "172.16.0.0/12";

    public static IServiceCollection AddProxyInverso(this IServiceCollection servicios) =>
        servicios.Configure<ForwardedHeadersOptions>(opciones =>
        {
            opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            opciones.KnownIPNetworks.Clear();
            opciones.KnownProxies.Clear();
            opciones.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(RedInternaDocker));
        });
}
