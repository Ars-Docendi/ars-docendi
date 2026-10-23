using ArsDocendi.Host.Desarrollo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// El archivo <c>.env</c> como fuente de configuración del Host.
/// </summary>
/// <remarks>
/// LO QUE ESTOS TESTS CUIDAN es que un archivo en disco no gane donde no debe.
/// Dos de las tres propiedades del diseño —el ambiente y la precedencia— no
/// producen ningún error cuando se rompen: el sistema sigue arrancando y sirviendo,
/// sólo que con el valor equivocado. Un <c>.env</c> viejo pisando lo que alguien
/// acaba de exportar se ve como «el cambio no tomó», y se busca en el lugar
/// equivocado durante horas.
///
/// La precedencia se VERIFICA contra el builder real y no se deduce del orden en
/// que .NET registra sus proveedores: ese orden es un detalle de implementación del
/// framework, y si cambia, acá tiene que fallar un test y no un ambiente.
/// </remarks>
public sealed class ArchivoDeEntornoTests
{
    private const string Clave = "Asistente:ClaveDelProveedor";
    private const string ClaveEnArchivo = "Asistente__ClaveDelProveedor";

    // ------------------------------------------------------------ interpretación

    [Fact]
    public void Traduce_el_doble_guion_bajo_a_la_separacion_de_secciones()
    {
        var valores = ArchivoDeEntorno.Interpretar([$"{ClaveEnArchivo}=sk-de-prueba"]);

        Assert.Equal("sk-de-prueba", valores[Clave]);
    }

    [Fact]
    public void Ignora_comentarios_lineas_vacias_y_renglones_sin_asignacion()
    {
        var valores = ArchivoDeEntorno.Interpretar(
        [
            "# un comentario",
            "",
            "   ",
            "esto no es una asignacion",
            "=sin_clave",
            "UNA=1",
        ]);

        Assert.Equal(["UNA"], valores.Keys);
    }

    [Fact]
    public void Acepta_el_prefijo_export_para_que_la_linea_se_copie_y_pegue()
    {
        // El punto no es soportar bash: es que la MISMA línea sirva exportada en la
        // terminal y escrita en el archivo, sin que nadie tenga que traducirla.
        var valores = ArchivoDeEntorno.Interpretar([$"export {ClaveEnArchivo}=sk-abc"]);

        Assert.Equal("sk-abc", valores[Clave]);
    }

    [Theory]
    [InlineData("UNA=\"con espacios\"", "con espacios")]
    [InlineData("UNA='con espacios'", "con espacios")]
    [InlineData("UNA=  con_margen  ", "con_margen")]
    [InlineData("UNA=valor # comentario al final", "valor")]
    [InlineData("UNA=\"valor # que no es comentario\"", "valor # que no es comentario")]
    [InlineData("UNA=sk-ant-con#numeral", "sk-ant-con#numeral")]
    [InlineData("UNA=", "")]
    public void Interpreta_comillas_y_comentarios_como_docker_compose(
        string linea, string esperado)
    {
        // Compose lee ESTE MISMO archivo. Dos lectores que entiendan distinto el
        // mismo renglón es una trampa que sólo aparece con un valor cortado.
        var valores = ArchivoDeEntorno.Interpretar([linea]);

        Assert.Equal(esperado, valores["UNA"]);
    }

    [Fact]
    public void Un_numeral_pegado_al_valor_no_corta_la_credencial()
    {
        // Caso separado y no una fila más del Theory: es el modo de falla peor de
        // todos —una clave truncada llega al proveedor y vuelve 401, que se lee
        // como «la clave está mal» y no como «el parser la cortó»—.
        var valores = ArchivoDeEntorno.Interpretar([$"{ClaveEnArchivo}=sk-ant-a#b#c"]);

        Assert.Equal("sk-ant-a#b#c", valores[Clave]);
    }

    // ------------------------------------------------------------------ ubicación

    [Fact]
    public void Ubica_el_archivo_de_la_raiz_subiendo_desde_un_subdirectorio()
    {
        using var raiz = new RaizTemporal(conAncla: true);
        var hondo = Directory.CreateDirectory(
            Path.Combine(raiz.Ruta, "backend", "src", "ArsDocendi.Host"));

        Assert.Equal(raiz.Archivo, ArchivoDeEntorno.Ubicar(hondo.FullName));
    }

