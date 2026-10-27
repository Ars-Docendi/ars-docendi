using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>El guard que mantiene al evaluador fuera del CI (RNF-16).</summary>
public sealed class ExclusionDelEvaluadorTests
{
    /// <summary>Nombre del proyecto que instancia un proveedor real.</summary>
    private const string ProyectoDelEvaluador = "ArsDocendi.Evaluacion";

    /// <summary>Nombre del núcleo, que sí puede estar en la solución.</summary>
    private const string ProyectoDelNucleo = "ArsDocendi.Evaluacion.Nucleo";

    [Fact]
    public void El_archivo_de_solucion_no_incluye_al_evaluador()
    {
        var proyectos = ProyectosDeLaSolucion();

        var infractor = proyectos.FirstOrDefault(proyecto =>
            NombreDeProyecto(proyecto) == ProyectoDelEvaluador);

        Assert.True(
            infractor is null,
            $"El proyecto '{ProyectoDelEvaluador}' volvió a entrar al archivo de solución "
            + $"(entrada: {infractor}). El CI corre los tests de la solución SIN FILTRO, así que "
            + "adentro ejecutaría la API real decenas de veces por semana. El síntoma de "
            + "olvidarlo es una factura, no un test rojo. Sacalo de backend/ArsDocendi.slnx.");
    }

    [Fact]
    public void El_guard_leyo_el_archivo_y_encontro_proyectos()
    {
        // Anti-vacuidad. Si el archivo se moviera, se renombrara o el parseo dejara
        // de encontrar entradas, el test de arriba pasaría por no haber mirado
        // nada — que es la peor forma de fallar para un guard de costo.
        var proyectos = ProyectosDeLaSolucion();

        Assert.True(proyectos.Count >= 10, $"Solo se leyeron {proyectos.Count} proyectos.");
        Assert.Contains(proyectos, proyecto => NombreDeProyecto(proyecto) == "Modules.Asistente");
    }

    [Fact]
    public void El_nucleo_si_esta_en_la_solucion()
    {
        // La otra mitad de la decisión: lo que NO cuesta dinero tiene que estar adentro, o el
        // generador del fixture, la puntuación y el preflight se quedarían sin tests en el CI — y
        // son justo las piezas donde un error hace que el número mienta.
        var proyectos = ProyectosDeLaSolucion();

        Assert.Contains(proyectos, proyecto => NombreDeProyecto(proyecto) == ProyectoDelNucleo);
    }

    [Fact]
    public void El_evaluador_existe_fuera_de_la_solucion()
    {
        // Si el proyecto no existiera, el guard pasaría trivialmente para siempre.
        var proyecto = Path.Combine(
            RaizRepositorio.Ruta(), "backend", "eval", ProyectoDelEvaluador,
            $"{ProyectoDelEvaluador}.csproj");

        Assert.True(File.Exists(proyecto), $"No se encontró {proyecto}.");
    }

    [Fact]
    public void Ningun_proyecto_de_la_solucion_referencia_al_evaluador()
    {
        // La exclusión no sirve si alguien lo arrastra por referencia: entraría al
        // grafo de compilación del CI igual.
        var raiz = RaizRepositorio.Ruta();
        var infractores = ProyectosDeLaSolucion()
            .Select(entrada => Path.Combine(raiz, "backend", entrada.Replace('\\', '/')))
            .Where(File.Exists)
            .Where(ruta => File.ReadAllText(ruta)
                .Contains($"eval\\{ProyectoDelEvaluador}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(infractores);
    }

    [Fact]
    public void El_proveedor_por_omision_es_el_simulado()
    {
        // La última línea de defensa: aunque algo del CI llegara a construir el
        // módulo, sin configuración explícita usa el simulado y no gasta nada.
        Assert.Equal(ProveedorSimulado.Clave, new OpcionesAsistente().Proveedor);
    }

    // ------------------------------------------------------------------ apoyo

    /// <summary>Las rutas de proyecto declaradas en el archivo de solución.</summary>
    private static IReadOnlyList<string> ProyectosDeLaSolucion()
    {
        var solucion = Path.Combine(RaizRepositorio.Ruta(), "backend", "ArsDocendi.slnx");

        Assert.True(File.Exists(solucion), $"No se encontró el archivo de solución en {solucion}.");

        return
        [
            .. System.Text.RegularExpressions.Regex
                .Matches(File.ReadAllText(solucion), "Path=\"([^\"]+)\"")
                .Select(coincidencia => coincidencia.Groups[1].Value),
        ];
    }

    /// <summary>El nombre del proyecto a partir de su ruta.</summary>
    private static string NombreDeProyecto(string ruta) =>
        Path.GetFileNameWithoutExtension(ruta.Replace('\\', '/'));
}
