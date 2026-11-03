using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El bloque «GLOSARIO INSTITUCIONAL» del prefijo: qué dice, dónde va y que no
/// cambia nada cuando la opción está apagada (asistente-glosario-institucional, D6).
/// </summary>
public sealed class RenderizadoDelGlosarioTests
{
    [Fact]
    public void Cada_linea_nombra_sus_columnas_y_sus_valores_guardados()
    {
        var bloque = RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Vigente.Terminos);

        Assert.Contains("designaciones.pedidos.estado = 'en_revision_decanato'", bloque, StringComparison.Ordinal);
        Assert.Contains(
            "designaciones.pedidos.estado IN ('en_lote', 'rechazado', 'cancelado')", bloque, StringComparison.Ordinal);
        // La conjunción de «en Cátedra»: las dos igualdades en la misma línea.
        Assert.Contains(
            "designaciones.pedidos.estado = 'devuelto' AND designaciones.pedidos.propietario_actual = 'jefe_catedra'",
            bloque, StringComparison.Ordinal);
        Assert.Contains("designaciones.pedidos.novedad = 'Cambio de cargo o dedicación'", bloque, StringComparison.Ordinal);
        // Lo que no es una conjunción de igualdades lleva su pista.
        Assert.Contains(
            "SQL: COALESCE(horas,0) + COALESCE(horas_investigacion,0) + COALESCE(horas_externas,0)",
            bloque, StringComparison.Ordinal);
    }

}
