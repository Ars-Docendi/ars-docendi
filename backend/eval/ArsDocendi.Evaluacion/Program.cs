using System.Globalization;
using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Fixture;
using ArsDocendi.Evaluacion.Nucleo.Runner;

namespace ArsDocendi.Evaluacion;

/// <summary>
/// Ejecutable del evaluador.
/// </summary>
/// <remarks>
/// Es deliberadamente delgado. Todo lo que decide algo —el generador del fixture,
/// la puntuación, el preflight, el formato del reporte— vive en el núcleo, que
/// está en la solución y tiene tests en el CI. Acá solo se arma el proveedor real
/// y se escribe el archivo.
///
/// Este proyecto NO está en <c>backend/ArsDocendi.slnx</c>. Ver el comentario del
/// <c>.csproj</c> y el guard <c>ExclusionDelEvaluadorTests</c>.
/// </remarks>
public static class Program
{
    private const int CodigoDeUso = 64;
    private const int CodigoSinProveedor = 3;

    public static async Task<int> Main(string[] argumentos)
    {
        var raiz = RaizDelRepositorio();
        var datasets = Path.Combine(raiz, "backend", "eval", "datasets");
        var reportes = Path.Combine(raiz, "backend", "eval", "reportes");

        if (argumentos.Contains("--ayuda", StringComparer.Ordinal))
        {
            Ayuda();
            return CodigoDeUso;
        }

        var capacidad = Path.Combine(datasets, "capacidad.json");
        if (!File.Exists(capacidad))
        {
            await Console.Error.WriteLineAsync($"No se encontró el dataset en {capacidad}.");
            return CodigoDeUso;
        }

        // Los cuatro ejes se cargan ACÁ, antes de mirar si hay proveedor: un dataset
        // mal armado tiene que fallar de forma visible aunque no haya con qué correr,
        // porque si no el error aparece recién el día que alguien consigue una clave.
        var cargado = DatasetDeCapacidad.Cargar(capacidad);
        var robustez = DatasetDeRobustez.Cargar(Path.Combine(datasets, "robustez.json"), cargado);
        var dialogo = DatasetDeDialogo.Cargar(Path.Combine(datasets, "dialogo.json"));
        var social = DatasetSocial.Cargar(Path.Combine(datasets, "social.json"));
        var fixture = new GeneradorDeFixture();

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Capacidad: {cargado.Items.Count} ítems · huella {cargado.Huella[..12]}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Robustez:  {robustez.Items.Count} ítems · huella {robustez.Huella[..12]}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Diálogo:   {dialogo.Turnos} turnos en {dialogo.Dialogos.Count} conversaciones · huella {dialogo.Huella[..12]}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Social:    {social.Items.Count} ítems · huella {social.Huella[..12]}"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Fixture:   huella {fixture.Huella()[..12]}"));

        // ─────────────────────────────────────────────────────────────────────
        // ACÁ VA EL PROVEEDOR REAL, y es lo único que falta para poder correr
        // esto de verdad.
        //
        // `IProveedorDeModelo` tiene hoy una sola implementación: la simulada. El
        // preflight la rechaza a propósito, así que el runner corta antes de
        // evaluar nada y no deja reporte — que es exactamente el comportamiento
        // que se quiere cuando no hay con qué medir.
        //
        // Elegir proveedor y modelo, y conseguir una clave, es una decisión de
        // producto y de costo. Cuando esté tomada, se implementa
        // `IProveedorDeModelo` contra ese proveedor y se lo construye acá,
        // leyendo la clave del ambiente. Nada más del pipeline cambia.
        // ─────────────────────────────────────────────────────────────────────
        await Console.Error.WriteLineAsync(
            """

            No hay ninguna implementación de proveedor de modelo real.

            El evaluador está completo y probado, pero producir un número exige
            elegir proveedor y modelo y tener una clave. Mientras tanto, el
            preflight rechazaría igual cualquier corrida contra el proveedor
            simulado: una corrida así no mediría nada y dejaría un reporte que
            parece una regresión del modelo.

            Ver backend/eval/README.md.
            """);

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"\n(Los cuatro reportes se escribirían en {reportes}, uno por eje.)"));
        Console.WriteLine(
            "Los ejes NO se promedian entre sí: un 0,7 de robustez y un 0,7 de diálogo\n"
            + "no significan ni valen lo mismo.");

        return CodigoSinProveedor;
    }

    private static void Ayuda() => Console.WriteLine(
        """
        Evaluador del asistente conversacional.

          dotnet run --project backend/eval/ArsDocendi.Evaluacion

        Necesita:
          · una base PostgreSQL con el esquema migrado y el fixture aplicado;
          · las cadenas de conexión del asistente en el ambiente;
          · un proveedor de modelo REAL — hoy no hay ninguno implementado.

        Cuatro ejes, con reportes separados: capacidad, robustez de fraseo,
        diálogo y social/meta. Más el gate de regresión con lock por ítem, que
        necesita una línea de base — y una línea de base sale de correr esto.

        No lo corre el CI, y no puede: este proyecto está fuera del archivo de
        solución a propósito. Ver backend/eval/README.md.
        """);

    /// <summary>
    /// Sube desde el directorio de salida hasta encontrar la raíz del repositorio.
    /// </summary>
    /// <remarks>
    /// Se busca un directorio conocido en vez de contar niveles: contar niveles
    /// rompe en silencio si el proyecto se mueve.
    /// </remarks>
    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            if (Directory.Exists(Path.Combine(directorio.FullName, "openspec")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException(
            $"No se encontró la raíz del repositorio subiendo desde {AppContext.BaseDirectory}.");
    }
}
