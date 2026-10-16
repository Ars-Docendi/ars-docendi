using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// La regla pura de cupo efectivo por usuario (tarea «rol y cupo efectivo por
/// usuario» de sistema-seccion-unificada) — mismos casos que
/// <c>CuotaPersistenteTests</c> documenta para la aplicación real del cupo,
/// para que las dos reglas no puedan divergir sin que un test lo note.
/// </summary>
public sealed class ReglaDeCupoEfectivoTests
{
    [Fact]
    public void El_override_gana_aunque_sea_mas_chico_que_el_minimo_de_rol()
    {
        var (cupo, origen) = ReglaDeCupoEfectivo.Resolver(
            overrideDeUsuario: 1,
            codigosDeRol: ["secretaria"],
            cuposPorRol: new Dictionary<string, int> { ["secretaria"] = 10 });

        Assert.Equal(1, cupo);
        Assert.Equal("override", origen);
    }

    [Fact]
    public void El_override_gana_aunque_sea_mas_grande_que_el_minimo_de_rol()
    {
        var (cupo, origen) = ReglaDeCupoEfectivo.Resolver(
            overrideDeUsuario: 10,
            codigosDeRol: ["secretaria"],
            cuposPorRol: new Dictionary<string, int> { ["secretaria"] = 1 });

        Assert.Equal(10, cupo);
        Assert.Equal("override", origen);
    }

    [Fact]
    public void Con_varios_roles_activados_manda_el_minimo_no_el_del_rol_sin_tope()
    {
        // secretaria=3 (activado), decanato=0 (sin tope): el mínimo entre los
        // ACTIVADOS es 3, no 0 — un rol sin tope no "gana" por ser el número
        // más chico (misma regla que CuotaPersistenteTests).
        var (cupo, origen) = ReglaDeCupoEfectivo.Resolver(
            overrideDeUsuario: null,
            codigosDeRol: ["secretaria", "decanato"],
            cuposPorRol: new Dictionary<string, int> { ["secretaria"] = 3, ["decanato"] = 0 });

        Assert.Equal(3, cupo);
        Assert.Equal("rol", origen);
    }

    [Fact]
    public void Con_todos_los_roles_en_cero_el_cupo_es_cero_sin_tope()
    {
        var (cupo, origen) = ReglaDeCupoEfectivo.Resolver(
            overrideDeUsuario: null,
            codigosDeRol: ["secretaria", "decanato"],
            cuposPorRol: new Dictionary<string, int> { ["secretaria"] = 0, ["decanato"] = 0 });

        Assert.Equal(0, cupo);
        Assert.Equal("rol", origen);
    }

    [Fact]
    public void Sin_override_y_sin_ningun_rol_no_hay_nada_que_resolver()
    {
        var (cupo, origen) = ReglaDeCupoEfectivo.Resolver(
            overrideDeUsuario: null,
            codigosDeRol: [],
            cuposPorRol: new Dictionary<string, int>());

        Assert.Null(cupo);
        Assert.Null(origen);
    }

    [Fact]
    public void Un_rol_sin_entrada_en_el_mapa_cuenta_como_cero_no_como_error()
    {
        var (cupo, origen) = ReglaDeCupoEfectivo.Resolver(
            overrideDeUsuario: null,
            codigosDeRol: ["rol_desconocido", "secretaria"],
            cuposPorRol: new Dictionary<string, int> { ["secretaria"] = 5 });

        Assert.Equal(5, cupo);
        Assert.Equal("rol", origen);
    }
}
