using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace Modules.Asistente;

/// <summary>
/// Registración del módulo del asistente conversacional en la composición del Host.
/// </summary>
public static class ModuleExtensions
{
    /// <summary>Nombre del cliente HTTP con el reintento de transporte.</summary>
    public const string ClienteDelProveedor = "asistente-proveedor";

    /// <summary>
    /// Suma el módulo del asistente al contenedor de dependencias.
    /// </summary>
    /// <remarks>
    /// La configuración de los roles NO se valida al arrancar: solo hace falta en
    /// el arranque one-shot <c>--migrate</c>, y exigirla siempre rompería el Host
    /// en cualquier ambiente que todavía no la tenga. El migrador falla con un
    /// mensaje que nombra el valor faltante cuando efectivamente la necesita.
    /// </remarks>
    /// <param name="envolverProveedor">
    /// Envoltorio opcional del proveedor base, aplicado ANTES del corte y del techo
    /// por turno.
    ///
    /// Existe para el evaluador, que necesita contar tokens de las llamadas que
    /// llegan de verdad al modelo. Contarlas afuera de la cadena no sirve: lo que
    /// se quiere saber es si un turno alcanzó al proveedor, y el techo y el corte
    /// pueden hacer que no lo alcance. Va adentro y no encima por eso.
    ///
    /// Nulo en producción, que es donde no hay nada que medir de más.
    /// </param>
    public static IServiceCollection AddAsistenteModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IProveedorDeModelo, IProveedorDeModelo>? envolverProveedor = null)
    {
        services.Configure<OpcionesAsistente>(
            configuration.GetSection(OpcionesAsistente.Seccion));

        // TryAdd: si el Host ya registró un TimeProvider, gana el suyo. Los tests
        // del hilo inyectan uno falso para poder adelantar el reloj sin esperar.
        //
        // Va primero porque de él dependen el breaker, la cuota, el presupuesto del
        // turno y la expiración del hilo: cuatro relojes que en un test tienen que
        // ser el mismo, o adelantar uno no mueve a los otros.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IMigradorModulo, MigradorAsistente>();

        // Las dos cadenas de solo lectura se DERIVAN de la del dueño: mismo host,
        // mismo puerto, misma base, otro usuario. Con tres cadenas configuradas por
        // separado, un typo en el nombre de la base haría que el asistente leyera
        // otro ambiente sin que nada fallara.
        //
        // Se registran como fábrica, no como valor: recién fallan si alguien las
        // pide sin haberlas configurado. Construirlas acá rompería el arranque en
        // cualquier ambiente que todavía no tenga los dos roles.
        services.AddSingleton(sp => new CadenaSoloLectura(CadenasDeConexion.Derivar(
            sp.GetRequiredService<CadenaDuena>(),
            Requerido(sp, o => o.RolSoloLectura, nameof(OpcionesAsistente.RolSoloLectura)),
            Requerido(sp, o => o.PasswordSoloLectura, nameof(OpcionesAsistente.PasswordSoloLectura)))));

        services.AddSingleton(sp => new CadenaSoloLecturaPii(CadenasDeConexion.Derivar(
            sp.GetRequiredService<CadenaDuena>(),
            Requerido(sp, o => o.RolSoloLecturaPii, nameof(OpcionesAsistente.RolSoloLecturaPii)),
            Requerido(sp, o => o.PasswordSoloLecturaPii, nameof(OpcionesAsistente.PasswordSoloLecturaPii)))));

        // Proveedor del modelo. Se registra como fábrica para que un nombre
        // desconocido —o una clave faltante— falle recién cuando alguien lo pida, y
        // no impida arrancar: el ping tiene que responder en cualquier ambiente.
        //
        // Este switch ES el registro de adaptadores. El puerto no menciona ningún
        // proveedor y la selección va por ambiente, así que sumar uno nuevo —otro
        // proveedor, o un modelo propio corriendo en la nube— es un brazo más acá y
        // una clase en Infrastructure. No hay nada del pipeline que rehacer.
        services.AddSingleton(sp =>
        {
            var valores = sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value;
            var elegido = valores.Proveedor;

            // Los tres esfuerzos se interpretan ACÁ aunque cada llamada los vuelva a
            // pedir. El parseo real ocurre por solicitud; esto es solo para que un
            // valor mal escrito rompa al resolver el proveedor y no en la primera
            // pregunta. Alguien que escribió «alta» en vez de «alto» quiere
            // enterarse al arrancar, no correr un mes con un esfuerzo que no eligió.
            ValidarEsfuerzos(valores);

            var proveedor = ConstruirProveedor(sp, valores, valores.Modelo);

            return new ProveedorBase(envolverProveedor?.Invoke(proveedor) ?? proveedor);
        });
        // La base que redacta. Es el MISMO switch —el registro de adaptadores sigue
        // siendo uno solo— con otro modelo. Vacío significa «el mismo que genera»,
        // que es el default: elegir un modelo más chico es una decisión de costo y
        // de calidad que se toma por ambiente, no algo que el módulo imponga.
        services.AddSingleton(sp =>
        {
            var valores = sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value;

            return string.IsNullOrWhiteSpace(valores.ModeloDeRedaccion)
                ? new BaseDeRedaccion(sp.GetRequiredService<ProveedorBase>().Valor)
                : new BaseDeRedaccion(ConstruirProveedor(sp, valores, valores.ModeloDeRedaccion));
        });

        // Contador y decorador son SCOPED: el techo es por turno, y un turno no
        // puede heredar el conteo del anterior.
        services.AddScoped(sp => new ContadorDeLlamadasDelTurno(
            sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value.MaximoDeLlamadasPorTurno));

        // ------------------------------------------- presupuesto y degradación

        // El breaker es SINGLETON: el estado del proveedor es del proceso, no del
        // request. Uno por turno no acumularía ningún fallo y nunca abriría.
        services.AddSingleton<BreakerDelProveedor>();

        // La cuota también, y por lo mismo: la ventana deslizante de un actor tiene
        // que sobrevivir a sus turnos.
        services.AddSingleton<ICuotaDelActor, CuotaEnMemoria>();
        services.AddSingleton<IDisponibilidadDelModelo, DisponibilidadDelModeloReal>();

        // Nadie puede pedir el proveedor sin pasar por el techo: la interfaz solo
        // resuelve al decorador.
        //
        // El orden va de afuera hacia adentro de más barato a más caro:
        //
        //     techo del turno  →  breaker + timeout  →  proveedor real
        //
        // Invertir los dos primeros haría que el breaker registrara intentos que el
        // techo iba a rechazar igual, y un solo turno desbocado terminaría abriendo
        // el corte para todos los demás.
        //
        // La cuota NO está en esta cadena: la cobra la capa conversacional, que es
        // lo único que conoce al actor. Meterla acá exigiría un objeto de request
        // mutable con el actor adentro, leído por capas que no lo declaran.
        services.AddScoped<IProveedorDeModelo>(
            sp => Encadenar(sp, sp.GetRequiredService<ProveedorBase>().Valor));

        // LA SEGUNDA CADENA, para redactar. Comparte el techo del turno y el corte
        // con la primera —el contador es scoped y el breaker singleton, así que las
        // dos resuelven los mismos— y eso es lo que hay que conservar: dos techos
        // independientes serían el doble de llamadas, y dos cortes independientes
        // harían que el proveedor tuviera que caerse dos veces para que el sistema
        // se entere una.
        //
        // Lo único distinto es el modelo. Que sea la COMPOSICIÓN la que elige quién
        // sirve cada caso de uso, y no el puerto, es lo que deja el contrato
        // intacto: `RedactorDeRespuesta` sigue pidiendo un `IProveedorDeModelo` y no
        // sabe que le tocó otro.
        services.AddKeyedScoped<IProveedorDeModelo>(
            ProveedorDeRedaccion,
            (sp, _) => Encadenar(sp, sp.GetRequiredService<BaseDeRedaccion>().Valor));

        // ---------------------------------------------------------------- carril SQL

        // El proveedor de esquema es SINGLETON porque cachea el prefijo: uno por
        // turno lo recalcularía en cada request y pagaría escritura de caché del
        // lado del proveedor sobre el bloque más grande del prompt.
        services.AddSingleton<IProveedorDeEsquema, ProveedorDeEsquema>();

        // El selector lee el catálogo embebido una sola vez, al construirse.
        services.AddSingleton<ISelectorDeEjemplos, SelectorDeEjemplos>();

        // El manifiesto es inmutable y se lee del assembly: una sola instancia.
        // El catálogo lo resuelve a identificadores del motor y cachea, con la
        // misma pereza que el prefijo del prompt — construirlo al arrancar
        // exigiría base durante el arranque, y el ping tiene que responder sin ella.
        services.AddSingleton(_ => ManifiestoDeSensibilidad.Cargar());
        services.AddSingleton<IClasificadorDeSensibilidad, CatalogoDeSensibilidad>();

        // La fecha se resuelve UNA VEZ por turno: con alcance de request, un turno
        // que empieza a las 23:59:59 no puede cambiar de día a la mitad.
        services.AddScoped<IFechaDeReferencia>(_ =>
            new FechaDeReferenciaFija(DateOnly.FromDateTime(DateTime.UtcNow)));

        services.AddScoped<IPerfilDelActor, ConsultorDeAlcance>();
        services.AddScoped<IEjecutorDeConsulta, EjecutorDeConsulta>();
        services.AddScoped<GeneradorDeSql>();
        services.AddScoped<RedactorDeRespuesta>();
        services.AddScoped<CarrilSql>();

        // ---------------------------------------------- capa conversacional

        // Singletons los dos, y por motivos distintos. El almacén de hilos ES el
        // estado conversacional: registrarlo con scope lo perdería entre requests,
        // que es exactamente lo que tiene que sobrevivir. El índice de entidades es
        // un caché, y uno por request no cachearía nada.
        services.AddSingleton<IAlmacenDeHilos, AlmacenDeHilosEnMemoria>();
        services.AddSingleton<IIndiceDeEntidades, IndiceDeEntidades>();

        // El catálogo del dominio es el índice de entidades MÁS el vocabulario
        // cerrado del trámite, compuestos afuera y no adentro: el índice lo consumen
        // el detector de ambigüedad y el de cambio de tema, y meterles «borrador» o
        // «Titular» como entidades les cambiaría el comportamiento en silencio.
        //
        // El catálogo de intenciones se carga ACÁ y no perezosamente. Es un archivo
        // embebido y no toca la base, así que cargarlo temprano no compromete el
        // invariante #3 —el ping responde con la base detenida— y hace que un
        // catálogo mal escrito rompa el arranque en lugar de la primera pregunta.
        services.AddSingleton<ICatalogoDelDominio, CatalogoDelDominioReal>();
        services.AddSingleton(CatalogoDeIntenciones.Cargar());
        services.AddScoped<ResolutorDeIntenciones>();
        services.AddScoped<EnrutadorDeDominio>();

        // El portador de la decisión sombra es SCOPED por el mismo motivo que el
        // contador de llamadas: la decisión es de UN turno, y uno singleton le
        // devolvería al registro la intención del turno anterior.
        services.AddScoped<DecisionSombraDelTurno>();

        services.AddScoped<ReescritorDePreguntas>();
        services.AddScoped<IRegistroDelTurno, RegistroDelTurno>();

        // ------------------------------------------------ superficie de usuario

        // La caché es SINGLETON —lo que cuesta leer del catálogo de PostgreSQL tiene
        // que sobrevivir al request— y el catálogo es SCOPED, porque depende del
        // perfil del actor del turno. Al revés, un catálogo singleton capturaría el
        // perfil del primer actor que consultara y se lo devolvería a todos los
        // demás; el contenedor rechaza esa registración al arrancar.
        services.AddSingleton<CacheDeCapacidades>();
        services.AddScoped<ICatalogoDeCapacidades, CatalogoDeCapacidades>();

        // Singleton también: la caché de idempotencia tiene que sobrevivir al
        // request, que es literalmente para lo que existe.
        services.AddSingleton<IIdempotencia, IdempotenciaEnMemoria>();
        services.AddScoped<CapaConversacional>();

        // -------------------------------------------- registros y su purga

        // La purga es scoped porque la cadena dueña lo es; el servicio que la
        // dispara abre un scope por vuelta en vez de capturarla para siempre.
        services.AddScoped<PurgaDeRegistros>();
        services.AddHostedService<ServicioDePurga>();

        // Cliente HTTP del proveedor, con el reintento de transporte ya puesto.
        // Todavía no lo consume nadie —el proveedor real llega con el carril SQL—,
        // pero se registra acá para que esa implementación lo pida por nombre y no
        // tenga que saber nada de reintentos.
        var clienteDelProveedor = services.AddHttpClient(ClienteDelProveedor);

        // EL GRABADOR VA PRIMERO, y por lo tanto POR FUERA del reintento.
        //
        // En AddHttpMessageHandler el orden de registración es de afuera hacia
        // adentro, así que el pipeline queda:
        //
        //     adaptador → grabador → reintento → transporte
        //
        // Con ese orden el grabador ve UNA solicitud por llamada lógica y la
        // respuesta que el reintento resolvió. Del lado de adentro vería cada
        // intento: los cuatro campos de la clave son iguales en los tres, así que
        // distinguirlos exigiría meter el número de intento en la clave —estado del
        // transporte y no de la pregunta—, y reproducir un fallo reproduciría el
        // backoff de verdad.
        //
        // Se lee de la configuración y no de IOptions porque la decisión es de
        // REGISTRACIÓN: con el directorio vacío el handler no se registra y el
        // pipeline queda idéntico al de antes de que este mecanismo existiera.
        var directorioDeCassettes = configuration[
            $"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.DirectorioDeCassettes)}"];

        if (!string.IsNullOrWhiteSpace(directorioDeCassettes))
        {
            clienteDelProveedor.AddHttpMessageHandler(sp =>
            {
                var opciones = sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value;

                return new GrabadorDeCassettes(
                    new AlmacenDeCassettes(opciones.DirectorioDeCassettes),
                    !string.IsNullOrWhiteSpace(opciones.RegrabarCassettes),

                    // Opcional a propósito: la registra quien sabe cuál es —el
                    // evaluador—, y sin ella no se graba ni se sirve nada. Un
                    // cassette que no se puede verificar contra el fixture vigente
                    // es indistinguible de uno grabado contra datos importados.
                    sp.GetService<HuellaDelFixture>()?.Valor ?? string.Empty,
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<GrabadorDeCassettes>>());
            });
        }

        clienteDelProveedor.AddHttpMessageHandler(sp =>
        {
            var opciones = sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value;
            return new ReintentoDeTransporte(
                opciones.MaximoDeIntentosDeTransporte,
                TimeSpan.FromMilliseconds(opciones.EsperaBaseMs),
                TimeSpan.FromMilliseconds(opciones.EsperaMaximaMs),
                Random.Shared);
        });

        services.AddControllers()
            .AddApplicationPart(typeof(ModuleExtensions).Assembly);

        return services;
    }

    private static string Requerido(
        IServiceProvider sp, Func<OpcionesAsistente, string> leer, string nombre)
    {
        var valor = leer(sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value);
        return string.IsNullOrWhiteSpace(valor)
            ? throw new InvalidOperationException(
                $"Falta '{OpcionesAsistente.Seccion}:{nombre}' en la configuración del ambiente.")
            : valor;
    }

    /// <summary>
    /// Arma el adaptador de Anthropic con el cliente HTTP que ya trae el reintento.
    /// </summary>
    /// <remarks>
    /// El transporte sale de la fábrica con nombre y no de un <c>HttpClient</c>
    /// propio: ahí adentro está <see cref="ReintentoDeTransporte"/>, que es la
    /// única autoridad de reintento del módulo. Un cliente propio dejaría al
    /// adaptador sin reintento, o —peor— lo tentaría a poner el suyo y multiplicar
    /// en silencio la cota de requests que el módulo documenta.
    /// </remarks>
    /// <summary>
    /// Interpreta los tres esfuerzos y descarta el resultado, para fallar temprano.
    /// </summary>
    /// <summary>
    /// El registro de adaptadores. Un proveedor nuevo es un brazo más acá.
    /// </summary>
    private static IProveedorDeModelo ConstruirProveedor(
        IServiceProvider sp, OpcionesAsistente valores, string modelo) => valores.Proveedor switch
        {
            ProveedorSimulado.Clave => new ProveedorSimulado(),
            ProveedorAnthropic.Clave => ArmarAnthropic(sp, modelo),
            _ => throw new InvalidOperationException(
                $"Proveedor de modelo '{valores.Proveedor}' desconocido. Los disponibles son "
                + $"'{ProveedorSimulado.Clave}' y '{ProveedorAnthropic.Clave}'."),
        };

    /// <summary>Clave del proveedor que redacta, en el contenedor.</summary>
    public const string ProveedorDeRedaccion = "asistente-redaccion";

    /// <summary>
    /// Envuelve un proveedor con el corte y el techo del turno.
    /// </summary>
    /// <remarks>
    /// El orden va de afuera hacia adentro de más barato a más caro:
    /// techo del turno → corte + timeout → proveedor real. Invertir los dos
    /// primeros haría que el corte registrara intentos que el techo iba a rechazar
    /// igual, y un solo turno desbocado abriría el corte para todos.
    /// </remarks>
    private static IProveedorDeModelo Encadenar(IServiceProvider sp, IProveedorDeModelo interno) =>
        new ProveedorConTechoDeLlamadas(
            new ProveedorConBreaker(
                interno,
                sp.GetRequiredService<BreakerDelProveedor>(),
                TimeSpan.FromSeconds(
                    sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value
                        .TimeoutDeLlamadaSegundos),
                sp.GetRequiredService<TimeProvider>()),
            sp.GetRequiredService<ContadorDeLlamadasDelTurno>());

    private static void ValidarEsfuerzos(OpcionesAsistente valores)
    {
        EsfuerzoConfigurado.Interpretar(
            valores.EsfuerzoDeGeneracion, nameof(OpcionesAsistente.EsfuerzoDeGeneracion));
        EsfuerzoConfigurado.Interpretar(
            valores.EsfuerzoDeRedaccion, nameof(OpcionesAsistente.EsfuerzoDeRedaccion));
        EsfuerzoConfigurado.Interpretar(
            valores.EsfuerzoDeReescritura, nameof(OpcionesAsistente.EsfuerzoDeReescritura));
    }

    private static ProveedorAnthropic ArmarAnthropic(IServiceProvider sp, string modelo) =>
        new(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(ClienteDelProveedor),
            Requerido(sp, o => o.ClaveDelProveedor, nameof(OpcionesAsistente.ClaveDelProveedor)),
            string.IsNullOrWhiteSpace(modelo)
                ? Requerido(sp, o => o.Modelo, nameof(OpcionesAsistente.Modelo))
                : modelo,
            sp.GetRequiredService<ILogger<ProveedorAnthropic>>());

}
