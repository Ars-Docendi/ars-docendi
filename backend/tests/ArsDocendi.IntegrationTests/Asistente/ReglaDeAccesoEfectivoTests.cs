using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// La regla pura de acceso efectivo (asistente-acceso-granular, design.md D2).
/// Es la única implementación: la usan el bloqueo del turno y el panel.
/// </summary>
public sealed class ReglaDeAccesoEfectivoTests
{
    private static readonly Dictionary<string, bool> Todos = new() { ["docente"] = true, ["secretaria"] = true };

    [Fact]
    public void La_revocacion_propia_gana_aunque_el_rol_tenga_acceso()
    {
        var (acceso, origen) = ReglaDeAccesoEfectivo.Resolver(true, ["secretaria"], Todos);

        Assert.False(acceso);
        Assert.Equal("propio", origen);
    }

    [Fact]
    public void Sin_revocacion_hereda_el_acceso_del_rol()
    {
        var (acceso, origen) = ReglaDeAccesoEfectivo.Resolver(false, ["secretaria"], Todos);

        Assert.True(acceso);
        Assert.Equal("rol", origen);
    }

    [Fact]
    public void Un_rol_apagado_quita_el_acceso_a_quien_solo_tiene_ese_rol()
    {
        var (acceso, origen) = ReglaDeAccesoEfectivo.Resolver(
            false, ["docente"], new Dictionary<string, bool> { ["docente"] = false });

        Assert.False(acceso);
        Assert.Equal("rol", origen);
    }

    [Fact]
    public void Basta_un_rol_con_acceso_para_tenerlo()
    {
        var (acceso, _) = ReglaDeAccesoEfectivo.Resolver(
            false,
            ["docente", "secretaria"],
            new Dictionary<string, bool> { ["docente"] = false, ["secretaria"] = true });

        Assert.True(acceso);
    }

    [Fact]
    public void Un_rol_sin_fila_cuenta_como_habilitado()
    {
        var (acceso, _) = ReglaDeAccesoEfectivo.Resolver(false, ["rol_nuevo"], new Dictionary<string, bool>());

        Assert.True(acceso);
    }

    [Fact]
    public void Sin_roles_de_sistema_decide_solo_el_permiso()
    {
        var (acceso, origen) = ReglaDeAccesoEfectivo.Resolver(false, [], new Dictionary<string, bool>());

        Assert.True(acceso);
        Assert.Equal("rol", origen);
    }
}
