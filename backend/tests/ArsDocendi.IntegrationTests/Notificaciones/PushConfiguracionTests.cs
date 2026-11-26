using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Notificaciones.Services;
using WebPush;

namespace ArsDocendi.IntegrationTests.Notificaciones;

public sealed class PushConfiguracionTests
{
    private static readonly VapidDetails Claves = VapidHelper.GenerateVapidKeys();

    private static OpcionesWebPush Opciones(string? subject = "mailto:soporte@unlam.edu.ar") =>
        new() { PublicKey = Claves.PublicKey, PrivateKey = Claves.PrivateKey, Subject = subject };

    private sealed class Ambiente(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ConfiguracionWebPush Crear(OpcionesWebPush opciones, string ambiente) =>
        new(Options.Create(opciones), new Ambiente(ambiente), NullLogger<ConfiguracionWebPush>.Instance);

    [Fact]
    public void Sin_claves_esta_deshabilitado_aunque_el_subject_este_mal()
    {
        var config = Crear(new OpcionesWebPush { Subject = "no-es-valido" }, Environments.Production);
        Assert.False(config.Habilitada);
        Assert.Null(config.ClavePublica);
    }

    [Theory]
    [InlineData("mailto:soporte@unlam.edu.ar")]
    [InlineData("https://ars-docendi.unlam.edu.ar")]
    public void Una_configuracion_valida_se_habilita(string subject)
    {
        var config = Crear(Opciones(subject), Environments.Production);
        Assert.True(config.Habilitada);
        Assert.Equal(Claves.PublicKey, config.ClavePublica);
    }

    [Theory]
    [InlineData("mailto:dev@ars-docendi.local")]
    [InlineData("https://app.localhost")]
    [InlineData("mailto:a@example.com")]
    [InlineData("mailto:a@sub.example.org")]
    public void Apple_rechaza_un_subject_en_dominio_reservado_asi_que_Production_no_arranca(string subject)
    {
        var excepcion = Assert.Throws<InvalidOperationException>(() => Crear(Opciones(subject), Environments.Production));
        Assert.Contains("Subject", excepcion.Message);
        Assert.DoesNotContain(Claves.PrivateKey, excepcion.Message);

        // Chrome and Firefox accept it: outside Production it only warns and push keeps working.
        Assert.True(Crear(Opciones(subject), Environments.Development).Habilitada);
    }

    [Theory]
    [InlineData("ftp://unlam.edu.ar")]
    [InlineData("soporte@unlam.edu.ar")]
    [InlineData("mailto:sin-dominio")]
    [InlineData("http://unlam.edu.ar")]
    [InlineData(null)]
    public void Un_subject_que_no_es_mailto_ni_https_es_un_error(string? subject)
    {
        Assert.Throws<InvalidOperationException>(() => Crear(Opciones(subject), Environments.Production));
        Assert.False(Crear(Opciones(subject), Environments.Development).Habilitada);
    }

    [Fact]
    public void Una_sola_clave_o_una_clave_mal_formada_es_un_error()
    {
        var soloPublica = new OpcionesWebPush { PublicKey = Claves.PublicKey, Subject = "mailto:a@unlam.edu.ar" };
        Assert.Throws<InvalidOperationException>(() => Crear(soloPublica, Environments.Production));

        var malformada = Opciones();
        malformada.PrivateKey = "abc";
        Assert.Throws<InvalidOperationException>(() => Crear(malformada, Environments.Production));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://web.push.apple.com/QAbc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://wns2-par02p.notify.windows.com/w/?token=abc", true)]
    [InlineData("https://eu.push.services.mozilla.com/abc", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com.evil.com/abc", false)]
    [InlineData("https://evil.com/fcm.googleapis.com", false)]
    [InlineData("https://evilpush.apple.com.attacker.net/x", false)]
    [InlineData("https://notpush.apple.com/x", false)]
    [InlineData("https://user:pass@fcm.googleapis.com/x", false)]
    [InlineData("https://fcm.googleapis.com:8443/x", false)]
    [InlineData("https://127.0.0.1/x", false)]
    [InlineData("https://169.254.169.254/latest/meta-data", false)]
    [InlineData("fcm.googleapis.com/x", false)]
    [InlineData("", false)]
    public void Solo_se_aceptan_endpoints_https_de_servicios_de_push_conocidos(string endpoint, bool esperado) =>
        Assert.Equal(esperado, EndpointsPush.EsPermitido(endpoint));
}
