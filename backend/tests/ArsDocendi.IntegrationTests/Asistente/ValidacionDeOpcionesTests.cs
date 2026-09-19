using System.Reflection;
using Microsoft.Extensions.Options;
using Modules.Asistente;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la validación de las perillas del módulo.
/// </summary>
/// <remarks>
/// Corre en memoria: el validador es una función pura sobre un objeto de opciones.
/// </remarks>
public sealed class ValidacionDeOpcionesTests
{
    private static ValidateOptionsResult Validar(Action<OpcionesAsistente> ajustar)
    {
        var valores = new OpcionesAsistente();
        ajustar(valores);

        return new ValidadorDeOpcionesAsistente().Validate(name: null, valores);
    }

    /// <summary>Valida con una sola perilla —nombrada por reflexión— puesta en un valor.</summary>
    private static ValidateOptionsResult EnPerilla(string perilla, int valor) =>
        Validar(o => typeof(OpcionesAsistente).GetProperty(perilla)!.SetValue(o, valor));

    [Fact]
    public void Los_defaults_del_modulo_son_validos()
    {
        // Si esto fallara, el módulo vendría de fábrica con una configuración que
        // él mismo rechaza.
        var resultado = Validar(_ => { });

        Assert.True(resultado.Succeeded, string.Join(" | ", resultado.Failures ?? []));
    }

    // ------------------------------------------------- estrictamente positivas

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Un_tope_de_filas_no_positivo_se_rechaza(int valor)
    {
        var resultado = Validar(o => o.TopeDeFilas = valor);

        Assert.False(resultado.Succeeded);
        Assert.Contains(
            resultado.Failures!,
            f => f.Contains(nameof(OpcionesAsistente.TopeDeFilas), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(nameof(OpcionesAsistente.TimeoutDeSentenciaMs))]
    [InlineData(nameof(OpcionesAsistente.TimeoutDeComandoSegundos))]
    public void Los_dos_timeouts_en_cero_se_rechazan(string perilla)
    {
        // ESTOS DOS SON LOS QUE IMPORTAN. Cero no es «desactivado» acá: en
        // PostgreSQL `statement_timeout = 0` significa SIN LÍMITE, y en Npgsql
        // `CommandTimeout = 0` es lo mismo. Un cero en la configuración de un
        // ambiente apaga en silencio la cota que impide que una consulta generada
        // con un producto cartesiano ocupe un backend hasta terminar.
        var resultado = EnPerilla(perilla, 0);

        Assert.False(resultado.Succeeded);
        Assert.Contains(resultado.Failures!, f => f.Contains(perilla, StringComparison.Ordinal));
    }

    // ------------------------------------------------- el cero como apagado

    [Fact]
    public void Las_cuatro_perillas_que_apagan_con_cero_aceptan_el_cero()
    {
        // Es el punto del renglón: `[Range(1, …)]` sobre estas cuatro —el reflejo
        // obvio— rompería una capacidad que el módulo ofrece a propósito. Sin cupo
        // por actor, sin corte del proveedor, sin presupuesto de turno y sin
        // historial para el reescritor son cuatro configuraciones válidas.
        var resultado = Validar(o =>
        {
            o.CupoDeLlamadasPorActor = 0;
            o.FallosParaAbrirElBreaker = 0;
            o.PresupuestoDelTurnoSegundos = 0;
            o.TopeDeTurnosDelHistorial = 0;
        });

        Assert.True(resultado.Succeeded, string.Join(" | ", resultado.Failures ?? []));

        // El cero las apaga; el negativo no significa nada y se sigue rechazando.
        Assert.False(EnPerilla(nameof(OpcionesAsistente.CupoDeLlamadasPorActor), -1).Succeeded);
    }

    [Fact]
    public void La_espera_base_admite_cero_pero_no_negativo()
    {
        // Reintentar sin esperar es agresivo pero legítimo. Esperar un tiempo
        // negativo no significa nada.
        Assert.True(Validar(o => o.EsperaBaseMs = 0).Succeeded);
        Assert.False(Validar(o => o.EsperaBaseMs = -1).Succeeded);
    }

    // ------------------------------------------------------- las relaciones

    [Fact]
    public void El_tope_de_espera_no_puede_quedar_por_debajo_de_la_espera_base()
    {
        var resultado = Validar(o =>
        {
            o.EsperaBaseMs = 5000;
            o.EsperaMaximaMs = 1000;
        });

        Assert.False(resultado.Succeeded);
        Assert.Contains(
            resultado.Failures!,
            f => f.Contains(nameof(OpcionesAsistente.EsperaMaximaMs), StringComparison.Ordinal));
    }

    [Fact]
    public void El_timeout_de_comando_tiene_que_quedar_por_encima_del_de_sentencia()
    {
        // Uno va en segundos y el otro en milisegundos, que es exactamente por qué
        // esta relación se puede invertir sin que nadie lo note leyendo el archivo
        // de configuración: 8 y 8000 parecen el mismo número.
        var resultado = Validar(o =>
        {
            o.TimeoutDeComandoSegundos = 8;
            o.TimeoutDeSentenciaMs = 8000;
        });

        Assert.False(resultado.Succeeded);
        Assert.Contains(
            resultado.Failures!,
            f => f.Contains(nameof(OpcionesAsistente.TimeoutDeComandoSegundos), StringComparison.Ordinal));
    }

    // --------------------------------------------- ninguna perilla sin clasificar

    /// <summary>Si poner esa perilla en <c>-1</c> produce una falla que la nombra.</summary>
    private static bool LaValidacionNombra(string perilla) =>
        EnPerilla(perilla, -1) is { Succeeded: false, Failures: { } fallas }
        && fallas.Any(f => f.Contains(perilla, StringComparison.Ordinal));

    [Fact]
    public void Toda_perilla_numerica_esta_clasificada_en_algun_balde()
    {
        // EL GUARD QUE HACE QUE ESTO NO SE PUDRA. Una perilla nueva sin regla no
        // rompe nada visible: simplemente no se valida, y nadie se entera hasta que
        // un ambiente la pone en cero. Se afirma sobre la REFLEXIÓN del tipo y no
        // sobre una lista escrita, para que agregar una propiedad al options sea lo
        // que dispare el test.
        var numericas = typeof(OpcionesAsistente)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(int))
            .Select(p => p.Name)
            .ToList();

        Assert.NotEmpty(numericas);

        // Poner cada una en -1 de a una: el negativo no es válido en NINGUNO de los
        // tres baldes, así que una perilla clasificada siempre se hace nombrar.
        var sinRegla = numericas.Where(nombre => !LaValidacionNombra(nombre)).ToList();

        Assert.True(sinRegla.Count == 0,
            "Hay perillas numéricas que ninguna regla del validador nombra. Una perilla sin "
            + "regla no falla: simplemente no se valida. Clasificala en uno de los tres baldes "
            + "de `ValidadorDeOpcionesAsistente`. Detectadas: " + string.Join(", ", sinRegla));
    }
}
