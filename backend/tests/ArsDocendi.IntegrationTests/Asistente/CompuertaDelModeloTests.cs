using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// La compuerta de concurrencia hacia el modelo (asistente-proveedor-local,
/// design.md D4): cuántas llamadas en curso, quién entra primero, y que una
/// cola larga nunca se confunda con un proveedor caído.
/// </summary>
public sealed class CompuertaDelModeloTests
{
    private static readonly TimeSpan Larga = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Corta = TimeSpan.FromMilliseconds(80);

    private static readonly SolicitudAlModelo Solicitud = new()
    {
        PrefijoEstable = "Esquema.",
        Mensaje = "¿Cuántos pedidos hay?",
        Temperatura = 0.0m,
        Esfuerzo = EsfuerzoDelModelo.Medio,
        MaximoDeTokens = 100,
    };

    [Fact]
    public async Task No_deja_pasar_mas_llamadas_que_su_capacidad()
    {
        var compuerta = new CompuertaDelModelo(2, TimeProvider.System);

        Assert.True(await compuerta.EntrarAsync(false, Larga, Ct));
        Assert.True(await compuerta.EntrarAsync(false, Larga, Ct));

        var tercera = compuerta.EntrarAsync(false, Larga, Ct);

        Assert.False(tercera.IsCompleted);
        Assert.Equal(2, compuerta.EnCurso);
        Assert.Equal(1, compuerta.EnEspera);

        compuerta.Salir();

        // El lugar pasa de mano: sigue habiendo dos en curso.
        Assert.True(await tercera);
        Assert.Equal(2, compuerta.EnCurso);
        Assert.Equal(0, compuerta.EnEspera);
    }

    [Fact]
    public async Task Un_turno_ya_empezado_entra_antes_que_uno_nuevo()
    {
        var compuerta = new CompuertaDelModelo(1, TimeProvider.System);
        Assert.True(await compuerta.EntrarAsync(false, Larga, Ct));

        // Llega primero la nueva y después la prioritaria.
        var nueva = compuerta.EntrarAsync(prioritaria: false, Larga, Ct);
        var empezada = compuerta.EntrarAsync(prioritaria: true, Larga, Ct);

        compuerta.Salir();

        Assert.True(await empezada);
        Assert.False(nueva.IsCompleted);

        compuerta.Salir();
        Assert.True(await nueva);
    }

    [Fact]
    public async Task Vencida_la_espera_devuelve_falso_y_sale_de_la_cola()
    {
        var compuerta = new CompuertaDelModelo(1, TimeProvider.System);
        Assert.True(await compuerta.EntrarAsync(false, Larga, Ct));

        Assert.False(await compuerta.EntrarAsync(false, Corta, Ct));

        Assert.Equal(0, compuerta.EnEspera);

        // Y no se llevó ningún lugar: al salir el primero, la compuerta queda vacía.
        compuerta.Salir();
        Assert.Equal(0, compuerta.EnCurso);
    }

    [Fact]
    public async Task Una_cancelacion_del_request_se_propaga_y_no_ocupa_lugar()
    {
        var compuerta = new CompuertaDelModelo(1, TimeProvider.System);
        Assert.True(await compuerta.EntrarAsync(false, Larga, Ct));

        using var cancelacion = new CancellationTokenSource();
        var esperando = compuerta.EntrarAsync(false, Larga, cancelacion.Token);
        await cancelacion.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => esperando);
        Assert.Equal(0, compuerta.EnEspera);

        compuerta.Salir();
        Assert.Equal(0, compuerta.EnCurso);
    }

    [Fact]
    public async Task La_saturacion_no_abre_el_breaker()
    {
        // EL PUNTO DE LA COMPUERTA. Un breaker que abre al primer fallo, y una
        // llamada que ocupa el único lugar sin terminar: las que esperan vencen en
        // la cola, se informan como saturación, y el breaker no se entera — una GPU
        // ocupada no es una GPU caída.
        var opciones = Options.Create(new OpcionesAsistente { FallosParaAbrirElBreaker = 1 });
        var breaker = new BreakerDelProveedor(
            opciones, TimeProvider.System, NullLogger<BreakerDelProveedor>.Instance);
        var compuerta = new CompuertaDelModelo(1, TimeProvider.System);
        var lenta = new ProveedorLento();

        IProveedorDeModelo Cadena() => new ProveedorConCompuerta(
            new ProveedorConBreaker(lenta, breaker, Larga, TimeProvider.System),
            compuerta,
            Corta,
            new ContadorDeLlamadasDelTurno(4));

        var enCurso = Cadena().CompletarAsync(Solicitud, Ct);

        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<ProveedorSaturado>(() => Cadena().CompletarAsync(Solicitud, Ct));
        }

        Assert.Equal(EstadoDelBreaker.Cerrado, breaker.Estado);

        lenta.Liberar();
        await enCurso;

        // Terminada la llamada, el lugar volvió.
        Assert.Equal(0, compuerta.EnCurso);
    }

    [Fact]
    public async Task La_espera_en_cola_no_consume_el_timeout_de_la_llamada()
    {
        // El timeout de la segunda llamada es más corto que lo que espera en la
        // cola. Si la cola estuviera adentro del timeout, vencería como
        // TimeoutDelProveedor; como está afuera, completa.
        var opciones = Options.Create(new OpcionesAsistente());
        var breaker = new BreakerDelProveedor(
            opciones, TimeProvider.System, NullLogger<BreakerDelProveedor>.Instance);
        var compuerta = new CompuertaDelModelo(1, TimeProvider.System);
        var lenta = new ProveedorLento();
        var timeoutCorto = TimeSpan.FromMilliseconds(300);

        IProveedorDeModelo Cadena(IProveedorDeModelo interno, TimeSpan timeout) => new ProveedorConCompuerta(
            new ProveedorConBreaker(interno, breaker, timeout, TimeProvider.System),
            compuerta,
            Larga,
            new ContadorDeLlamadasDelTurno(4));

        var primera = Cadena(lenta, Larga).CompletarAsync(Solicitud, Ct);
        var segunda = Cadena(new ProveedorInmediato(), timeoutCorto).CompletarAsync(Solicitud, Ct);

        // La segunda espera en la cola MÁS que su propio timeout, y recién después
        // llama: si la espera contara, vencería como TimeoutDelProveedor.
        await Task.Delay(TimeSpan.FromMilliseconds(600), Ct);
        lenta.Liberar();

        await primera;
        var respuesta = await segunda;

        Assert.Equal("ok", respuesta.Texto);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Un proveedor que no contesta hasta que se lo libera.</summary>
    private sealed class ProveedorLento : IProveedorDeModelo
    {
        private readonly TaskCompletionSource _liberado =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Nombre => "lento";

        public bool EsSimulado => true;

        public void Liberar() => _liberado.TrySetResult();

        public async Task<RespuestaDelModelo> CompletarAsync(SolicitudAlModelo solicitud, CancellationToken ct)
        {
            await _liberado.Task.WaitAsync(ct);
            return new RespuestaDelModelo("ok", 1, 1, EsSimulada: true);
        }
    }

    private sealed class ProveedorInmediato : IProveedorDeModelo
    {
        public string Nombre => "inmediato";

        public bool EsSimulado => true;

        public Task<RespuestaDelModelo> CompletarAsync(SolicitudAlModelo solicitud, CancellationToken ct) =>
            Task.FromResult(new RespuestaDelModelo("ok", 1, 1, EsSimulada: true));
    }
}
