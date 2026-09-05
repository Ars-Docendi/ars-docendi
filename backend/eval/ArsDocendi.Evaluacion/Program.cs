using System.Globalization;
using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Fixture;
using ArsDocendi.Evaluacion.Nucleo.Runner;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Asistente;
using Modules.Asistente.Application;

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

        // El fixture se EMITE, no se aplica. El evaluador no tiene —ni debería
        // tener— la cadena del dueño: corre con los roles de solo lectura del
        // asistente, que es lo que hace que lo que mide sea lo que el asistente
        // puede ver. Emitirlo por salida estándar deja que lo aplique quien sí
        // tiene permiso de escritura, con psql o con lo que use ese ambiente.
        //
        // El README lo pedía aplicado y no daba forma de aplicarlo.
        if (argumentos.Contains("--fixture", StringComparer.Ordinal))
        {
            Console.Write(new GeneradorDeFixture().Generar());
            return 0;
        }

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
        // EL PROVEEDOR SALE DEL MÓDULO, no se construye acá.
        //
        // Se arma el contenedor del módulo y se le pide `IProveedorDeModelo`. Es
        // deliberado: el `switch` de `ModuleExtensions` ES el registro de
        // adaptadores, y construir uno a mano acá crearía una segunda forma de
        // elegir proveedor que puede quedar en desacuerdo con la de producción sin
        // que nada falle. Además el que devuelve el contenedor viene ya envuelto en
        // el reintento, el techo por turno y el corte, que es lo que corre de
        // verdad; medir sobre un proveedor desnudo mediría otro sistema.
        //
        // Sin `Asistente__Proveedor=anthropic` y su clave, esto resuelve el
        // simulado y el preflight lo rechaza — que es exactamente el
        // comportamiento que se quiere cuando no hay con qué medir.
        // ─────────────────────────────────────────────────────────────────────
        var configuracion = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging(registro => registro.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // La cadena del dueño la registra normalmente el Host, no el módulo. Acá el
        // Host no existe, así que se registra igual que allá: desde la
        // configuración, con la misma clave y la misma validación.
        servicios.AddSingleton(CadenaDuena.Desde(configuracion));
        // El medidor va DENTRO de la cadena y no al lado. El eje social pregunta si
        // un turno alcanzó al modelo, y contarlo desde afuera contaría lo que el
        // evaluador pidió y no lo que el pipeline dejó pasar: el techo por turno y
        // el corte pueden hacer que la llamada no llegue nunca.
        //
        // La primera corrida lo tuvo afuera y el eje se auto-rechazó —«ningún turno
        // consumió tokens»—, que es exactamente lo que ese guard existe para hacer.
        MedidorDeConsumo? medidor = null;
        servicios.AddAsistenteModule(
            configuracion, interno => medidor = new MedidorDeConsumo(interno));

        // La huella del fixture, para el sellado de los cassettes del proveedor.
        //
        // La registra el evaluador y no el módulo porque el módulo no referencia al
        // núcleo de evaluación ni debe hacerlo, y acá ya se recalcula en cada corrida
        // para sellar los reportes. Sin ella el grabador no graba ni sirve nada: un
        // cassette que no se puede verificar contra el fixture vigente es
        // indistinguible de uno grabado contra una base con datos importados.
        //
        // Sin `Asistente__DirectorioDeCassettes` el handler ni se registra y esto
        // queda sin consumidor, que es el caso normal.
        servicios.AddSingleton(new HuellaDelFixture(fixture.Huella()));

        // La fecha de referencia se clava en el ancla del fixture. El pipeline la
        // pasa como parámetro y nunca usa el reloj, así que fijarla acá es lo que
        // hace que «los nombramientos abiertos» signifique lo mismo hoy que dentro
        // de un año, y que dos corridas comparables lo sean de verdad.
        servicios.AddScoped<IFechaDeReferencia>(
            _ => new FechaDeReferenciaFija(GeneradorDeFixture.Ancla));

        await using var raizDeServicios = servicios.BuildServiceProvider();
        var alcances = new List<IServiceScope>();

        // UNA FÁBRICA Y NO UNA INSTANCIA, y el motivo no es de estilo: el techo de
        // llamadas es POR TURNO. Un carril compartido para todo el dataset lo
        // convertiría en un techo de la corrida entera —el tercer ítem lo agotaría
        // y todos los siguientes resolverían degradado—. El modo de fallar no da
        // error: da un número, más bajo, que parece del modelo.
        T PorTurno<T>() where T : notnull
        {
            var alcance = raizDeServicios.CreateScope();
            alcances.Add(alcance);
            return alcance.ServiceProvider.GetRequiredService<T>();
        }

        try
        {
            using var alcanceRaiz = raizDeServicios.CreateScope();
            var raizDeAlcance = alcanceRaiz.ServiceProvider;

            var proveedor = raizDeAlcance.GetRequiredService<IProveedorDeModelo>();
            var ejecutor = raizDeAlcance.GetRequiredService<IEjecutorDeConsulta>();
            var esquema = await raizDeAlcance
                .GetRequiredService<IProveedorDeEsquema>()
                .ObtenerAsync(conDatosPersonales: false, CancellationToken.None);

            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Proveedor: {proveedor.Nombre}"));

            var preflight = await Preflight.VerificarAsync(proveedor, CancellationToken.None);

            if (!preflight.Aprobado)
            {
                await Console.Error.WriteLineAsync($"\nPreflight rechazado: {preflight.Motivo}");
                return CodigoSinProveedor;
            }

            var actores = new ActoresDelFixture();

            // El proveedor ya se resolvió arriba, así que la fábrica del envoltorio
            // corrió y el medidor existe. Si no existiera, el eje social mediría
            // cero para siempre.
            var medidorDelTurno = medidor ?? throw new InvalidOperationException(
                "El módulo no aplicó el envoltorio del proveedor.");

            var capacidadRunner = new RunnerDeCapacidad(
                PorTurno<CarrilSql>, ejecutor, actores, proveedor);

            // Los cuatro ejes, con su sello propio: cada uno se sella con la huella
            // de SU dataset, porque el gate de regresión compara reporte contra
            // línea de base del mismo eje.
            SelloDeIdentidad Sello(string huellaDelDataset) =>
                new(esquema.Huella, huellaDelDataset, fixture.Huella());

            var corridas = new (string Nombre, Func<Task<ResultadoDeCorrida>> Correr)[]
            {
                ("capacidad", () => capacidadRunner.CorrerAsync(
                    cargado, Sello(cargado.Huella), CancellationToken.None)),
                ("robustez", () => new RunnerDeRobustez(capacidadRunner, proveedor).CorrerAsync(
                    robustez, Sello(robustez.Huella), CancellationToken.None)),
                ("dialogo", () => new RunnerDeDialogo(
                    PorTurno<CapaConversacional>, ejecutor, actores, proveedor).CorrerAsync(
                    dialogo, Sello(dialogo.Huella), CancellationToken.None)),
                ("social", () => new RunnerSocial(
                    PorTurno<CapaConversacional>, actores, medidorDelTurno).CorrerAsync(
                    social, Sello(social.Huella), CancellationToken.None)),
            };

            Directory.CreateDirectory(reportes);
            var salida = 0;

            foreach (var (nombre, correr) in corridas)
            {
                Console.WriteLine($"\n▸ Eje {nombre}…");
                var resultado = await correr();

                if (!resultado.HayReporte)
                {
                    await Console.Error.WriteLineAsync($"  {resultado.Motivo}");
                    salida = resultado.Codigo;
                    continue;
                }

                var ruta = Path.Combine(reportes, $"{nombre}.md");
                await File.WriteAllTextAsync(ruta, resultado.Reporte!.Renderizar());

                Console.WriteLine($"  {resultado.Reporte.Total} ítems · reporte en {ruta}");
            }

            Console.WriteLine(
                "\nLos ejes NO se promedian entre sí: un 0,7 de robustez y un 0,7 de diálogo\n"
                + "no significan ni valen lo mismo.");

            return salida;
        }
        finally
        {
            foreach (var alcance in alcances)
            {
                alcance.Dispose();
            }
        }
    }

    /// <summary>
    /// Resuelve los actores del fixture por su alcance.
    /// </summary>
    /// <remarks>
    /// Los cuatro índices son los que el fixture asigna en
    /// <c>GeneradorDeFixture.EscribirAsignaciones</c>, y el orden no es arbitrario:
    /// 0 es Secretaría con alcance global, 1 el coordinador de la carrera 0, 2 el
    /// jefe de cátedra de la materia 0 y 3 el docente SIN permiso de designaciones
    /// —el caso en que RLS devuelve cero filas por falta de permiso y no por falta
    /// de datos, que es justo lo que el eje mide—.
    ///
    /// Los identificadores salen del generador y no están escritos acá: si el
    /// fixture cambia de identificadores, esto sigue apuntando a la persona
    /// correcta en vez de a un GUID que ya no existe.
    /// </remarks>
    private sealed class ActoresDelFixture : IResolutorDeActores
    {
        public Guid Resolver(string actor) => Guid.Parse(GeneradorDeFixture.IdDeUsuario(
            actor switch
            {
                "global" => 0,
                "carrera" => 1,
                "materia" => 2,
                "sin_permiso" => 3,
                _ => throw new InvalidOperationException(
                    $"El dataset pide un actor '{actor}' que el fixture no define. "
                    + "Los alcances son: global, carrera, materia, sin_permiso."),
            }));
    }

    private static void Ayuda() => Console.WriteLine(
        """
        Evaluador del asistente conversacional.

          dotnet run --project backend/eval/ArsDocendi.Evaluacion

        Emitir el SQL del fixture por salida estándar, para aplicarlo:

          dotnet run --project backend/eval/ArsDocendi.Evaluacion -- --fixture

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
