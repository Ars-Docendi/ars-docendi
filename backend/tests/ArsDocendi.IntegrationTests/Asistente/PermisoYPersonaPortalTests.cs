using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El permiso y la función que la RLS de portal necesita como predicado.
/// </summary>
/// <remarks>
/// Las dos piezas de <c>identity</c> que hacen falta antes de que exista una sola
/// policy sobre <c>portal</c>: el interruptor que decide quién puede leer la
/// trayectoria ajena, y la resolución de a qué persona corresponde el actor del
/// turno.
/// </remarks>
public sealed class PermisoYPersonaPortalTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_portal_identity")
{
    private const string PermisoNuevo = "portal.ver_trayectoria_ajena";

    /// <summary>Secretaría Académica, ámbito global y con cuenta.</summary>
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    // ------------------------------------------------------------- el permiso

    [Fact]
    public async Task El_permiso_existe_y_no_lo_tiene_ningun_rol()
    {
        // NACE VACÍO Y ES DELIBERADO. Quién puede leer la trayectoria de otro es una
        // decisión del Departamento con respaldo normativo. Un permiso concedido de
        // arranque es difícil de quitar; uno vacío se concede en treinta segundos
        // desde /membresia-roles y queda registrado quién lo pidió.
        await SembrarAsync();

        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM identity.permisos WHERE code = @codigo",
            ("codigo", PermisoNuevo)));

        Assert.Equal(0L, await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = @codigo
            """,
            ("codigo", PermisoNuevo)));
    }

    [Fact]
    public async Task El_permiso_nuevo_no_es_portal_ver()
    {
        // LA TRAMPA QUE ESTE PERMISO EXISTE PARA CERRAR. `portal.ver` lo tienen los
        // siete roles y significa «acceder al portal PROPIO»: un predicado que
        // preguntara por él sería verdadero siempre y no protegería nada.
        //
        // Se afirma la premisa —que `portal.ver` está repartido— para que quede
        // claro por qué no sirve, y no sólo que no se usa.
        await SembrarAsync();

        var conPortalVer = await EscalarAsync<long>(
            """
            SELECT count(DISTINCT rp.rol_id)
              FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = 'portal.ver'
            """);

        Assert.True(
            conPortalVer >= 6,
            $"`portal.ver` lo tienen {conPortalVer} roles. Si dejara de estar repartido, "
            + "convendría revisar por qué este permiso separado existe.");
    }

    // ------------------------------------------------------------- la función

    [Fact]
    public async Task La_funcion_resuelve_la_persona_del_actor()
    {
        await SembrarAsync();

        var persona = await EscalarComoActorAsync<Guid>(
            Secretaria, "SELECT identity.asistente_persona()");

        var esperada = await EscalarAsync<Guid>(
            "SELECT persona_id FROM identity.users WHERE id = @actor", ("actor", Secretaria));

        Assert.Equal(esperada, persona);
    }

    [Fact]
    public async Task Sin_actor_fijado_la_funcion_no_devuelve_ninguna_persona()
    {
        // Falla cerrado, igual que el resto de la resolución del actor: sin ajuste no
        // hay actor, sin actor no hay persona, y un NULL hace falsa la comparación de
        // la policy. El actor no ve ningún perfil propio porque no tiene ninguno.
        await SembrarAsync();

        await using var conexion = await AbrirConexionComoAsistenteAsync(false);
        await using var comando = new NpgsqlCommand(
            "SELECT identity.asistente_persona()", conexion);

        Assert.Equal(
            DBNull.Value,
            await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Los_dos_roles_de_lectura_pueden_ejecutar_la_funcion()
    {
        // SIN EL GRANT LA POLICY NO DEVUELVE CERO FILAS: tira «permission denied for
        // function». Es un modo de falla distinto —ruidoso en vez de silencioso— y
        // por eso CREATE, REVOKE FROM PUBLIC y GRANT son una unidad indivisible.
        await SembrarAsync();

        foreach (var conDatosPersonales in new[] { false, true })
        {
            await using var conexion = await AbrirConexionComoAsistenteAsync(conDatosPersonales);
            await using var comando = new NpgsqlCommand(
                "SELECT identity.asistente_persona()", conexion);

            // No importa qué devuelve —sin actor es nulo—: importa que no explote.
            await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task PUBLIC_no_puede_ejecutar_la_funcion()
    {
        await SembrarAsync();

        Assert.False(await EscalarAsync<bool>(
            "SELECT has_function_privilege('public', 'identity.asistente_persona()', 'EXECUTE')"));
    }

    // ------------------------------------------------------------------ apoyo

    private async Task<T> EscalarComoActorAsync<T>(Guid actor, string sql)
    {
        await using var conexion = await AbrirConexionComoAsistenteAsync(false);
        await using var transaccion = await conexion.BeginTransactionAsync(
            TestContext.Current.CancellationToken);

        await using (var ajuste = new NpgsqlCommand(
            "SELECT set_config('app.asistente_user_id', @actor, TRUE)", conexion, transaccion))
        {
            ajuste.Parameters.AddWithValue("actor", actor.ToString());
            await ajuste.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var comando = new NpgsqlCommand(sql, conexion, transaccion);
        return (T)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

}
