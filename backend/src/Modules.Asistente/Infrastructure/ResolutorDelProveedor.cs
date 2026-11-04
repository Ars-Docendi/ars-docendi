using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// The evaluator's wrapper of the raw adapter, applied inside the ceiling and the breaker.
/// </summary>
internal sealed record EnvoltorioDelProveedor(Func<IProveedorDeModelo, IProveedorDeModelo>? Envolver);

/// <summary>
/// Reads the stored runtime selection. A seam of its own so that the many tests that compose the
/// module without a database can say "no selection" instead of reaching for one.
/// </summary>
internal interface ILectorDeLaSeleccion
{
    /// <summary>The stored selection, or null: the module runs on server configuration.</summary>
    Task<SeleccionDeModelo?> LeerAsync(CancellationToken ct);
}

internal sealed class LectorDeLaSeleccion(RepositorioDelModelo repositorio) : ILectorDeLaSeleccion
{
    public Task<SeleccionDeModelo?> LeerAsync(CancellationToken ct) => repositorio.LeerSeleccionAsync(ct);
}

/// <summary>Which adapter a turn runs on, before any adapter is built.</summary>
internal sealed record IdentidadDelModelo(
    string Empresa,
    string Modelo,
    bool DeSeleccion,
    string NombreDeGeneracion,
    string NombreDeRedaccion)
{
    /// <summary>Own hardware: the shared inference service or the own server. No cloud company is.</summary>
    public bool EnHardwarePropio =>
        string.Equals(Empresa, ProveedorOllama.Clave, StringComparison.Ordinal)
        || string.Equals(Empresa, ProveedorLocal.Clave, StringComparison.Ordinal);

    public bool EsSimulado =>
        !DeSeleccion && string.Equals(Empresa, ProveedorSimulado.Clave, StringComparison.Ordinal);
}

