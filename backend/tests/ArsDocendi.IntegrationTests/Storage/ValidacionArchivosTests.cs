using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage;
using ArsDocendi.Storage.Contracts;
using ArsDocendi.Storage.Infrastructure;

namespace ArsDocendi.IntegrationTests.Storage;

public sealed class ValidacionArchivosTests
{
    private static readonly AlmacenamientoOptions Opciones = new()
    {
        TamanoMaximoPdf = 1024,
        TamanoMaximoImagen = 512,
    };

    [Fact]
    public void Detecta_firmas_reales_y_no_extensiones()
    {
        Assert.Equal("application/pdf", ReglasArchivos.DetectarMime("%PDF-1.7"u8));
        Assert.Equal("image/jpeg", ReglasArchivos.DetectarMime(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.Equal("image/png", ReglasArchivos.DetectarMime(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        Assert.Equal("application/octet-stream", ReglasArchivos.DetectarMime("not-a-pdf"u8));
    }

    [Fact]
    public void Calcula_hash_sha256_estable_y_de_longitud_fija()
    {
        var contenido = "contenido de prueba"u8;
        var hash = ReglasArchivos.CalcularSha256(contenido);

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, ReglasArchivos.CalcularSha256(contenido));
        Assert.NotEqual(hash, ReglasArchivos.CalcularSha256("contenido distinto"u8));
    }

    [Fact]
    public void Rechaza_proposito_mime_y_tamano_no_admitidos()
    {
        var error = Assert.Throws<ExcepcionAplicacion>(() => ReglasArchivos.ValidarInicio(
            new IniciarCargaArchivoDto(PropositosArchivo.Cv, "curriculum.pdf", "image/png", 10), Opciones));
        Assert.Equal("archivo-validation", error.Codigo);

        Assert.Throws<ExcepcionAplicacion>(() => ReglasArchivos.ValidarInicio(
            new IniciarCargaArchivoDto("externo", "archivo.pdf", "application/pdf", 10), Opciones));
        Assert.Throws<ExcepcionAplicacion>(() => ReglasArchivos.ValidarInicio(
            new IniciarCargaArchivoDto(PropositosArchivo.Cv, "archivo.pdf", "application/pdf", 1025), Opciones));
    }

    [Fact]
    public void Sanitiza_nombre_y_aplica_politica_por_proposito()
    {
        Assert.Equal("informe.pdf", ReglasArchivos.SanitizarNombre("../../informe.pdf"));
        ReglasArchivos.ValidarContenido(PropositosArchivo.Cv, "application/pdf", 100, Opciones);
        ReglasArchivos.ValidarContenido(PropositosArchivo.DniFrente, "image/png", 100, Opciones);
        Assert.Throws<ExcepcionAplicacion>(() => ReglasArchivos.ValidarContenido(
            PropositosArchivo.DniFrente, "application/pdf", 100, Opciones));
    }
}
