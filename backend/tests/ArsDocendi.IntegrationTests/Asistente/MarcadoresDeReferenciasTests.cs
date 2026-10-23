using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Numeración de marcadores <c>$refN</c> (design.md D11 de asistente-rediseno-v3).
/// </summary>
public sealed class MarcadoresDeReferenciasTests
{
    private static readonly ResultadoDeMencion Materia = new(Guid.NewGuid(), "Álgebra", Carrera: "Informática");
    private static readonly ResultadoDeMencion Docente = new(Guid.NewGuid(), "Juan Pérez", Cargo: "Titular");

    [Fact]
    public void Sin_consultas_anteriores_empieza_en_uno()
    {
        var asignadas = MarcadoresDeReferencias.Asignar([(TipoDeMencion.Materia, Materia)], null);

        Assert.Equal("$ref1", Assert.Single(asignadas).Marcador);
    }

    [Fact]
    public void Varias_menciones_nuevas_se_numeran_en_el_orden_en_que_llegaron()
    {
        var asignadas = MarcadoresDeReferencias.Asignar(
            [(TipoDeMencion.Materia, Materia), (TipoDeMencion.Docente, Docente)], null);

        Assert.Equal(["$ref1", "$ref2"], asignadas.Select(a => a.Marcador));
    }

    [Fact]
    public void Continua_despues_del_maximo_marcador_que_ya_trae_el_segmento()
    {
        var asignadas = MarcadoresDeReferencias.Asignar(
            [(TipoDeMencion.Materia, Materia)],
            ["SELECT 1 WHERE id = $ref1", "SELECT 2 WHERE persona_id = $ref3"]);

        // El máximo ya usado es 3, aunque sólo haya dos consultas: la numeración
        // nunca colisiona con un marcador que el modelo ya vio en este segmento.
        Assert.Equal("$ref4", Assert.Single(asignadas).Marcador);
    }

    [Fact]
    public void Sin_menciones_nuevas_no_asigna_nada()
    {
        Assert.Empty(MarcadoresDeReferencias.Asignar([], ["SELECT 1 WHERE id = $ref1"]));
    }

    [Fact]
    public void La_etiqueta_de_una_materia_incluye_la_carrera()
    {
        Assert.Equal(
            "Álgebra (Informática)", MarcadoresDeReferencias.Etiqueta(TipoDeMencion.Materia, Materia));
    }

    [Fact]
    public void La_etiqueta_de_un_docente_es_solo_el_nombre()
    {
        Assert.Equal("Juan Pérez", MarcadoresDeReferencias.Etiqueta(TipoDeMencion.Docente, Docente));
    }
}
