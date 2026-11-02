using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class IdentityPersistenciaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "identity")
{
    [Fact]
    public async Task Servicio_crea_persona_sin_cuenta_y_rechaza_documento_duplicado()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = new ServicioPersonas(new RepositorioDocentes(db));
        var datos = new DatosPersonaSinCuenta("30111999", "Ada", "Lovelace");

        var id = await servicio.CrearPersonaSinCuentaAsync(datos, ct);
        var persona = await db.Personas.SingleAsync(p => p.Id == id, ct);

        Assert.Equal(datos.Documento, persona.Documento);
        Assert.Null(persona.Legajo);
        Assert.False(await db.Usuarios.AnyAsync(u => u.PersonaId == id, ct));

        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() =>
            servicio.CrearPersonaSinCuentaAsync(datos, ct));

        Assert.Equal(TipoErrorAplicacion.Conflicto, error.Tipo);
        Assert.Equal("identity-document-conflict", error.Codigo);
    }

    [Fact]
    public async Task Altas_concurrentes_del_mismo_documento_no_duplican_personas()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var dbUno = PostgresFixture.CrearIdentity(Cadena);
        await using var dbDos = PostgresFixture.CrearIdentity(Cadena);
        var datos = new DatosPersonaSinCuenta("30111888", "Ada", "Lovelace");

        var resultados = await Task.WhenAll(
            IntentarAltaAsync(new ServicioPersonas(new RepositorioDocentes(dbUno)), datos, ct),
            IntentarAltaAsync(new ServicioPersonas(new RepositorioDocentes(dbDos)), datos, ct));

        Assert.Equal(1, resultados.Count(id => id is not null));
        await using var verificacion = PostgresFixture.CrearIdentity(Cadena);
        Assert.Equal(1, await verificacion.Personas.CountAsync(p => p.Documento == datos.Documento, ct));
    }

    private static async Task<Guid?> IntentarAltaAsync(
        IAdministracionIdentity servicio,
        DatosPersonaSinCuenta datos,
        CancellationToken ct)
    {
        try
        {
            return await servicio.CrearPersonaSinCuentaAsync(datos, ct);
        }
        catch (ExcepcionAplicacion error) when (error.Codigo == "identity-document-conflict")
        {
            return null;
        }
    }

    [Fact]
    public async Task Rol_custom_con_scope_se_persiste_como_no_sistema()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        var id = Guid.NewGuid();
        await using var comando = new NpgsqlCommand("""
            INSERT INTO identity.roles (id, code, name, scope)
            VALUES (@id, 'veedor_custom', 'Veedor custom', 'global')
            RETURNING es_sistema
            """, conexion);
        comando.Parameters.AddWithValue("id", id);

        Assert.False((bool)(await comando.ExecuteScalarAsync(ct))!);
    }

    [Fact]
    public async Task Rol_de_sistema_protege_toda_su_identidad()
    {
        await using var conexion = await AbrirConexionAsync();
        var codigo = await Assert.ThrowsAsync<PostgresException>(() =>
            EjecutarAsync(conexion, "UPDATE identity.roles SET code = 'secretaria_2' WHERE code = 'secretaria'"));
        var scope = await Assert.ThrowsAsync<PostgresException>(() =>
            EjecutarAsync(conexion, "UPDATE identity.roles SET scope = 'materia' WHERE code = 'secretaria'"));

        var nombre = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(
            conexion,
            "UPDATE identity.roles SET name = 'Secretaría Académica editada' WHERE code = 'secretaria'"));
        var descripcion = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(
            conexion,
            "UPDATE identity.roles SET description = 'No debe persistir' WHERE code = 'secretaria'"));

        Assert.Equal(PostgresErrorCodes.RaiseException, codigo.SqlState);
        Assert.Equal(PostgresErrorCodes.RaiseException, scope.SqlState);
        Assert.Equal(PostgresErrorCodes.RaiseException, nombre.SqlState);
        Assert.Equal(PostgresErrorCodes.RaiseException, descripcion.SqlState);
        Assert.Equal("Secretaría Académica",
            await EscalarAsync<string>(conexion, "SELECT name FROM identity.roles WHERE code = 'secretaria'"));
    }

    [Fact]
    public async Task Rol_de_sistema_no_se_puede_borrar()
    {
        await using var conexion = await AbrirConexionAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            EjecutarAsync(conexion, "DELETE FROM identity.roles WHERE code = 'decanato'"));

        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Equal(1L, await EscalarAsync<long>(
            conexion, "SELECT count(*) FROM identity.roles WHERE code = 'decanato'"));
    }

    [Fact]
    public async Task Rol_sin_scope_o_con_scope_invalido_es_rechazado()
    {
        await using var conexion = await AbrirConexionAsync();

        var sinScope = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(conexion, """
            INSERT INTO identity.roles (code, name, scope)
            VALUES ('sin_scope', 'Sin scope', NULL)
            """));
        var invalido = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(conexion, """
            INSERT INTO identity.roles (code, name, scope)
            VALUES ('scope_invalido', 'Scope inválido', 'departamento')
            """));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, sinScope.SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalido.SqlState);
    }

    [Fact]
    public async Task Scope_de_rol_custom_gobierna_su_asignacion()
    {
        await using var conexion = await AbrirConexionAsync();
        var usuario = await InsertarUsuarioAsync(conexion);
        var (carrera, materia) = await InsertarCarreraMateriaAsync(conexion);
        var rol = Guid.NewGuid();
        await EjecutarAsync(conexion, """
            INSERT INTO identity.roles (id, code, name, scope)
            VALUES (@rol, 'coordinador_custom', 'Coordinador custom', 'carrera')
            """, new NpgsqlParameter("rol", rol));

        await EjecutarAsync(conexion, """
            INSERT INTO identity.user_roles (user_id, role_id, carrera_id)
            VALUES (@usuario, @rol, @carrera)
            """, new NpgsqlParameter("usuario", usuario), new NpgsqlParameter("rol", rol), new NpgsqlParameter("carrera", carrera));

        var error = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(conexion, """
            INSERT INTO identity.user_roles (user_id, role_id, carrera_id, materia_id)
            VALUES (@usuario, @rol, @carrera, @materia)
            """, new NpgsqlParameter("usuario", usuario), new NpgsqlParameter("rol", rol), new NpgsqlParameter("carrera", carrera), new NpgsqlParameter("materia", materia)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("user_roles_scope_coherente", error.ConstraintName);
    }

    [Fact]
    public async Task Rol_materia_personalizado_se_asigna_sin_carrera()
    {
        await using var conexion = await AbrirConexionAsync();
        var usuario = await InsertarUsuarioAsync(conexion);
        var (_, materia) = await InsertarCarreraMateriaAsync(conexion);
        var rol = Guid.NewGuid();
        await EjecutarAsync(conexion, """
            INSERT INTO identity.roles (id, code, name, scope)
            VALUES (@rol, 'revisor_materia_custom', 'Revisor materia', 'materia')
            """, new NpgsqlParameter("rol", rol));

        await EjecutarAsync(conexion, """
            INSERT INTO identity.user_roles (user_id, role_id, materia_id)
            VALUES (@usuario, @rol, @materia)
            """, new NpgsqlParameter("usuario", usuario), new NpgsqlParameter("rol", rol), new NpgsqlParameter("materia", materia));

        var carreraNula = await EscalarAsync<bool>(conexion, """
            SELECT carrera_id IS NULL FROM identity.user_roles
            WHERE user_id = @usuario AND role_id = @rol AND materia_id = @materia
            """, new NpgsqlParameter("usuario", usuario), new NpgsqlParameter("rol", rol), new NpgsqlParameter("materia", materia));
        Assert.True(carreraNula);
    }

    [Fact]
    public async Task Asignacion_revocada_puede_volver_a_otorgarse()
    {
        await using var conexion = await AbrirConexionAsync();
        var usuario = await InsertarUsuarioAsync(conexion);
        var rol = Guid.NewGuid();
        await EjecutarAsync(conexion, """
            INSERT INTO identity.roles (id, code, name, scope)
            VALUES (@rol, 'auditor_custom', 'Auditor custom', 'global')
            """, new NpgsqlParameter("rol", rol));
        await EjecutarAsync(conexion, """
            INSERT INTO identity.user_roles (user_id, role_id) VALUES (@usuario, @rol)
            """, new NpgsqlParameter("usuario", usuario), new NpgsqlParameter("rol", rol));
        await EjecutarAsync(conexion, """
            UPDATE identity.user_roles SET deleted_at = now()
            WHERE user_id = @usuario AND role_id = @rol
            """, new NpgsqlParameter("usuario", usuario), new NpgsqlParameter("rol", rol));

        await EjecutarAsync(conexion, """
            INSERT INTO identity.user_roles (user_id, role_id) VALUES (@usuario, @rol)
            """, new NpgsqlParameter("usuario", usuario), new NpgsqlParameter("rol", rol));

        Assert.Equal(2L, await EscalarAsync<long>(conexion, """
            SELECT count(*) FROM identity.user_roles
            WHERE user_id = @usuario AND role_id = @rol
            """, new NpgsqlParameter("usuario", usuario), new NpgsqlParameter("rol", rol)));
    }

    [Fact]
    public async Task Interceptor_propaga_el_usuario_al_trigger_de_auditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        var usuarioId = Guid.NewGuid();
        await using (var conexion = await AbrirConexionAsync())
        {
            await EjecutarAsync(conexion, """
                INSERT INTO identity.users (id, azure_oid, upn, display_name)
                VALUES (@id, NULL, 'auditor@unlam.edu.ar', 'Auditor')
                """, new NpgsqlParameter("id", usuarioId), new NpgsqlParameter("oid", Guid.NewGuid()));
        }

        var actual = new UsuarioActualFalso(usuarioId);
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        http.HttpContext.TraceIdentifier = "test-audit-guc";
        var opciones = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Cadena, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .AddInterceptors(new AuditDbConnectionInterceptor(actual, http))
            .Options;
        await using var db = new IdentityDbContext(opciones);
        var persona = NuevaPersona("30444555");
        db.Personas.Add(persona);
        await db.SaveChangesAsync(ct);

        var evento = await db.RegistrosDeCambio.SingleAsync(r =>
            r.NombreSchema == "identity" && r.NombreTabla == "personas" && r.ClaveFila == persona.Id.ToString(), ct);
        Assert.Equal(usuarioId, evento.CambiadoPor);
        Assert.Equal("test-audit-guc", evento.RequestId);
    }

    private static Persona NuevaPersona(string documento) => new()
    {
        Documento = documento,
        Nombre = "Ada",
        Apellido = "Lovelace",
    };

    private static async Task<Guid> InsertarUsuarioAsync(NpgsqlConnection conexion)
    {
        var id = Guid.NewGuid();
        await EjecutarAsync(conexion, """
            INSERT INTO identity.users (id, azure_oid, upn, display_name)
            VALUES (@id, NULL, @upn, 'Usuario de prueba')
            """, new NpgsqlParameter("id", id), new NpgsqlParameter("oid", Guid.NewGuid()), new NpgsqlParameter("upn", $"{id:N}@unlam.edu.ar"));
        return id;
    }

    private static async Task<(Guid Carrera, Guid Materia)> InsertarCarreraMateriaAsync(
        NpgsqlConnection conexion)
    {
        var carrera = Guid.NewGuid();
        var materia = Guid.NewGuid();
        await EjecutarAsync(conexion, """
            INSERT INTO identity.carreras (id, code, name) VALUES (@id, @code, 'Carrera test')
            """, new NpgsqlParameter("id", carrera), new NpgsqlParameter("code", $"C-{carrera:N}"));
        await EjecutarAsync(conexion, """
            INSERT INTO identity.materias (id, code, name)
            VALUES (@id, '00001', 'Materia test')
            """, new NpgsqlParameter("id", materia));
        return (carrera, materia);
    }

    private static async Task EjecutarAsync(
        NpgsqlConnection conexion, string sql, params NpgsqlParameter[] parametros)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddRange(parametros);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<T> EscalarAsync<T>(
        NpgsqlConnection conexion, string sql, params NpgsqlParameter[] parametros)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddRange(parametros);
        return (T)(await comando.ExecuteScalarAsync())!;
    }

    private sealed class UsuarioActualFalso(Guid id) : ICurrentUser
    {
        public string UserId => id.ToString();
        public string? Email => "auditor@unlam.edu.ar";
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
    }
}
