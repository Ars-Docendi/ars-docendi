using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

    /// <summary>Suma el módulo del asistente al contenedor de dependencias.</summary>
    public static IServiceCollection AddAsistenteModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IProveedorDeModelo, IProveedorDeModelo>? envolverProveedor = null)
    {
        services.Configure<OpcionesAsistente>(
            configuration.GetSection(OpcionesAsistente.Seccion));

        // SIN `ValidateOnStart`, y es una decisión. Con arranque validado, un
        // `Asistente:TopeDeFilas` en cero tumbaría el Host entero —incluido `/api/tareas/ping`, que
        // no tiene nada que ver con esto—, y eso invierte la postura que este mismo archivo tiene
        // escrita doce líneas más abajo: el proveedor se registra como fábrica para que una
        // configuración incompleta falle cuando alguien la pida y no impida arrancar.
        services.AddSingleton<IValidateOptions<OpcionesAsistente>, ValidadorDeOpcionesAsistente>();

        // TryAdd: si el Host ya registró un TimeProvider, gana el suyo. Los tests del hilo inyectan
        // uno falso para poder adelantar el reloj sin esperar.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IMigradorModulo, MigradorAsistente>();

        // Las dos cadenas de solo lectura se DERIVAN de la del dueño: mismo host, mismo puerto,
        // misma base, otro usuario.
        services.AddSingleton(sp => new CadenaSoloLectura(CadenasDeConexion.Derivar(
            sp.GetRequiredService<CadenaDuena>(),
            Requerido(sp, o => o.RolSoloLectura, nameof(OpcionesAsistente.RolSoloLectura)),
            Requerido(sp, o => o.PasswordSoloLectura, nameof(OpcionesAsistente.PasswordSoloLectura)))));

        services.AddSingleton(sp => new CadenaSoloLecturaPii(CadenasDeConexion.Derivar(
            sp.GetRequiredService<CadenaDuena>(),
            Requerido(sp, o => o.RolSoloLecturaPii, nameof(OpcionesAsistente.RolSoloLecturaPii)),
            Requerido(sp, o => o.PasswordSoloLecturaPii, nameof(OpcionesAsistente.PasswordSoloLecturaPii)))));

        // Proveedor del modelo. Se registra como fábrica para que un nombre desconocido —o una
        // clave faltante— falle recién cuando alguien lo pida, y no impida arrancar: el ping tiene
        // que responder en cualquier ambiente.
        services.AddKeyedSingleton<IProveedorDeModelo>(AdaptadorDeGeneracion, (sp, _) =>
        {
            var valores = sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value;
            var elegido = valores.Proveedor;

            // Los tres esfuerzos se interpretan ACÁ aunque cada llamada los vuelva a pedir.
            ValidarEsfuerzos(valores);

            return ConstruirProveedor(sp, valores, valores.Modelo);
        });

        services.AddSingleton(sp =>
        {
            var crudo = sp.GetRequiredKeyedService<IProveedorDeModelo>(AdaptadorDeGeneracion);

            return new ProveedorBase(envolverProveedor?.Invoke(crudo) ?? crudo);
        });
        // La base que redacta. Es el MISMO switch —el registro de adaptadores sigue siendo uno
        // solo— con otro modelo.
        services.AddSingleton(sp =>
        {
            var valores = sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value;

            // Con `ModeloDeRedaccion` vacío se reusa la MISMA instancia, así que el adaptador de
            // redacción no se resuelve, no se construye y no se dispone: hay un solo dueño para un
            // solo objeto.
            return string.IsNullOrWhiteSpace(valores.ModeloDeRedaccion)
                ? new BaseDeRedaccion(sp.GetRequiredService<ProveedorBase>().Valor)
                : new BaseDeRedaccion(
                    sp.GetRequiredKeyedService<IProveedorDeModelo>(AdaptadorDeRedaccion));
        });

        services.AddKeyedSingleton<IProveedorDeModelo>(AdaptadorDeRedaccion, (sp, _) =>
        {
            var valores = sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value;

            return ConstruirProveedor(sp, valores, valores.ModeloDeRedaccion);
        });

        // La ÚNICA puerta a las bases del sistema para leer. Singleton porque lleva adentro dos
        // `NpgsqlDataSource` —uno por rol— y ahí vive el pool: uno por request tiraría el pool en
        // cada turno.
        services.AddSingleton<AperturaDeLectura>();

        // Contador y decorador son SCOPED: el techo es por turno, y un turno no
        // puede heredar el conteo del anterior.
        services.AddScoped(sp => new ContadorDeLlamadasDelTurno(
            sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value.MaximoDeLlamadasPorTurno));

        // ------------------------------------------- presupuesto y degradación

        // El registro de breakers es SINGLETON: el estado del proveedor es del proceso, no del
        // request.
        services.AddSingleton<RegistroDeBreakers>();

        // La compuerta de concurrencia (asistente-proveedor-local, D4) es SINGLETON por el mismo
        // motivo que el breaker: la GPU es una sola para todo el proceso.
        services.AddSingleton(sp => new CompuertaDelModelo(
            Math.Max(1, sp.GetRequiredService<IOptions<OpcionesAsistente>>().Value.MaximoDeLlamadasConcurrentes),
            sp.GetRequiredService<TimeProvider>()));

        // SCOPED desde asistente-administracion-de-uso, y no por elección: la cuota ahora vive en
        // Postgres (CuotaPersistente) y resuelve el rol del actor a través de IConsultasIdentity,
        // que el Host registra scoped — capturarlo desde un singleton sería una dependencia
        // cautiva.
        services.AddSingleton<DetectorDeUmbrales>();

        services.AddScoped<ICuotaDelActor, CuotaPersistente>();

        // Misma vida y mismo motivo que CuotaPersistente: resuelve los roles del
        // actor vía IConsultasIdentity, scoped (asistente-acceso-granular).
        services.AddScoped<IAccesoAlAsistente, AccesoPersistente>();

        // También scoped: PresupuestoOrganizacionalPersistente no depende de IConsultasIdentity,
        // pero DisponibilidadDelModeloReal ya lo es por ICuotaDelActor, y las dos piezas del mismo
        // veredicto conviven mejor con el mismo alcance.
        services.AddScoped<IPresupuestoOrganizacional, PresupuestoOrganizacionalPersistente>();

        // El kill switch (design.md D7): sin caché de proceso, misma vida que
        // el resto de esta sección.
        services.AddScoped<IDisponibilidadDelModulo, DisponibilidadDelModuloReal>();
        services.AddScoped<IAuditoriaDeAdministracion, AuditoriaDeAdministracionReal>();
        services.AddScoped<IConsultasDeUso, ConsultasDeUso>();
        // Quality metrics (asistente-metricas-de-calidad): two readers confined to the
        // analytic record and the vote table, and the audit of the problem list.
        services.AddScoped<IConsultasDeCalidad, ConsultasDeCalidad>();
        services.AddScoped<IConsultasDeProblemasDeCalidad, ConsultasDeProblemasDeCalidad>();
        services.AddScoped<IAuditoriaDeRevisionDeCalidad, AuditoriaDeRevisionDeCalidad>();
        services.AddScoped<IPresupuestosAdministrables, PresupuestosAdministrablesReal>();

        // Runtime model (asistente-modelo-en-caliente, slice 1). Inert: nothing here is resolved at
        // startup, and CifradoDeCredenciales reads the master keys only when a key operation is
        // requested, so the module starts and the ping answers with no master key configured.
        services.AddSingleton<CifradoDeCredenciales>();
        services.AddScoped<RepositorioDeCredenciales>();
        services.AddScoped<RepositorioDelModelo>();
        services.AddScoped<RepositorioDePrecios>();
        // Slice 3: the administration endpoints. Resolved per request only.
        services.AddScoped<ProbadorDeConexion>();
        services.AddScoped<Api.AdministracionDelModelo>();

        // Telemetría del servidor del modelo propio (asistente-optimizaciones-
        // modelo-local, D8): un cliente aparte, con timeout corto y SIN el
        // reintento del proveedor — es un panel, no un turno.
        services.AddHttpClient(TelemetriaDelServidorLocal.Cliente, cliente => cliente.Timeout = TimeSpan.FromSeconds(3));

        // El servicio de inferencia compartido (integrar-cliente-inferencia, ahora en este módulo:
        // asistente-proveedor-ollama).
        services.AddOptions<OpcionesInferencia>()
            .Bind(configuration.GetSection(OpcionesInferencia.Seccion))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<OpcionesInferencia>, ValidacionOpcionesInferencia>();
        services.AddHttpClient<ClienteInferencia>();
        services.AddScoped<TelemetriaDelServidorLocal>();

        // El primer contrato público del módulo (sistema-seccion-unificada, design.md D1): el Host
        // los resuelve para el feed unificado de auditoría y el estado del sistema, sin SQL directo
        // ni referenciar estos internals.
        services.AddScoped<Contracts.IConsultasDeAuditoriaDeAdministracion,
            ConsultasDeAuditoriaDeAdministracion>();
        services.AddScoped<Contracts.IConsultaDeMantenimiento, ConsultaDeMantenimiento>();

        services.AddScoped<IDisponibilidadDelModelo, DisponibilidadDelModeloReal>();

        // El candado del turno (asistente-turno-exclusivo-del-actor) abre su
        // PROPIA conexión dedicada, sin pool (ver CandadoDelTurno) — scoped
        // igual que el resto de esta sección, una instancia por turno.
        services.AddScoped<ICandadoDelTurno, CandadoDelTurnoReal>();

        // Nadie puede pedir el proveedor sin pasar por el techo: la interfaz solo resuelve al
        // decorador.
        services.AddSingleton(new EnvoltorioDelProveedor(envolverProveedor));
        services.AddScoped<ILectorDeLaSeleccion, LectorDeLaSeleccion>();
        services.AddScoped<ResolutorDelProveedor>();
        services.AddScoped<IBreakerDelTurno>(sp => sp.GetRequiredService<ResolutorDelProveedor>());
        services.AddScoped<IEnmascaradoDelTurno>(sp => sp.GetRequiredService<ResolutorDelProveedor>());

        services.AddScoped<IProveedorDeModelo>(
            sp => new ProveedorDelTurno(sp.GetRequiredService<ResolutorDelProveedor>(), paraRedactar: false));

        // LA SEGUNDA CADENA, para redactar. Comparte el techo del turno y el corte con la primera
        // —el contador es scoped y el breaker singleton, así que las dos resuelven los mismos— y
        // eso es lo que hay que conservar: dos techos independientes serían el doble de llamadas, y
        // dos cortes independientes harían que el proveedor tuviera que caerse dos veces para que
        // el sistema se entere una.
        services.AddKeyedScoped<IProveedorDeModelo>(
            ProveedorDeRedaccion,
            (sp, _) => new ProveedorDelTurno(sp.GetRequiredService<ResolutorDelProveedor>(), paraRedactar: true));

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

        // La fecha se resuelve UNA VEZ por turno: con alcance de request, un turno que empieza a
        // las 23:59:59 no puede cambiar de día a la mitad.
        services.AddScoped<IFechaDeReferencia>(sp => new FechaDeReferenciaFija(
            DateOnly.FromDateTime(sp.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime)));

        services.AddScoped<IPerfilDelActor, ConsultorDeAlcance>();
        services.AddScoped<IConsultorDeCobertura, ConsultorDeCobertura>();
        services.AddScoped<IEjecutorDeConsulta, EjecutorDeConsulta>();
        services.AddScoped<IBuscadorDeMenciones, BuscadorDeMenciones>();
        // La caché de consultas generadas (asistente-optimizaciones-modelo-local, D6) es SINGLETON:
        // sirve entre turnos y entre actores.
        services.AddSingleton<CacheDeConsultasGeneradas>();
        services.AddScoped<GeneradorDeSql>();
        services.AddScoped<RedactorDeRespuesta>();
        // Lo llena el endpoint de flujo en el mismo request (D9).
        services.AddScoped<CanalDeRedaccion>();

        services.AddScoped<CarrilSql>();
        // El plan compilado (asistente-plan-compilado): registrado siempre, usado
        // sólo con la opción encendida. `CarrilSql` lo recibe como opcional.
        services.AddScoped<GeneradorDePlan>();
        services.AddScoped<ResolutorDeEntidadesDelPlan>();
        services.AddScoped(proveedor => new CarrilDelPlan(
            proveedor.GetRequiredService<GeneradorDePlan>(),
            proveedor.GetRequiredService<ResolutorDeEntidadesDelPlan>(),
            proveedor.GetRequiredService<IEjecutorDeConsulta>(),
            proveedor.GetRequiredService<IFechaDeReferencia>(),
            proveedor.GetRequiredService<ContadorDeLlamadasDelTurno>(),
            proveedor.GetRequiredService<IOptions<OpcionesAsistente>>(),
            proveedor.GetRequiredService<ILogger<CarrilDelPlan>>()));

        // ---------------------------------------------- capa conversacional

        // Singletons los dos, y por motivos distintos. El almacén de hilos ES el estado
        // conversacional: registrarlo con scope lo perdería entre requests, que es exactamente lo
        // que tiene que sobrevivir.
        services.AddSingleton<IAlmacenDeHilos, AlmacenDeHilosEnMemoria>();
        services.AddSingleton<IIndiceDeEntidades, IndiceDeEntidades>();

        // El catálogo del dominio es el índice de entidades MÁS el vocabulario cerrado del trámite,
        // compuestos afuera y no adentro: el índice lo consumen el detector de ambigüedad y el de
        // cambio de tema, y meterles «borrador» o «Titular» como entidades les cambiaría el
        // comportamiento en silencio.
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

        // Scoped, same reason as IRegistroDelTurno: opens the owner connection
        // per call, no state to survive the request.
        services.AddScoped<IRegistroDeHistorial, RegistroDeHistorial>();
        services.AddScoped<IConsultasDeHistorial, ConsultasDeHistorial>();
        services.AddScoped<IConsultasDeAuditoriaDeSoporte, ConsultasDeAuditoriaDeSoporte>();

        // ------------------------------------------------ superficie de usuario

        // La caché es SINGLETON —lo que cuesta leer del catálogo de PostgreSQL tiene que sobrevivir
        // al request— y el catálogo es SCOPED, porque depende del perfil del actor del turno.
        services.AddSingleton<CacheDeCapacidades>();
        services.AddScoped<ICatalogoDeCapacidades, CatalogoDeCapacidades>();

        // Singleton también: la caché de idempotencia tiene que sobrevivir al
        // request, que es literalmente para lo que existe.
        services.AddSingleton<IIdempotencia, IdempotenciaEnMemoria>();

        // Same lifetime and the same reason: the feedback token store has to survive the request
        // that minted the token so a LATER request can still find it.
        services.AddSingleton<IValidezDeRetroalimentacion, ValidezDeRetroalimentacionEnMemoria>();

        // Scoped: it opens the owner connection per call, same as
        // IRegistroDelTurno.
        services.AddScoped<IRegistroDeRetroalimentacion, RegistroDeRetroalimentacion>();
        services.AddScoped<ServicioDeRetroalimentacion>();

        // El puerto de vínculos arranca SIN resolver nada. Quién puede abrir un trámite lo sabe el
        // módulo de designaciones, y este módulo no lo referencia: la composición la hace el Host,
        // que ve a los dos, y su registro reemplaza a éste.
        services.AddScoped<IResolutorDeVinculos, SinVinculos>();

        // Igual con las fichas: el asistente no sabe qué datos tiene un trámite, así que el puerto
        // arranca sin ninguna y el Host lo reemplaza.
        services.AddScoped<IProveedorDeFichas, SinFichas>();

        services.AddScoped<CapaConversacional>();

        // -------------------------------------------- registros y su purga

        // La purga es scoped porque la cadena dueña lo es; el servicio que la
        // dispara abre un scope por vuelta en vez de capturarla para siempre.
        services.AddScoped<PurgaDeRegistros>();
        services.AddHostedService<ServicioDePurga>();

        // El barrido de borrados pendientes (asistente-rediseno-v3, design.md D4 de
        // asistente-historial-conversaciones): scoped por el mismo motivo que la purga de arriba,
        // con su propio período —mucho más corto, un minuto— porque acá la finalidad tiene que ser
        // física dentro de la ventana de «Deshacer», no dentro del día.
        services.AddScoped<BarridoDePendientes>();
        services.AddHostedService<BarridoDeBorradosPendientes>();

        // Cliente HTTP del proveedor, con el reintento de transporte ya puesto. Todavía no lo
        // consume nadie —el proveedor real llega con el carril SQL—, pero se registra acá para que
        // esa implementación lo pida por nombre y no tenga que saber nada de reintentos.
        var clienteDelProveedor = services.AddHttpClient(ClienteDelProveedor);

        // EL GRABADOR VA PRIMERO, y por lo tanto POR FUERA del reintento. En AddHttpMessageHandler
        // el orden de registración es de afuera hacia adentro, así que el pipeline queda: adaptador
        // → grabador → reintento → transporte Con ese orden el grabador ve UNA solicitud por
        // llamada lógica y la respuesta que el reintento resolvió.
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

                    // Opcional a propósito: la registra quien sabe cuál es —el evaluador—, y sin
                    // ella no se graba ni se sirve nada.
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

        // States at startup whether redaction masks sensible-valor. It reads the
        // options without validating them: see AnuncioDeLaRedaccion.
        services.AddHostedService(sp => new AnuncioDeLaRedaccion(
            configuration.GetSection(OpcionesAsistente.Seccion).Get<OpcionesAsistente>()
                ?? new OpcionesAsistente(),
            sp.GetRequiredService<ILogger<AnuncioDeLaRedaccion>>()));

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

    /// <summary>El registro de adaptadores. Un proveedor nuevo es un brazo más acá.</summary>
    private static IProveedorDeModelo ConstruirProveedor(
        IServiceProvider sp, OpcionesAsistente valores, string modelo) => valores.Proveedor switch
        {
            ProveedorSimulado.Clave => new ProveedorSimulado(),
            ProveedorAnthropic.Clave => ArmarAnthropic(sp, modelo),
            ProveedorLocal.Clave => ArmarLocal(sp, valores, modelo),
            ProveedorOllama.Clave => ArmarOllama(sp, valores, modelo),
            _ => throw new InvalidOperationException(
                $"Proveedor de modelo '{valores.Proveedor}' desconocido. Los disponibles son "
                + $"'{ProveedorSimulado.Clave}', '{ProveedorAnthropic.Clave}', '{ProveedorLocal.Clave}' "
                + $"y '{ProveedorOllama.Clave}'."),
        };

    /// <summary>Clave del proveedor que redacta, en el contenedor.</summary>
    public const string ProveedorDeRedaccion = "asistente-redaccion";

    /// <summary>Clave del adaptador crudo que genera, antes de cualquier decorador.</summary>
    public const string AdaptadorDeGeneracion = "asistente-adaptador-generacion";

    /// <summary>Clave del adaptador crudo que redacta, si es un modelo distinto.</summary>
    public const string AdaptadorDeRedaccion = "asistente-adaptador-redaccion";

    /// <summary>
    /// Interpreta los tres esfuerzos y descarta el resultado, para fallar temprano.
    /// </summary>
    private static void ValidarEsfuerzos(OpcionesAsistente valores)
    {
        EsfuerzoConfigurado.Interpretar(
            valores.EsfuerzoDeGeneracion, nameof(OpcionesAsistente.EsfuerzoDeGeneracion));
        EsfuerzoConfigurado.Interpretar(
            valores.EsfuerzoDeRedaccion, nameof(OpcionesAsistente.EsfuerzoDeRedaccion));
        EsfuerzoConfigurado.Interpretar(
            valores.EsfuerzoDeReescritura, nameof(OpcionesAsistente.EsfuerzoDeReescritura));
    }

    /// <summary>
    /// Arma el adaptador de Anthropic con el cliente HTTP que ya trae el reintento.
    /// </summary>
    private static ProveedorAnthropic ArmarAnthropic(IServiceProvider sp, string modelo) =>
        new(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(ClienteDelProveedor),
            Requerido(sp, o => o.ClaveDelProveedor, nameof(OpcionesAsistente.ClaveDelProveedor)),
            string.IsNullOrWhiteSpace(modelo)
                ? Requerido(sp, o => o.Modelo, nameof(OpcionesAsistente.Modelo))
                : modelo,
            sp.GetRequiredService<ILogger<ProveedorAnthropic>>());

    /// <summary>
    /// Arma el adaptador del servicio de inferencia compartido
    /// (asistente-proveedor-ollama, D1) con el mismo transporte que los demás.
    /// </summary>
    internal static ProveedorOllama ArmarOllama(
        IServiceProvider sp, OpcionesAsistente valores, string modelo) =>
        new(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(ClienteDelProveedor),
            sp.GetRequiredService<IOptions<OpcionesInferencia>>().Value,
            string.IsNullOrWhiteSpace(modelo)
                ? Requerido(sp, o => o.Modelo, nameof(OpcionesAsistente.Modelo))
                : modelo,
            valores.ContextoDelModelo,
            sp.GetRequiredService<ILogger<ProveedorOllama>>());

    /// <summary>
    /// Arma el adaptador del modelo propio con el cliente HTTP que ya trae el reintento
    /// (asistente-proveedor-local, D2).
    /// </summary>
    internal static ProveedorLocal ArmarLocal(
        IServiceProvider sp, OpcionesAsistente valores, string modelo) =>
        new(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(ClienteDelProveedor),
            Requerido(sp, o => o.UrlDelProveedorLocal, nameof(OpcionesAsistente.UrlDelProveedorLocal)),
            valores.ClaveDelProveedor,
            string.IsNullOrWhiteSpace(modelo)
                ? Requerido(sp, o => o.Modelo, nameof(OpcionesAsistente.Modelo))
                : modelo,
            sp.GetRequiredService<ILogger<ProveedorLocal>>());

    // En Development no se exigen secretos locales, igual que Almacenamiento. Sin
    // Host —el evaluador compone el módulo solo— no hay ambiente y se valida.
    private sealed class ValidacionOpcionesInferencia(IHostEnvironment? ambiente = null)
        : IValidateOptions<OpcionesInferencia>
    {
        public ValidateOptionsResult Validate(string? name, OpcionesInferencia opciones)
        {
            if (ambiente?.IsDevelopment() == true) return ValidateOptionsResult.Success;
            var errores = opciones.Errores().ToList();
            return errores.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errores);
        }
    }
}
