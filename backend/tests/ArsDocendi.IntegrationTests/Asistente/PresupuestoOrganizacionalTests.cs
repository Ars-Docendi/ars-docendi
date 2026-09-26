using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El tope organizacional de gasto mensual, persistente en Postgres
/// (asistente-presupuesto-persistente, design.md D3/D4/D6 de
/// asistente-administracion-de-uso).
/// </summary>
public sealed class PresupuestoOrganizacionalTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_tope_organizacional")
{
    private static readonly DateTimeOffset Ancla = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Bloquea_al_alcanzar_el_tope_y_desbloquea_el_mes_siguiente()
    {
        await FijarTopeAsync(1.00m);
        await FijarPrecioAsync("anthropic", "claude-sonnet-5", precioPorTokenEntrada: 0.001m);

        var reloj = new RelojFijo(Ancla);
        var presupuesto = new PresupuestoOrganizacionalPersistente(new CadenaDuena(Cadena), reloj, new DetectorDeUmbrales(), NullLogger<PresupuestoOrganizacionalPersistente>.Instance);
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await presupuesto.HayPresupuestoAsync(ct));

        // 1000 tokens de entrada * 0.001 = 1.00 USD: exactamente el tope.
        await presupuesto.AcumularAsync(
            "anthropic/claude-sonnet-5", reloj.GetUtcNow(), 1000, 0, null, ct);

        Assert.False(await presupuesto.HayPresupuestoAsync(ct));

        reloj.Avanzar(TimeSpan.FromDays(32));
        Assert.True(await presupuesto.HayPresupuestoAsync(ct));
    }

    [Fact]
    public async Task Un_tope_en_cero_nunca_bloquea()
    {
        // Sin FijarTopeAsync: el seed de 006 deja el tope en cero.
        var reloj = new RelojFijo(Ancla);
        var presupuesto = new PresupuestoOrganizacionalPersistente(new CadenaDuena(Cadena), reloj, new DetectorDeUmbrales(), NullLogger<PresupuestoOrganizacionalPersistente>.Instance);

        Assert.True(await presupuesto.HayPresupuestoAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Una_fila_sin_precio_no_acumula_nada()
    {
        await FijarTopeAsync(0.01m);

        var reloj = new RelojFijo(Ancla);
        var presupuesto = new PresupuestoOrganizacionalPersistente(new CadenaDuena(Cadena), reloj, new DetectorDeUmbrales(), NullLogger<PresupuestoOrganizacionalPersistente>.Instance);
        var ct = TestContext.Current.CancellationToken;

        await presupuesto.AcumularAsync(
            "local/sin-precio", reloj.GetUtcNow(), 1_000_000, 1_000_000, null, ct);

        Assert.True(await presupuesto.HayPresupuestoAsync(ct));
    }

    // ------------------------------------ el veredicto en CapaConversacional

    [Fact]
    public async Task Un_actor_bajo_su_propio_cupo_queda_bloqueado_igual_por_el_tope()
    {
        // design.md D3/D4: el tope organizacional bloquea a TODOS, incluso a
        // un actor que todavía tiene cupo propio disponible.
        await SembrarAsync();
        var (basica, pii) = CadenasDeLectura();
        var actor = Guid.Parse("a0000000-0000-4000-8000-000000000004");

        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente(),
            cupoDiario: 100,
            topeOrganizacional: 1,
            guion: [ProveedorGuionado.Generacion("SELECT count(*) FROM designaciones.designaciones"), "Hay 4."]);

        // Un primer turno agota el tope falso (incrementa en 1 por llamada).
        await banco.Capa().ResponderAsync(
            actor, null, "¿cuántos docentes hay?", TestContext.Current.CancellationToken);

        var segundo = await banco.Capa().ResponderAsync(
            actor, null, "¿cuántos pedidos hay?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, segundo.Estado);
        Assert.Equal(PoliticaDeAbstencion.TextoTopeOrganizacionalAgotado, segundo.Respuesta);

        // El texto no menciona ningún número: ni costo ni tope.
        Assert.DoesNotMatch(@"\d", segundo.Respuesta);

        // El actor SEGUÍA con cupo propio: no fue su cuota lo que lo bloqueó.
        Assert.True(await banco.Cuota.HayCupoAsync(actor, TestContext.Current.CancellationToken));
    }

    private async Task FijarTopeAsync(decimal tope)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "INSERT INTO asistente.tope_organizacional (tope_mensual_usd, vigente_desde) VALUES (@tope, now())",
            conexion);
        comando.Parameters.AddWithValue("tope", tope);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task FijarPrecioAsync(
        string proveedor, string modelo, decimal precioPorTokenEntrada)
    {
        // El precio tiene que regir a la vez "ahora" (el acumulador lo busca con
        // now() del motor) y en el `ocurridoEn` del reloj fijo (la calculadora lo
        // cruza contra esa fecha). Con `now() - 1 día` el test caducaba solo un
        // día después de `Ancla`: el precio pasaba a empezar después del turno.
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.tabla_de_precios
                (proveedor, modelo, precio_por_token_entrada, precio_por_token_salida,
                 precio_por_token_cache, version, vigente_desde)
            VALUES (@proveedor, @modelo, @precio, 0, 0, 1, LEAST(now(), @ancla) - interval '1 day')
            """, conexion);
        comando.Parameters.AddWithValue("ancla", Ancla);
        comando.Parameters.AddWithValue("proveedor", proveedor);
        comando.Parameters.AddWithValue("modelo", modelo);
        comando.Parameters.AddWithValue("precio", precioPorTokenEntrada);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
