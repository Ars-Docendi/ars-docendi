using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica las plantillas de rechazo contra la copia aprobada de design.md
/// D11, palabra por palabra (confirmado por el PO, 2026-09-26).
/// </summary>
/// <remarks>
/// Los tests de acá pinnean PROPIEDADES sobre esa copia fija —distinción por
/// razón/variante, eco del término sólo donde la celda de D11 tiene una forma
/// con término, áreas sólo donde D11 tiene <c>{areas}</c>, ninguna afirmación
/// de ausencia, el puntero de ayuda enrutado a la meta-pregunta— nunca una
/// sentencia distinta de la de D11: una celda sin corchete de término
/// (<c>otro_sistema</c> variante 3, <c>muy_general</c> variante 3) se
/// renderiza IGUAL con o sin término validado, y a propósito.
/// </remarks>
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

    [Theory]
    [MemberData(nameof(CombinacionesDeEntrada))]
    public void Motivo_y_variante_son_pairwise_distintos_para_la_misma_entrada(
        string? termino, string? areas)
    {
        var textos = Motivos
            .SelectMany(motivo => new[] { 0, 1, 2 }
                .Select(variante => PlantillasDeRechazo.Texto(motivo, termino, areas, variante)))
            .ToList();

        Assert.Equal(12, textos.Count);
        Assert.Equal(12, textos.Distinct(StringComparer.Ordinal).Count());
    }

    // ------------------- (b) nunca repite entre rechazos consecutivos

    [Theory]
    [MemberData(nameof(CombinacionesDeEntrada))]
    public void Rechazos_consecutivos_de_los_primeros_siete_nunca_repiten(
        string? termino, string? areas)
    {
        foreach (var motivo in Motivos)
        {
            var textos = Enumerable.Range(0, 7)
                .Select(k => PlantillasDeRechazo.Texto(motivo, termino, areas, k))
                .ToList();

            for (var i = 0; i < textos.Count - 1; i++)
            {
                Assert.NotEqual(textos[i], textos[i + 1]);
            }
        }
    }

    [Fact]
    public void La_primera_variante_es_para_rechazos_previos_cero_o_menos()
    {
        Assert.Equal(1, PlantillasDeRechazo.Variante(0));
        Assert.Equal(1, PlantillasDeRechazo.Variante(-1));
    }

    [Fact]
    public void Las_variantes_alternan_entre_dos_y_tres()
    {
        Assert.Equal(2, PlantillasDeRechazo.Variante(1));
        Assert.Equal(3, PlantillasDeRechazo.Variante(2));
        Assert.Equal(2, PlantillasDeRechazo.Variante(3));
        Assert.Equal(3, PlantillasDeRechazo.Variante(4));
    }

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

    [Theory]
    [MemberData(nameof(CeldasPorFormaDeTermino))]
    public void El_termino_aparece_solo_en_las_celdas_que_D11_declara_con_termino(
        MotivoDeRechazo motivo, int variante, bool tieneFormaConTermino)
    {
        var conTermino = PlantillasDeRechazo.Texto(motivo, Termino, Areas, variante);
        var sinTermino = PlantillasDeRechazo.Texto(motivo, null, Areas, variante);

        // Nunca aparece cuando no se da, tenga o no la celda una forma con
        // término: sin candidato no hay nada que citar.
        Assert.DoesNotContain(Termino, sinTermino, StringComparison.OrdinalIgnoreCase);

        if (tieneFormaConTermino)
        {
            Assert.Contains($"«{Termino}»", conTermino, StringComparison.Ordinal);
            Assert.NotEqual(sinTermino, conTermino);
        }
        else
        {
            // Sin corchete en D11: la celda se renderiza IGUAL con o sin
            // término validado — no es un descuido, es la tabla.
            Assert.Equal(sinTermino, conTermino);
        }
    }

    // -------------------- las áreas, sólo donde D11 tiene {areas}

    /// <summary>
    /// Si la celda (motivo, variante) de design.md D11 tiene <c>{areas}</c>.
    /// </summary>
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

    [Theory]
    [MemberData(nameof(CeldasPorFormaDeAreas))]
    public void Las_areas_aparecen_solo_en_las_celdas_que_D11_declara_con_areas(
        MotivoDeRechazo motivo, int variante, bool tieneFormaConAreas)
    {
        var conAreas = PlantillasDeRechazo.Texto(motivo, Termino, Areas, variante);
        var sinAreas = PlantillasDeRechazo.Texto(motivo, Termino, null, variante);

        if (tieneFormaConAreas)
        {
            Assert.Contains(Areas, conAreas, StringComparison.Ordinal);
            Assert.DoesNotContain(Areas, sinAreas, StringComparison.Ordinal);
            Assert.NotEqual(sinAreas, conAreas);
        }
        else
        {
            // Sin {areas} en D11 (design.md: "without areas the clause
            // carrying {areas} is replaced by AYUDA" no aplica porque nunca
            // hubo cláusula de áreas que reemplazar): la celda se renderiza
            // IGUAL con o sin catálogo.
            Assert.Equal(sinAreas, conAreas);
            Assert.DoesNotContain(Areas, conAreas, StringComparison.Ordinal);
        }
    }

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

    [Theory]
    [MemberData(nameof(TodosLosMotivos))]
    public void La_segunda_variante_trae_areas_o_el_puntero(MotivoDeRechazo motivo)
    {
        var conAreas = PlantillasDeRechazo.Texto(motivo, null, Areas, rechazosPrevios: 1);
        var sinAreas = PlantillasDeRechazo.Texto(motivo, null, null, rechazosPrevios: 1);

        Assert.Contains(Areas, conAreas, StringComparison.Ordinal);
        Assert.Contains("qué podés hacer", sinAreas, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(TodosLosMotivos))]
    public void La_tercera_variante_siempre_trae_el_puntero(MotivoDeRechazo motivo)
    {
        var texto = PlantillasDeRechazo.Texto(motivo, null, Areas, rechazosPrevios: 2);

        Assert.Contains("qué podés hacer", texto, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<MotivoDeRechazo> TodosLosMotivos() => new(Motivos);

    // ------------------------------------------------- (f) el puntero es una meta-pregunta

    [Fact]
    public void La_pregunta_citada_por_el_puntero_se_clasifica_como_meta()
    {
        Assert.Equal(IntencionSocial.Meta, EnrutadorSocial.Clasificar("¿qué podés hacer?"));
    }

    // ------------------------------------- (g) otro_sistema sólo nombra el límite fijo

    [Fact]
    public void Otro_sistema_solo_nombra_guarani_y_planillas()
    {
        var limite = "no Guaraní, ni planillas, ni otras fuentes";

        var variante1 = PlantillasDeRechazo.Texto(MotivoDeRechazo.OtroSistema, null, Areas, 0);
        var variante2 = PlantillasDeRechazo.Texto(MotivoDeRechazo.OtroSistema, null, Areas, 1);
        var variante3 = PlantillasDeRechazo.Texto(MotivoDeRechazo.OtroSistema, null, Areas, 2);

        Assert.Contains(limite, variante1, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(limite, variante2, StringComparison.OrdinalIgnoreCase);

        // La tercera no nombra ningún sistema, ni el fijo ni otro: sólo
        // apunta a la meta-pregunta (D11 no le da forma con {areas} ni con L).
        Assert.DoesNotContain("sistema", variante3, StringComparison.OrdinalIgnoreCase);
    }

    // --------------------------------------------------------------- caso genérico

    [Fact]
    public void No_cubierto_primera_variante_sin_termino_es_el_texto_generico_preexistente()
    {
        var texto = PlantillasDeRechazo.Texto(MotivoDeRechazo.NoCubierto, null, null, rechazosPrevios: 0);

        Assert.Equal(PoliticaDeAbstencion.TextoNoContestable, texto);
    }

    // ------------------------------------------------- las 12 formas, literal

    /// <summary>
    /// Las 12 celdas (4 motivos × 3 variantes), renderizadas con término
    /// «python» y áreas «designaciones, materias», tal como quedan en D11.
    /// </summary>
    [Fact]
    public void Las_doce_celdas_con_termino_y_areas_coinciden_con_D11()
    {
        Assert.Equal(
            "No puedo responder sobre «python» con los datos que consulto: trabajo con designaciones, materias.",
            PlantillasDeRechazo.Texto(MotivoDeRechazo.FueraDeTema, Termino, Areas, 0));
        Assert.Equal(
            "«python» también queda fuera de lo que consulto. Solo trabajo con designaciones, materias; "
                + "si es sobre eso, reformulala y la busco.",
            PlantillasDeRechazo.Texto(MotivoDeRechazo.FueraDeTema, Termino, Areas, 1));
        Assert.Equal(
            "Sigo sin poder ayudarte con eso («python»). " + PlantillasDeRechazo.Ayuda,
            PlantillasDeRechazo.Texto(MotivoDeRechazo.FueraDeTema, Termino, Areas, 2));

        Assert.Equal(
            "«python» parece estar en otro sistema. " + PlantillasDeRechazo.LimiteDeFuentes,
            PlantillasDeRechazo.Texto(MotivoDeRechazo.OtroSistema, Termino, Areas, 0));
        Assert.Equal(
            "Eso («python») también parece venir de otro sistema. "
                + PlantillasDeRechazo.LimiteDeFuentes + " Lo que sí consulto: designaciones, materias.",
            PlantillasDeRechazo.Texto(MotivoDeRechazo.OtroSistema, Termino, Areas, 1));
        Assert.Equal(
            "Esa información no la leo desde acá. " + PlantillasDeRechazo.Ayuda,
            PlantillasDeRechazo.Texto(MotivoDeRechazo.OtroSistema, Termino, Areas, 2));

        Assert.Equal(
            "La pregunta sobre «python» es muy amplia para una consulta. Contame qué dato puntual "
                + "buscás y de qué carrera, materia o período.",
            PlantillasDeRechazo.Texto(MotivoDeRechazo.MuyGeneral, Termino, Areas, 0));
        Assert.Equal(
            "Necesito una pregunta más concreta sobre «python». Puedo consultar designaciones, "
                + "materias: decime qué dato de eso te sirve.",
            PlantillasDeRechazo.Texto(MotivoDeRechazo.MuyGeneral, Termino, Areas, 1));
        Assert.Equal(
            "Todavía me falta precisión para armar la consulta. " + PlantillasDeRechazo.Ayuda,
            PlantillasDeRechazo.Texto(MotivoDeRechazo.MuyGeneral, Termino, Areas, 2));

        Assert.Equal(
            "No puedo responder sobre «python» con la información que tengo disponible.",
            PlantillasDeRechazo.Texto(MotivoDeRechazo.NoCubierto, Termino, Areas, 0));
        Assert.Equal(
            "Tampoco puedo responder eso sobre «python» con lo que consulto. Lo que sí tengo: "
                + "designaciones, materias.",
            PlantillasDeRechazo.Texto(MotivoDeRechazo.NoCubierto, Termino, Areas, 1));
        Assert.Equal(
            "Eso («python») sigue fuera de lo que puedo consultar. " + PlantillasDeRechazo.Ayuda,
            PlantillasDeRechazo.Texto(MotivoDeRechazo.NoCubierto, Termino, Areas, 2));
    }

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
