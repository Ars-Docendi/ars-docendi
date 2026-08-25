using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la cuota por actor y el circuit breaker, en memoria.
/// </summary>
/// <remarks>
/// Las dos piezas son puras salvo por el reloj, así que se prueban sin base y sin
/// proveedor. Que no necesiten infraestructura es justamente lo que permite
/// probarlas de verdad: un test de cuota que dependiera de una base tendería a
/// probar la base.
/// </remarks>
public sealed class CuotaYBreakerTests
{
    private static readonly DateTimeOffset Ancla = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Alguien = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid Otro = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    // ------------------------------------------------------------------ cuota

    [Fact]
    public void El_cupo_se_agota_al_llegar_al_limite()
    {
        var (cuota, _) = Cuota(cupo: 6);

        cuota.Anotar(Alguien, 3);
        Assert.True(cuota.HayCupo(Alguien));

        cuota.Anotar(Alguien, 3);
        Assert.False(cuota.HayCupo(Alguien));
    }

    [Fact]
    public void Dos_actores_no_comparten_cupo()
    {
        var (cuota, _) = Cuota(cupo: 3);

        cuota.Anotar(Alguien, 3);

        Assert.False(cuota.HayCupo(Alguien));
        Assert.True(cuota.HayCupo(Otro));
    }

    [Fact]
    public void Al_pasar_la_ventana_vuelve_el_cupo()
    {
        var (cuota, reloj) = Cuota(cupo: 3, ventanaMinutos: 60);

        cuota.Anotar(Alguien, 3);
        Assert.False(cuota.HayCupo(Alguien));

        reloj.Avanzar(TimeSpan.FromMinutes(61));

        Assert.True(cuota.HayCupo(Alguien));
    }

    [Fact]
    public void La_ventana_es_deslizante_y_no_un_balde_que_se_vacia_de_golpe()
    {
        // Con una ventana fija por bloques, lo consumido a las 10:59 se perdonaría
        // entero a las 11:00 y un actor podría gastar dos cupos en dos minutos.
        var (cuota, reloj) = Cuota(cupo: 4, ventanaMinutos: 60);

        cuota.Anotar(Alguien, 2);
        reloj.Avanzar(TimeSpan.FromMinutes(30));
        cuota.Anotar(Alguien, 2);

        Assert.False(cuota.HayCupo(Alguien));

        // A los 61 minutos del primero, sale el primero y no el segundo.
        reloj.Avanzar(TimeSpan.FromMinutes(31));

        Assert.True(cuota.HayCupo(Alguien));
        cuota.Anotar(Alguien, 2);
        Assert.False(cuota.HayCupo(Alguien));
    }

    [Fact]
    public void Un_cupo_en_cero_desactiva_la_cuota()
    {
        var (cuota, _) = Cuota(cupo: 0);

        cuota.Anotar(Alguien, 10_000);

        Assert.True(cuota.HayCupo(Alguien));
        Assert.Null(cuota.CupoVuelveA(Alguien));
    }

    [Fact]
    public void Con_cupo_disponible_no_hay_hora_de_vuelta()
    {
        var (cuota, _) = Cuota(cupo: 6);

        cuota.Anotar(Alguien, 3);

        Assert.Null(cuota.CupoVuelveA(Alguien));
    }

    [Fact]
    public void Sin_cupo_la_hora_de_vuelta_es_la_del_consumo_mas_viejo()
    {
        var (cuota, reloj) = Cuota(cupo: 4, ventanaMinutos: 60);

        cuota.Anotar(Alguien, 2);
        reloj.Avanzar(TimeSpan.FromMinutes(20));
        cuota.Anotar(Alguien, 2);

        // El primero salió de la ventana a los 60 minutos de haberse hecho, no a
        // los 60 del último: decirle al usuario la hora del último lo mandaría a
        // esperar de más.
        Assert.Equal(Ancla + TimeSpan.FromMinutes(60), cuota.CupoVuelveA(Alguien));
    }

    [Fact]
    public void Anotar_cero_llamadas_no_consume_nada()
    {
        // Es el caso del turno degradado: corrió, no llamó a nadie, no paga.
        var (cuota, _) = Cuota(cupo: 1);

        cuota.Anotar(Alguien, 0);

        Assert.True(cuota.HayCupo(Alguien));
    }

    // ---------------------------------------------------------------- breaker

    [Fact]
    public void El_breaker_abre_al_llegar_al_umbral()
    {
        var (breaker, _) = Breaker(umbral: 3);

        breaker.Fallo();
        breaker.Fallo();
        Assert.Equal(EstadoDelBreaker.Cerrado, breaker.Estado);

        breaker.Fallo();
        Assert.Equal(EstadoDelBreaker.Abierto, breaker.Estado);
        Assert.False(breaker.Permite());
    }

