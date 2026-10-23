using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El advisory lock de sesión de <see cref="CandadoDelTurno"/>
/// (asistente-turno-exclusivo-del-actor, design.md D5 de
/// asistente-administracion-de-uso).
/// </summary>
public sealed class CandadoDelTurnoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_candado_turno")
{
    private static readonly Guid ActorA = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid ActorB = Guid.Parse("a0000000-0000-4000-8000-000000000003");

    [Fact]
    public async Task La_segunda_toma_del_mismo_actor_falla_mientras_la_primera_este_tomada()
    {
        var ct = TestContext.Current.CancellationToken;
        var cadena = new CadenaDuena(Cadena);

        var primero = await CandadoDelTurno.IntentarAsync(cadena, ActorA, ct);
        Assert.NotNull(primero);

        var segundo = await CandadoDelTurno.IntentarAsync(cadena, ActorA, ct);
        Assert.Null(segundo);

        await primero!.DisposeAsync();

        var tercero = await CandadoDelTurno.IntentarAsync(cadena, ActorA, ct);
        Assert.NotNull(tercero);
        await tercero!.DisposeAsync();
    }

    [Fact]
    public async Task Dos_actores_distintos_nunca_se_bloquean_entre_si()
    {
        var ct = TestContext.Current.CancellationToken;
        var cadena = new CadenaDuena(Cadena);

        var deA = await CandadoDelTurno.IntentarAsync(cadena, ActorA, ct);
        var deB = await CandadoDelTurno.IntentarAsync(cadena, ActorB, ct);

        Assert.NotNull(deA);
        Assert.NotNull(deB);

        await deA!.DisposeAsync();
        await deB!.DisposeAsync();
    }

    [Fact]
    public async Task Liberar_dos_veces_no_falla()
    {
        var ct = TestContext.Current.CancellationToken;
        var cadena = new CadenaDuena(Cadena);

        var candado = await CandadoDelTurno.IntentarAsync(cadena, ActorA, ct);
        Assert.NotNull(candado);

        await candado!.DisposeAsync();
        await candado.DisposeAsync();
    }
}