    [Fact]
    public void Sin_el_ancla_del_repositorio_no_toma_ningun_env_suelto()
    {
        // Sin esta condición, un .env olvidado en el home del desarrollador entraría
        // a la configuración del Host de cualquier proyecto que corra debajo.
        using var raiz = new RaizTemporal(conAncla: false);

        Assert.Null(ArchivoDeEntorno.Ubicar(raiz.Ruta));
    }

    // ---------------------------------------------------------------- precedencia

    [Fact]
    public void En_desarrollo_el_archivo_le_gana_a_appsettings()
    {
        using var raiz = new RaizTemporal(conAncla: true);
        raiz.Escribir($"{ClaveEnArchivo}=del-archivo");

        var configuracion = new ConfigurationManager();
        configuracion.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [Clave] = "de-appsettings",
        });

        var aplicado = ArchivoDeEntorno.Sumar(
            new AmbienteFalso(Environments.Development), configuracion, raiz.Ruta);

        Assert.NotNull(aplicado);
        Assert.Equal("del-archivo", configuracion[Clave]);
    }

    [Fact]
    public void Una_variable_de_ambiente_real_le_gana_al_archivo()
    {
        using var raiz = new RaizTemporal(conAncla: true);
        raiz.Escribir($"{ClaveEnArchivo}=del-archivo");

        var nombre = $"ARSDOCENDI_TEST_{Guid.NewGuid():N}__Valor";
        Environment.SetEnvironmentVariable(nombre, "del-ambiente");

        try
        {
            raiz.Escribir($"{nombre}=del-archivo");

            var configuracion = new ConfigurationManager();
            configuracion.AddEnvironmentVariables();

            ArchivoDeEntorno.Sumar(
                new AmbienteFalso(Environments.Development), configuracion, raiz.Ruta);

            var clave = nombre.Replace("__", ":", StringComparison.Ordinal);
            Assert.Equal("del-ambiente", configuracion[clave]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(nombre, null);
        }
    }

    // --------------------------------------------------------------- el ambiente

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Fuera_de_desarrollo_el_archivo_no_se_lee(string ambiente)
    {
        // Staging entra acá aunque no sea producción: es un ambiente desplegado, y
        // ahí la credencial la pone la infra. Un archivo en disco que le gane sería
        // un segundo camino hacia la credencial, más débil y silencioso.
        using var raiz = new RaizTemporal(conAncla: true);
        raiz.Escribir($"{ClaveEnArchivo}=del-archivo");

        var configuracion = new ConfigurationManager();

        var aplicado = ArchivoDeEntorno.Sumar(
            new AmbienteFalso(ambiente), configuracion, raiz.Ruta);

        Assert.Null(aplicado);
        Assert.Null(configuracion[Clave]);
    }

    [Fact]
    public void Sin_archivo_el_arranque_sigue_sin_configurar_nada()
    {
        var vacio = Directory.CreateTempSubdirectory("ars-sin-env");

        try
        {
            var configuracion = new ConfigurationManager();

            Assert.Null(ArchivoDeEntorno.Sumar(
                new AmbienteFalso(Environments.Development), configuracion, vacio.FullName));
        }
        finally
        {
            vacio.Delete(recursive: true);
        }
    }

    // ------------------------------------------------------------------ andamios

    private sealed class RaizTemporal : IDisposable
    {
        public RaizTemporal(bool conAncla)
        {
            Ruta = Directory.CreateTempSubdirectory("ars-env").FullName;
            Archivo = Path.Combine(Ruta, ArchivoDeEntorno.Nombre);
            File.WriteAllText(Archivo, string.Empty);

            if (conAncla)
            {
                File.WriteAllText(Path.Combine(Ruta, ArchivoDeEntorno.Ancla), "services:");
            }
        }

        public string Ruta { get; }

        public string Archivo { get; }

        public void Escribir(string contenido) => File.WriteAllText(Archivo, contenido);

        public void Dispose() => Directory.Delete(Ruta, recursive: true);
    }

    private sealed class AmbienteFalso(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;

        public string ApplicationName { get; set; } = "ArsDocendi.Host";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
