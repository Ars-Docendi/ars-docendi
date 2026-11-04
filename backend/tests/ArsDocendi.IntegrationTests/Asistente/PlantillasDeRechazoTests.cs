using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica las plantillas de rechazo contra la copia aprobada de design.md D11, palabra por
/// palabra (confirmado por el PO, 2026-09-26).
/// </summary>
public sealed class PlantillasDeRechazoTests
{
    private static readonly MotivoDeRechazo[] Motivos =
    [
        MotivoDeRechazo.FueraDeTema, MotivoDeRechazo.OtroSistema,
        MotivoDeRechazo.MuyGeneral, MotivoDeRechazo.NoCubierto,
    ];

    private const string Areas = "designaciones, materias";
    private const string Termino = "python";

    // ------------------------------- (a) distinción por razón × variante

    public static TheoryData<string?, string?> CombinacionesDeEntrada() => new()
    {
        { null, null },
        { null, Areas },
        { Termino, null },
        { Termino, Areas },
    };

    // ------------------- (b) nunca repite entre rechazos consecutivos

    // ------------------ (c) el término, sólo donde D11 tiene esa forma

    /// <summary>
    /// Si la celda (motivo, variante) de design.md D11 tiene una forma CON
    /// término —tiene corchetes de término—, o no.
    /// </summary>
    public static TheoryData<MotivoDeRechazo, int, bool> CeldasPorFormaDeTermino() => new()
    {
        { MotivoDeRechazo.FueraDeTema, 0, true },
        { MotivoDeRechazo.FueraDeTema, 1, true },
        { MotivoDeRechazo.FueraDeTema, 2, true },
        { MotivoDeRechazo.OtroSistema, 0, true },
        { MotivoDeRechazo.OtroSistema, 1, true },
        // otro_sistema variante 3: "Esa información no la leo desde acá.
        // AYUDA" — SIN corchete de término en D11.
        { MotivoDeRechazo.OtroSistema, 2, false },
        { MotivoDeRechazo.MuyGeneral, 0, true },
        { MotivoDeRechazo.MuyGeneral, 1, true },
        // muy_general variante 3: "Todavía me falta precisión para armar la
        // consulta. AYUDA" — SIN corchete de término en D11.
        { MotivoDeRechazo.MuyGeneral, 2, false },
        { MotivoDeRechazo.NoCubierto, 0, true },
        { MotivoDeRechazo.NoCubierto, 1, true },
        { MotivoDeRechazo.NoCubierto, 2, true },
    };

    // -------------------- las áreas, sólo donde D11 tiene {areas}

    /// <summary>Si la celda (motivo, variante) de design.md D11 tiene <c>{areas}</c>.</summary>
    public static TheoryData<MotivoDeRechazo, int, bool> CeldasPorFormaDeAreas() => new()
    {
        { MotivoDeRechazo.FueraDeTema, 0, true },
        { MotivoDeRechazo.FueraDeTema, 1, true },
        { MotivoDeRechazo.FueraDeTema, 2, false },
        { MotivoDeRechazo.OtroSistema, 0, false },
        { MotivoDeRechazo.OtroSistema, 1, true },
        { MotivoDeRechazo.OtroSistema, 2, false },
        { MotivoDeRechazo.MuyGeneral, 0, false },
        { MotivoDeRechazo.MuyGeneral, 1, true },
        { MotivoDeRechazo.MuyGeneral, 2, false },
        { MotivoDeRechazo.NoCubierto, 0, false },
        { MotivoDeRechazo.NoCubierto, 1, true },
        { MotivoDeRechazo.NoCubierto, 2, false },
    };

    // -------------------------------------------- (d) ninguna afirmación de ausencia

    [Theory]
    [InlineData("no hay")]
    [InlineData("no existe")]
    [InlineData("no encontré")]
    [InlineData("ningún")]
    [InlineData("nadie")]
    public void Ninguna_renderizacion_afirma_ausencia(string prohibida)
    {
        var textos = TodasLasCombinaciones();

        Assert.All(textos, texto =>
            Assert.DoesNotContain(prohibida, texto, StringComparison.OrdinalIgnoreCase));
    }

    // -------------------------------- (e) variantes 2 y 3 traen áreas o el puntero

    public static TheoryData<MotivoDeRechazo> TodosLosMotivos() => new(Motivos);

    // ------------------------------------------------- (f) el puntero es una meta-pregunta

    [Fact]
    public void La_pregunta_citada_por_el_puntero_se_clasifica_como_meta()
    {
        Assert.Equal(IntencionSocial.Meta, EnrutadorSocial.Clasificar("¿qué podés hacer?"));
    }

    // ------------------------------------- (g) otro_sistema sólo nombra el límite fijo

    // --------------------------------------------------------------- caso genérico

    // ------------------------------------------------- las 12 formas, literal

    /// <summary>
    /// Las 12 celdas (4 motivos × 3 variantes), renderizadas con término
    /// «python» y áreas «designaciones, materias», tal como quedan en D11.
    /// </summary>

    // --------------------------------------------------------------------- apoyo

    private static IReadOnlyList<string> TodasLasCombinaciones()
    {
        var textos = new List<string>();

        foreach (var motivo in Motivos)
        {
            foreach (var variante in new[] { 0, 1, 2 })
            {
                foreach (var termino in new[] { null, Termino })
                {
                    foreach (var areas in new[] { null, Areas })
                    {
                        textos.Add(PlantillasDeRechazo.Texto(motivo, termino, areas, variante));
                    }
                }
            }
        }

        return textos;
    }
}