/// <summary>
/// Resolves, ONCE per turn (scope), which model adapter the turn runs on
/// (asistente-modelo-en-caliente, D3), and builds the chain around it.
/// </summary>
internal sealed class ResolutorDelProveedor(
    IServiceProvider servicios,
    ILectorDeLaSeleccion lector,
    RegistroDeBreakers breakers,
    IOptions<OpcionesAsistente> opciones,
    EnvoltorioDelProveedor envoltorio,
    TimeProvider reloj,
    ILogger<ResolutorDelProveedor> log)
    : IBreakerDelTurno, IEnmascaradoDelTurno, IDisposable
{
    private readonly Lock _candado = new();
    private Task<IdentidadDelModelo>? _identidad;
    private Task<IProveedorDeModelo>? _generacion;
    private Task<IProveedorDeModelo>? _redaccion;
    private Task<IProveedorDeModelo>? _crudoDeLaSeleccion;
    private bool _dispuesto;

    /// <summary>The identity if the turn already resolved it, else null. Never reads the database.</summary>
    public IdentidadDelModelo? IdentidadSiYaResuelta =>
        _identidad is { IsCompletedSuccessfully: true } resuelta ? resuelta.Result : null;

    /// <summary>The identity the environment alone describes. Needs no database.</summary>
    public IdentidadDelModelo IdentidadDelEntorno()
    {
        var valores = opciones.Value;
        var generacion = NombreDelEntorno(valores.Proveedor, valores.Modelo);

        return new IdentidadDelModelo(
            valores.Proveedor,
            valores.Modelo,
            DeSeleccion: false,
            generacion,
            string.IsNullOrWhiteSpace(valores.ModeloDeRedaccion)
                ? generacion
                : NombreDelEntorno(valores.Proveedor, valores.ModeloDeRedaccion));
    }

    public Task<IdentidadDelModelo> IdentidadAsync(CancellationToken ct) =>
        UnaVez(ref _identidad, () => LeerIdentidadAsync(ct));

    /// <summary>The chain for generation: ceiling, optional gate, breaker, adapter.</summary>
    public Task<IProveedorDeModelo> GeneracionAsync(CancellationToken ct) =>
        UnaVez(ref _generacion, () => ArmarAsync(redaccion: false, ct));

    /// <summary>The chain for redaction. With a stored selection it is the same adapter as generation.</summary>
    public Task<IProveedorDeModelo> RedaccionAsync(CancellationToken ct) =>
        UnaVez(ref _redaccion, () => ArmarAsync(redaccion: true, ct));

    public async Task<EstadoDelBreaker> EstadoAsync(CancellationToken ct)
    {
        var identidad = await IdentidadAsync(ct);
        return breakers.Para(identidad.NombreDeGeneracion).Estado;
    }

    public async Task<bool> RedaccionSinEnmascararAsync(CancellationToken ct) =>
        opciones.Value.RedaccionSinEnmascararCon((await IdentidadAsync(ct)).EnHardwarePropio);

    public void Dispose()
    {
        if (_dispuesto)
        {
            return;
        }

        _dispuesto = true;

        // Only an adapter built for a selection belongs to this scope; the
        // environment's are singletons the container disposes.
        if (_crudoDeLaSeleccion is { IsCompletedSuccessfully: true } crudo
            && crudo.Result is IDisposable desechable)
        {
            desechable.Dispose();
        }
    }

    private async Task<IdentidadDelModelo> LeerIdentidadAsync(CancellationToken ct)
    {
        // The evaluator's wrapper (a meter that must be ONE instance for the whole run) exists only
        // for the adapter the environment describes: a stored selection would build a raw adapter
        // per turn and wrap it again each time, splitting the totals.
        if (envoltorio.Envolver is not null)
        {
            return IdentidadDelEntorno();
        }

        var seleccion = await lector.LeerAsync(ct);
        if (seleccion is null)
        {
            return IdentidadDelEntorno();
        }

        // The environment's redaction model is ignored: it may belong to another company.
        var nombre = $"{seleccion.Empresa}/{seleccion.Modelo}";
        return new IdentidadDelModelo(seleccion.Empresa, seleccion.Modelo, DeSeleccion: true, nombre, nombre);
    }

    private async Task<IProveedorDeModelo> ArmarAsync(bool redaccion, CancellationToken ct)
    {
        var identidad = await IdentidadAsync(ct);

        if (!identidad.DeSeleccion)
        {
            // Built the way registration always built it (and it throws the same way
            // when the environment is incomplete), only later: at the first call.
            IProveedorDeModelo @base = redaccion
                ? servicios.GetRequiredService<BaseDeRedaccion>().Valor
                : servicios.GetRequiredService<ProveedorBase>().Valor;

            return Encadenar(
                @base,
                redaccion ? identidad.NombreDeRedaccion : identidad.NombreDeGeneracion,
                identidad.EnHardwarePropio);
        }

        var crudo = await UnaVez(ref _crudoDeLaSeleccion, () => ConstruirCrudoAsync(identidad, ct));
        return Encadenar(crudo, identidad.NombreDeGeneracion, identidad.EnHardwarePropio);
    }

    /// <summary>
    /// The raw adapter of a stored selection. A key that cannot be opened is not an
    /// exception that escapes the turn: it becomes an adapter that fails every call
    /// as a provider failure, so the turn degrades and the breaker records it.
    /// </summary>
    private async Task<IProveedorDeModelo> ConstruirCrudoAsync(IdentidadDelModelo identidad, CancellationToken ct)
    {
        var valores = opciones.Value;
        IProveedorDeModelo crudo;

        try
        {
            crudo = identidad.Empresa switch
            {
                ProveedorOllama.Clave => ModuleExtensions.ArmarOllama(servicios, valores, identidad.Modelo),
                ProveedorLocal.Clave => ModuleExtensions.ArmarLocal(servicios, valores, identidad.Modelo),
                _ when ClientesDeChat.Empresas.Contains(identidad.Empresa) =>
                    await ArmarEnLaNubeAsync(identidad, ct),
                _ => throw new InvalidOperationException(
                    $"La empresa '{identidad.Empresa}' guardada como selección no es una que el módulo sepa armar."),
            };
        }
        catch (InvalidOperationException excepcion)
        {
            // ClaveMaestraNoDisponibleException, CredencialIlegibleException and the "not
            // configured" errors of the local builders are all this type.
            log.LogError(
                "No se pudo armar el modelo {Nombre} guardado como selección ({Causa}). Los turnos van a "
                + "responder degradados hasta que se corrija la clave o la configuración del servidor, "
                + "o se vuelva a la configuración del servidor.",
                identidad.NombreDeGeneracion,
                excepcion.GetType().Name);

            crudo = new ProveedorNoArmable(identidad.NombreDeGeneracion);
        }

        return crudo;
    }

    private async Task<IProveedorDeModelo> ArmarEnLaNubeAsync(IdentidadDelModelo identidad, CancellationToken ct)
    {
        var credencial = await servicios.GetRequiredService<RepositorioDeCredenciales>()
                .AbrirAsync(identidad.Empresa, ct)
            ?? throw new InvalidOperationException(
                $"No hay clave guardada para '{identidad.Empresa}'.");

        var cliente = ClientesDeChat.Crear(
            identidad.Empresa,
            identidad.Modelo,
            credencial.Clave,
            servicios.GetRequiredService<IHttpClientFactory>().CreateClient(ModuleExtensions.ClienteDelProveedor));

        return new ProveedorChat(
            identidad.Empresa,
            identidad.Modelo,
            cliente.Cliente,
            cliente.Constructor,
            servicios.GetRequiredService<ILogger<ProveedorChat>>(),
            cliente.Dueno);
    }

    /// <summary>
    /// Wraps an adapter with the ceiling, the gate and the breaker, outermost first: ceiling, gate
    /// (own hardware only), breaker and timeout, adapter.
    /// </summary>
    private IProveedorDeModelo Encadenar(IProveedorDeModelo interno, string nombre, bool conCompuerta)
    {
        var valores = opciones.Value;
        var contador = servicios.GetRequiredService<ContadorDeLlamadasDelTurno>();

        IProveedorDeModelo conCorte = new ProveedorConBreaker(
            interno,
            breakers.Para(nombre),
            TimeSpan.FromSeconds(valores.TimeoutDeLlamadaSegundos),
            reloj);

        if (conCompuerta && valores.MaximoDeLlamadasConcurrentes > 0)
        {
            conCorte = new ProveedorConCompuerta(
                conCorte,
                servicios.GetRequiredService<CompuertaDelModelo>(),
                TimeSpan.FromSeconds(valores.EsperaMaximaEnColaSegundos),
                contador);
        }

        return new ProveedorConTechoDeLlamadas(conCorte, contador);
    }

    /// <summary>
    /// The name every adapter writes: <c>"{clave}/{modelo}"</c>, and <c>"simulado"</c> alone.
    /// </summary>
    private static string NombreDelEntorno(string proveedor, string modelo) =>
        string.Equals(proveedor, ProveedorSimulado.Clave, StringComparison.Ordinal)
            ? ProveedorSimulado.Clave
            : $"{proveedor}/{modelo}";

    /// <summary>
    /// Runs <paramref name="crear"/> once and hands the same task to every caller of the scope.
    /// </summary>
    private Task<T> UnaVez<T>(ref Task<T>? campo, Func<Task<T>> crear)
    {
        lock (_candado)
        {
            if (campo is null or { IsFaulted: true } or { IsCanceled: true })
            {
                campo = crear();
            }

            return campo;
        }
    }
}

