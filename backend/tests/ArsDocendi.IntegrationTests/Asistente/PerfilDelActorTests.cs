using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El perfil del actor, y en particular qué significa para él un resultado vacío.
/// </summary>
/// <remarks>
/// LO QUE ESTOS TESTS EXISTEN PARA EVITAR. El carril usaba <c>EsGlobal</c> como
/// sinónimo de «ve todo», y con eso decidía tres cosas: si vale la pena gastar el
/// reintento sobre un resultado vacío, qué texto se le muestra a quien preguntó, y
/// si el redactor recibe la regla de no afirmar ausencias.
///
/// El ámbito y el permiso son ejes <b>independientes</b>. La policy de RLS conjuga
/// los dos —<c>asistente_tiene_permiso</c> AND <c>asistente_materias_visibles</c>—,
/// así que un actor de ámbito global sin el permiso de dominio ve cero filas
/// exactamente igual que uno cuyo literal no matcheó. Con <c>EsGlobal</c> como
/// proxy, a ése el sistema le afirma «no encontré ningún registro»: una afirmación
/// falsa sobre los datos, dicha con toda seguridad.
///
/// No es hipotético. La matriz de permisos es editable desde <c>/membresia-roles</c>
/// sin migración —la propia siembra dice que es un default «PENDIENTE DE
/// CONFIRMACIÓN CON EL CLIENTE»—, así que alcanza con que alguien le saque
/// <c>designaciones.ver</c> a Secretaría para que el asistente empiece a mentirle.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class PerfilDelActorTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_perfil")
{
    /// <summary>Secretaría Académica: ámbito global.</summary>
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    /// <summary>Jefe de cátedra: ámbito de materia, con el permiso de dominio.</summary>
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    /// <summary>Decanato: ámbito global, autoridad de aprobación final.</summary>
    private static readonly Guid Decanato = Guid.Parse("a0000000-0000-4000-8000-000000000005");

    private const string PermisoDeDominio = "designaciones.ver";

    [Fact]
    public async Task Un_actor_global_con_el_permiso_de_dominio_alcanza_todo()
    {
        await SembrarAsync();

        var perfil = await Consultor().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        // El caso base, y está acá para que el test de abajo signifique algo: sin
        // éste, «no alcanza todo» podría ser que el perfil nunca alcanza nada.
        Assert.True(perfil.EsGlobal);
        Assert.True(perfil.AlcanzaDesignaciones);
    }

    [Fact]
    public async Task Un_actor_global_sin_el_permiso_de_dominio_NO_alcanza_todo()
    {
        // ES EL CASO QUE ROMPÍA. El ámbito sigue siendo global —eso no cambió— pero
        // sin el permiso la policy le devuelve cero filas de las cuatro tablas del
        // trámite. Si el perfil dijera que alcanza todo, el carril interpretaría ese
        // vacío como «no hay datos» y se lo diría así.
        await SembrarAsync();
        await QuitarPermisoDeDominioAsync();

        var perfil = await Consultor().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.True(perfil.EsGlobal);
        Assert.False(perfil.AlcanzaDesignaciones);
    }

    [Fact]
    public async Task Un_actor_de_materia_con_el_permiso_tampoco_alcanza_todo()
    {
        // La otra mitad: tener el permiso no alcanza si el ámbito no llega. Los dos
        // ejes tienen que cumplirse, que es lo mismo que hace la policy.
        await SembrarAsync();

        var perfil = await Consultor().ObtenerAsync(
            Jefe, TestContext.Current.CancellationToken);

        Assert.False(perfil.EsGlobal);
        Assert.False(perfil.AlcanzaDesignaciones);
    }

    [Fact]
    public void El_texto_del_vacio_solo_afirma_ausencia_cuando_el_actor_alcanza_todo()
    {
        // La política es pura, así que acá se prueba la decisión sin base: dado el
        // booleano, qué se dice. Que el booleano se calcule bien lo prueban los tres
        // de arriba.
        var alcanzaTodo = PoliticaDeAbstencion.TextoDeResultadoVacio(true);
        var noAlcanza = PoliticaDeAbstencion.TextoDeResultadoVacio(false);

        Assert.Contains("No encontré ningún registro", alcanzaTodo, StringComparison.Ordinal);

        // La afirmación de ausencia es EXACTAMENTE lo que no se puede decir cuando
        // el vacío puede venir del alcance.
        Assert.DoesNotContain("No encontré ningún registro", noAlcanza, StringComparison.Ordinal);
        Assert.Contains("fuera de tu alcance", noAlcanza, StringComparison.Ordinal);
    }

    [Fact]
    public void No_conviene_reintentar_cuando_el_vacio_puede_venir_del_alcance()
    {
        // Gastar el reintento donde ningún reintento puede ayudar: la consulta
        // estaba bien, lo que no llega es el alcance.
        var vacio = new ResultadoDeConsulta([], [], false);

        Assert.True(PoliticaDeAbstencion.ConvieneReintentar(vacio, alcanzaTodo: true));
        Assert.False(PoliticaDeAbstencion.ConvieneReintentar(vacio, alcanzaTodo: false));
    }

    // ------------------------------------------------------------------ apoyo

    private IPerfilDelActor Consultor() => new ConsultorDeAlcance(Apertura);

    /// <summary>
    /// Le saca a todos los roles el permiso de dominio, dejando el ámbito intacto.
    /// </summary>
    /// <remarks>
    /// Se borra la fila de <c>rol_permisos</c> y no se toca <c>user_roles</c>: es lo
    /// que hace que el actor siga siendo global y deje de ver, que es justamente la
    /// combinación que el proxy no distinguía. Y es lo que alguien puede hacer desde
    /// la superficie de administración sin migración ninguna.
    /// </remarks>
    private async Task QuitarPermisoDeDominioAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            DELETE FROM identity.rol_permisos rp
             USING identity.permisos p
             WHERE p.id = rp.permiso_id AND p.code = @permiso
            """, conexion);

        comando.Parameters.AddWithValue("permiso", PermisoDeDominio);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // -------------------------------------------- la matriz de la base

    [Fact]
    public async Task Decanato_alcanza_los_datos_personales_del_padron()
    {
        // Decanato es la autoridad de aprobación final y necesita el contacto de un
        // docente para resolver un trámite. Es global, así que sólo le faltaba
        // `usuarios.ver` — la conjunción que abre la conexión con datos personales.
        //
        // Se afirma sobre el PERFIL RESUELTO y no sobre la fila de rol_permisos: lo
        // que importa no es que el INSERT esté, sino que el actor termine con la
        // conexión que puede leer teléfono y mail.
        await SembrarAsync();

        var perfil = await Consultor().ObtenerAsync(
            Decanato, TestContext.Current.CancellationToken);

        Assert.True(perfil.EsGlobal);
        Assert.True(perfil.VeDatosPersonales);
    }

    [Fact]
    public async Task Decanato_no_puede_administrar_usuarios()
    {
        // LA MITAD QUE EL CAMBIO NO DEBE MOVER. `usuarios.ver` es de lectura; las
        // escrituras van por `usuarios.administrar`. Si alguien concediera los dos
        // «para simplificar», Decanato pasaría a poder dar de baja usuarios sin que
        // nada lo señale.
        await SembrarAsync();

        var permisos = new List<string>();

        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand(
            """
            SELECT p.code
              FROM identity.roles r
              JOIN identity.rol_permisos rp ON rp.rol_id = r.id
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE r.code = 'decanato'
            """, conexion))
        await using (var lector = await comando.ExecuteReaderAsync(
            TestContext.Current.CancellationToken))
        {
            while (await lector.ReadAsync(TestContext.Current.CancellationToken))
            {
                permisos.Add(lector.GetString(0));
            }
        }

        Assert.Contains("usuarios.ver", permisos);
        Assert.DoesNotContain("usuarios.administrar", permisos);
    }

}