    [Fact]
    public void Un_exito_reinicia_la_cuenta_de_fallos()
    {
        // «Fallos seguidos» y no «fallos en total»: un proveedor que falla una vez
        // por semana no tiene por qué terminar cortado seis meses después.
        var (breaker, _) = Breaker(umbral: 3);

        breaker.Fallo();
        breaker.Fallo();
        breaker.Exito();
        breaker.Fallo();
        breaker.Fallo();

        Assert.Equal(EstadoDelBreaker.Cerrado, breaker.Estado);
    }

    [Fact]
    public void Tras_la_espera_pasa_una_sola_llamada_de_prueba()
    {
        // ÉSTA ES LA PROPIEDAD QUE IMPORTA. «Una por turno» no alcanza: con varios
        // turnos a la vez, sería una avalancha contra un proveedor que recién se
        // está levantando, que es justo lo que el breaker existe para evitar.
        var (breaker, reloj) = Breaker(umbral: 2, esperaSegundos: 30);

        breaker.Fallo();
        breaker.Fallo();
        reloj.Avanzar(TimeSpan.FromSeconds(31));

        Assert.Equal(EstadoDelBreaker.EnPrueba, breaker.Estado);
        Assert.True(breaker.Permite());
        Assert.False(breaker.Permite());
        Assert.False(breaker.Permite());
    }

    [Fact]
    public void La_prueba_exitosa_cierra_el_breaker()
    {
        var (breaker, reloj) = Breaker(umbral: 2, esperaSegundos: 30);

        breaker.Fallo();
        breaker.Fallo();
        reloj.Avanzar(TimeSpan.FromSeconds(31));
        breaker.Permite();
        breaker.Exito();

        Assert.Equal(EstadoDelBreaker.Cerrado, breaker.Estado);
        Assert.True(breaker.Permite());
        Assert.True(breaker.Permite());
    }

    [Fact]
    public void La_prueba_fallida_reabre_y_reinicia_la_espera()
    {
        var (breaker, reloj) = Breaker(umbral: 2, esperaSegundos: 30);

        breaker.Fallo();
        breaker.Fallo();
        reloj.Avanzar(TimeSpan.FromSeconds(31));
        breaker.Permite();
        breaker.Fallo();

        Assert.Equal(EstadoDelBreaker.Abierto, breaker.Estado);

        // Y la espera arranca de nuevo desde acá, no desde la apertura original.
        reloj.Avanzar(TimeSpan.FromSeconds(29));
        Assert.Equal(EstadoDelBreaker.Abierto, breaker.Estado);

        reloj.Avanzar(TimeSpan.FromSeconds(2));
        Assert.Equal(EstadoDelBreaker.EnPrueba, breaker.Estado);
    }

    [Fact]
    public void Un_umbral_en_cero_desactiva_el_breaker()
    {
        var (breaker, _) = Breaker(umbral: 0);

        for (var i = 0; i < 50; i++)
        {
            breaker.Fallo();
        }

        Assert.Equal(EstadoDelBreaker.Cerrado, breaker.Estado);
        Assert.True(breaker.Permite());
    }

    // --------------------------------------------- el decorador del proveedor

    [Fact]
    public async Task Un_rechazo_semantico_no_abre_el_breaker()
    {
        // El breaker cuenta transporte y timeout. Un modelo que devuelve una
        // respuesta que el validador descarta está SANO: cortarle las llamadas por
        // eso apagaría el asistente cada vez que alguien pregunta algo difícil.
        var (breaker, reloj) = Breaker(umbral: 2);
        var proveedor = new ProveedorGuionado("no soy JSON", "tampoco", "ni yo");
        var conBreaker = new ProveedorConBreaker(
            proveedor, breaker, TimeSpan.FromSeconds(20), reloj);

        for (var i = 0; i < 3; i++)
        {
            await conBreaker.CompletarAsync(Solicitud(), TestContext.Current.CancellationToken);
        }

        Assert.Equal(EstadoDelBreaker.Cerrado, breaker.Estado);
    }

    [Fact]
    public async Task Un_fallo_de_transporte_si_lo_abre()
    {
        var (breaker, reloj) = Breaker(umbral: 2);
        var proveedor = new ProveedorGuionado { Falla = new HttpRequestException("sin red") };
        var conBreaker = new ProveedorConBreaker(
            proveedor, breaker, TimeSpan.FromSeconds(20), reloj);

        for (var i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                conBreaker.CompletarAsync(Solicitud(), TestContext.Current.CancellationToken));
        }