/// <summary>
/// The adapter of a stored selection that could not be built. It fails every call as
/// a provider failure (<see cref="HttpRequestException"/>), which is what the breaker
/// counts and the lane degrades on.
/// </summary>
internal sealed class ProveedorNoArmable(string nombre) : IProveedorDeModelo
{
    public string Nombre => nombre;

    public bool EsSimulado => false;

    public Task<RespuestaDelModelo> CompletarAsync(SolicitudAlModelo solicitud, CancellationToken ct) =>
        throw new HttpRequestException("El modelo guardado como selección no se pudo armar.");
}

/// <summary>
/// The model port of a turn. It carries no adapter: it asks the scope's resolver the
/// first time a call goes out, so registration reads nothing and a turn that never
/// calls the model never builds one.
/// </summary>
internal sealed class ProveedorDelTurno(
    ResolutorDelProveedor resolutor, bool paraRedactar) : IProveedorDeModelo
{
    /// <summary>
    /// The adapter name for the registry of the turn. If the turn has not resolved
    /// yet it describes the environment, and never reads the database or blocks: the
    /// name is read in a <c>finally</c> that must not fail and mask the real error.
    /// </summary>
    public string Nombre
    {
        get
        {
            var identidad = resolutor.IdentidadSiYaResuelta ?? resolutor.IdentidadDelEntorno();
            return paraRedactar ? identidad.NombreDeRedaccion : identidad.NombreDeGeneracion;
        }
    }

    public bool EsSimulado => (resolutor.IdentidadSiYaResuelta ?? resolutor.IdentidadDelEntorno()).EsSimulado;

    public async Task<RespuestaDelModelo> CompletarAsync(SolicitudAlModelo solicitud, CancellationToken ct)
    {
        var cadena = await (paraRedactar ? resolutor.RedaccionAsync(ct) : resolutor.GeneracionAsync(ct));
        return await cadena.CompletarAsync(solicitud, ct);
    }
}
