using ArsDocendi.Evaluacion.Nucleo.Fixture;
using ArsDocendi.Evaluacion.Nucleo.Runner;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>Todo cassette versionado está sellado con un prefijo que sigue vigente.</summary>
public sealed class PrefijoDeLosCassettesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_prefijo_cassette")
{
    [Fact]
    public async Task Todo_cassette_versionado_declara_una_huella_de_prefijo_vigente()
    {
        var vigentes = await HuellasVigentesAsync();
        var cassettes = new AlmacenDeCassettes(RaizRepositorio.Cassettes()).Todos();

        Assert.NotEmpty(cassettes);

        var ajenos = cassettes
            .Where(cassette => !vigentes.Contains(cassette.Sello.HashDelPrefijo))
            .Select(cassette => $"{cassette.Archivo} ({cassette.Sello.HashDelPrefijo})")
            .ToList();

        Assert.True(
            ajenos.Count == 0,
            $"Hay cassettes sellados con un prefijo que ya no existe. Las {vigentes.Count} "
            + "huellas vigentes son las de los dos prefijos de esquema, las de las "
            + "instrucciones del redactor y del reescritor, y la del preflight del "
            + "evaluador. Tocar cualquiera de esos "
            + "textos obliga a regrabar: un cassette con prefijo ajeno no se sirve, y la "
            + "suite queda verde sobre un corpus que ya no reproduce nada. "
            + "Detectado en: " + string.Join(", ", ajenos));
    }

    [Fact]
    public async Task El_guard_reconoce_una_huella_ajena()
    {
        // EL PAR SINTÉTICO. Sin él, un cambio que hiciera devolver el conjunto vacío
        // —o uno que devolviera todas las huellas posibles— pasaría en verde para
        // siempre afirmando que no hay cassettes ajenos.
        var vigentes = await HuellasVigentesAsync();

        Assert.Equal(5, vigentes.Count);
        Assert.DoesNotContain(ClaveDeCassette.HuellaDe("un prefijo que nadie usó"), vigentes);
    }

    /// <summary>Las cinco huellas de <c>system</c> que se pueden mandar hoy.</summary>
    private async Task<IReadOnlySet<string>> HuellasVigentesAsync()
    {
        await AplicarFixtureAsync();

        var (basica, conDatosPersonales) = CadenasDeLectura();
        var proveedor = new ProveedorDeEsquema(Apertura);
        var ct = TestContext.Current.CancellationToken;

        var deEsquema = new[]
        {
            await proveedor.ObtenerAsync(conDatosPersonales: false, ct),
            await proveedor.ObtenerAsync(conDatosPersonales: true, ct),
        };

        return deEsquema
            .Select(esquema => ClaveDeCassette.HuellaDe(esquema.Prefijo))
            .Concat([
                ClaveDeCassette.HuellaDe(RedactorDeRespuesta.Instrucciones),
                ClaveDeCassette.HuellaDe(ReescritorDePreguntas.Instrucciones),
                ClaveDeCassette.HuellaDe(Preflight.PrefijoDePrueba),
            ])
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task AplicarFixtureAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(new GeneradorDeFixture().Generar(), conexion)
        { CommandTimeout = 60 };

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