        Assert.Equal(EstadoDelBreaker.Abierto, breaker.Estado);
    }

    [Fact]
    public async Task Con_el_breaker_abierto_el_proveedor_no_recibe_nada()
    {
        var (breaker, reloj) = Breaker(umbral: 1);
        var proveedor = new ProveedorGuionado { Falla = new HttpRequestException("sin red") };
        var conBreaker = new ProveedorConBreaker(
            proveedor, breaker, TimeSpan.FromSeconds(20), reloj);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            conBreaker.CompletarAsync(Solicitud(), TestContext.Current.CancellationToken));

        var antes = proveedor.Llamadas;

        await Assert.ThrowsAsync<ProveedorNoDisponible>(() =>
            conBreaker.CompletarAsync(Solicitud(), TestContext.Current.CancellationToken));

        Assert.Equal(antes, proveedor.Llamadas);
    }

    [Fact]
    public async Task Una_llamada_que_no_responde_a_tiempo_se_corta_y_cuenta_como_fallo()
    {
        var (breaker, reloj) = Breaker(umbral: 1);
        var proveedor = new ProveedorQueTarda(reloj, TimeSpan.FromSeconds(25));
        var conBreaker = new ProveedorConBreaker(
            proveedor, breaker, TimeSpan.FromSeconds(20), reloj);

        var falla = await Assert.ThrowsAsync<TimeoutDelProveedor>(() =>
            conBreaker.CompletarAsync(Solicitud(), TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.FromSeconds(20), falla.Cuanto);
        Assert.Equal(EstadoDelBreaker.Abierto, breaker.Estado);
    }

    [Fact]
    public async Task Los_reintentos_de_transporte_no_suman_al_conteo_de_llamadas()
    {
        // El reintento ocurre DENTRO de una llamada al modelo y tiene su propia
        // cota. Contarlo acá haría que un turno con red inestable agotara el techo
        // sin haber pedido nada de más.
        var (breaker, reloj) = Breaker(umbral: 5);
        var proveedor = new ProveedorConReintentoAdentro(intentosPorLlamada: 3);
        var contador = new ContadorDeLlamadasDelTurno(techo: 4);

        var conTecho = new ProveedorConTechoDeLlamadas(
            new ProveedorConBreaker(proveedor, breaker, TimeSpan.FromSeconds(20), reloj),
            contador);

        await conTecho.CompletarAsync(Solicitud(), TestContext.Current.CancellationToken);

        Assert.Equal(3, proveedor.IntentosDeTransporte);
        Assert.Equal(1, contador.Llamadas);
    }

    // ------------------------------------------------------------------ apoyo

    private static SolicitudAlModelo Solicitud() => new()
    {
        PrefijoEstable = "prefijo",
        Mensaje = "mensaje",
        Temperatura = 0m,
        MaximoDeTokens = 100,
    };

    private static (ICuotaDelActor Cuota, RelojFijo Reloj) Cuota(
        int cupo, int ventanaMinutos = 60)
    {
        var reloj = new RelojFijo(Ancla);

        var opciones = Options.Create(new OpcionesAsistente
        {
            CupoDeLlamadasPorActor = cupo,
            VentanaDeCuotaMinutos = ventanaMinutos,
        });

        return (new CuotaEnMemoria(opciones, reloj), reloj);
    }

    private static (BreakerDelProveedor Breaker, RelojFijo Reloj) Breaker(
        int umbral, int esperaSegundos = 30)
    {
        var reloj = new RelojFijo(Ancla);

        var opciones = Options.Create(new OpcionesAsistente
        {
            FallosParaAbrirElBreaker = umbral,
            EsperaDelBreakerSegundos = esperaSegundos,
        });

        return (new BreakerDelProveedor(
            opciones, reloj, NullLogger<BreakerDelProveedor>.Instance), reloj);
    }

    /// <summary>Un proveedor que consume tiempo del reloj antes de contestar.</summary>
    private sealed class ProveedorQueTarda(RelojFijo reloj, TimeSpan cuanto) : IProveedorDeModelo
    {
        public string Nombre => "lento";

        public bool EsSimulado => true;

        public Task<RespuestaDelModelo> CompletarAsync(
            SolicitudAlModelo solicitud, CancellationToken ct)
        {
            reloj.Avanzar(cuanto);
            ct.ThrowIfCancellationRequested();

            return Task.FromResult(new RespuestaDelModelo("{}", 10, 5, EsSimulada: true));
        }
    }

    /// <summary>
    /// Un proveedor que reintenta el transporte por su cuenta, como hace el real.
    /// </summary>
    private sealed class ProveedorConReintentoAdentro(int intentosPorLlamada) : IProveedorDeModelo
    {
        public string Nombre => "con-reintento";

        public bool EsSimulado => true;

        /// <summary>Intentos de red acumulados, incluyendo los que fallaron.</summary>
        public int IntentosDeTransporte { get; private set; }

        public Task<RespuestaDelModelo> CompletarAsync(
            SolicitudAlModelo solicitud, CancellationToken ct)
        {
            IntentosDeTransporte += intentosPorLlamada;

            return Task.FromResult(new RespuestaDelModelo("{}", 10, 5, EsSimulada: true));
        }
    }
}
