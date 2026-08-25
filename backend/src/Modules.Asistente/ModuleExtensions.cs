using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public static IServiceCollection AddAsistenteModule(
        this IServiceCollection services,
        IConfiguration configuration)
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
        // desconocido falle recién cuando alguien lo pida, y no impida arrancar.
        services.AddSingleton(sp =>
        {
            var elegido = sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value.Proveedor;
            return new ProveedorBase(elegido switch
            {
                ProveedorSimulado.Clave => new ProveedorSimulado(),
                _ => throw new InvalidOperationException(
                    $"Proveedor de modelo '{elegido}' desconocido. Hoy el único disponible es "
                    + $"'{ProveedorSimulado.Clave}'; el real llega con el carril SQL."),
            });
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
        services.AddScoped<IProveedorDeModelo>(sp =>
        {
            var valores = sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value;

            return new ProveedorConTechoDeLlamadas(
                new ProveedorConBreaker(
                    sp.GetRequiredService<ProveedorBase>().Valor,
                    sp.GetRequiredService<BreakerDelProveedor>(),
                    TimeSpan.FromSeconds(valores.TimeoutDeLlamadaSegundos),
                    sp.GetRequiredService<TimeProvider>()),
                sp.GetRequiredService<ContadorDeLlamadasDelTurno>());
        });

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
        services.AddHttpClient(ClienteDelProveedor)
            .AddHttpMessageHandler(sp =>
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
}
