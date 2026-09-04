using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auditing;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

[Collection(ColeccionPostgres.Nombre)]
public sealed class IdentityPersistenciaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "identity")
{
    [Fact]
    public async Task Persona_sin_cuenta_y_sin_legajo_se_persiste()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var persona = NuevaPersona("30111222");

        db.Personas.Add(persona);
        await db.SaveChangesAsync(ct);

        Assert.Null(persona.Legajo);
        Assert.False(await db.Usuarios.AnyAsync(u => u.PersonaId == persona.Id, ct));
    }

    [Fact]
    public async Task Primer_login_vincula_la_cuenta_sin_duplicar_la_persona()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var persona = NuevaPersona("30222333");
        db.Personas.Add(persona);
        await db.SaveChangesAsync(ct);

        var vinculador = new VinculadorPrimerLogin(db);
        var oid = Guid.NewGuid();
        var usuario = await vinculador.VincularAsync(new DatosPrimerLogin(
            oid, "docente@unlam.edu.ar", "Docente Ejemplo", persona.Documento), ct);
        await vinculador.VincularAsync(new DatosPrimerLogin(
            oid, "docente@unlam.edu.ar", "Docente Actualizado", persona.Documento), ct);

        Assert.Equal(persona.Id, usuario.PersonaId);
        Assert.Equal(1, await db.Personas.CountAsync(p => p.Documento == persona.Documento, ct));
        Assert.Equal(1, await db.Usuarios.CountAsync(u => u.PersonaId == persona.Id, ct));
        Assert.Equal("Docente Actualizado", usuario.NombreParaMostrar);
    }

    [Fact]
    public async Task Documento_duplicado_es_rechazado()
    {
        await using var conexion = await AbrirConexionAsync();
        await InsertarPersonaAsync(conexion, "30333444");

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => InsertarPersonaAsync(conexion, "30333444"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("personas_documento_key", error.ConstraintName);
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
    public async Task Rol_de_sistema_protege_codigo_y_scope_pero_admite_nombre()
    {
        await using var conexion = await AbrirConexionAsync();
        var codigo = await Assert.ThrowsAsync<PostgresException>(() =>
            EjecutarAsync(conexion, "UPDATE identity.roles SET code = 'secretaria_2' WHERE code = 'secretaria'"));
        var scope = await Assert.ThrowsAsync<PostgresException>(() =>
            EjecutarAsync(conexion, "UPDATE identity.roles SET scope = 'materia' WHERE code = 'secretaria'"));

        await EjecutarAsync(
            conexion,
            "UPDATE identity.roles SET name = 'Secretaría Académica editada' WHERE code = 'secretaria'");

        Assert.Equal(PostgresErrorCodes.RaiseException, codigo.SqlState);
        Assert.Equal(PostgresErrorCodes.RaiseException, scope.SqlState);
        Assert.Equal(
            "Secretaría Académica editada",
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

        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
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
    public async Task Permiso_duplicado_en_un_rol_es_rechazado()
    {
        await using var conexion = await AbrirConexionAsync();
        var rol = Guid.NewGuid();
        await EjecutarAsync(conexion, """
            INSERT INTO identity.roles (id, code, name, scope)
            VALUES (@rol, 'permisos_custom', 'Permisos custom', 'global')
            """, new NpgsqlParameter("rol", rol));
        var permiso = await EscalarAsync<Guid>(
            conexion, "SELECT id FROM identity.permisos ORDER BY code LIMIT 1");
        await EjecutarAsync(conexion, """
            INSERT INTO identity.rol_permisos (rol_id, permiso_id) VALUES (@rol, @permiso)
            """, new NpgsqlParameter("rol", rol), new NpgsqlParameter("permiso", permiso));

        var error = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(conexion, """
            INSERT INTO identity.rol_permisos (rol_id, permiso_id) VALUES (@rol, @permiso)
            """, new NpgsqlParameter("rol", rol), new NpgsqlParameter("permiso", permiso)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("rol_permisos_pkey", error.ConstraintName);
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
                VALUES (@id, @oid, 'auditor@unlam.edu.ar', 'Auditor')
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

    private static async Task InsertarPersonaAsync(NpgsqlConnection conexion, string documento) =>
        await EjecutarAsync(conexion, """
            INSERT INTO identity.personas (documento, nombre, apellido)
            VALUES (@documento, 'Ada', 'Lovelace')
            """, new NpgsqlParameter("documento", documento));

    private static async Task<Guid> InsertarUsuarioAsync(NpgsqlConnection conexion)
    {
        var id = Guid.NewGuid();
        await EjecutarAsync(conexion, """
            INSERT INTO identity.users (id, azure_oid, upn, display_name)
            VALUES (@id, @oid, @upn, 'Usuario de prueba')
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
            INSERT INTO identity.materias (id, code, name, carrera_id)
            VALUES (@id, @code, 'Materia test', @carrera)
            """, new NpgsqlParameter("id", materia), new NpgsqlParameter("code", $"M-{materia:N}"), new NpgsqlParameter("carrera", carrera));
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
