using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Asistente.Infrastructure;
using Modules.Designaciones.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArsDocendi.IntegrationTests.Infraestructura;

[CollectionDefinition(Nombre)]
public sealed class ColeccionPostgres : ICollectionFixture<PostgresFixture>
{
    public const string Nombre = "PostgreSQL 18";
}

/// <summary>
/// Base de prueba aislada, con los roles del asistente ya creados sobre ella.
/// </summary>
public sealed record BaseDePrueba(
    string Cadena,
    string RolSoloLectura,
    string RolSoloLecturaPii,
    string Password);

public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Password de los roles del asistente en el contenedor efímero de test.</summary>
    private const string PasswordDeRol = "asistente-de-prueba";

    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("ars_docendi_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public ValueTask InitializeAsync() => new(_contenedor.StartAsync());

    public ValueTask DisposeAsync() => new(_contenedor.DisposeAsync().AsTask());

    /// <summary>
    /// Crea una base migrada y, sobre ella, los dos roles de solo lectura del
    /// asistente con sus privilegios ya aplicados.
    /// </summary>
    /// <remarks>
    /// Los roles se crean acá y no en <c>provision-db.sh</c> porque ese script no
    /// corre en los tests: la base de prueba nace de las migraciones EF. Llevan
    /// sufijo único porque los roles son objetos de CLUSTER y varias clases de
    /// test comparten el contenedor.
    ///
    /// Se les dan los mismos atributos que en producción —en particular
    /// <c>NOBYPASSRLS</c>— para que lo que se prueba acá sea lo que se despliega.
    /// </remarks>
    public async Task<BaseDePrueba> CrearBaseMigradaAsync(string prefijo)
    {
        var identificador = Guid.NewGuid();
        var nombre = $"{prefijo}_{identificador:N}";
        var sufijoRol = identificador.ToString("N")[..8];
        var rolSoloLectura = $"asistente_ro_t{sufijoRol}";
        var rolSoloLecturaPii = $"asistente_ro_pii_t{sufijoRol}";
        await using (var conexion = new NpgsqlConnection(_contenedor.GetConnectionString()))
        {
            await conexion.OpenAsync();
            await using var comando = new NpgsqlCommand($"CREATE DATABASE \"{nombre}\"", conexion);
            await comando.ExecuteNonQueryAsync();
        }

        var cadena = new NpgsqlConnectionStringBuilder(_contenedor.GetConnectionString())
        {
            Database = nombre,
            Pooling = false,
        }.ConnectionString;

        await using (var identity = CrearIdentity(cadena))
        {
            await identity.Database.MigrateAsync();
        }

        await using (var designaciones = CrearDesignaciones(cadena))
        {
            await designaciones.Database.MigrateAsync();
        }

        await CrearRolesDelAsistenteAsync(nombre, rolSoloLectura, rolSoloLecturaPii);

        // Los GRANT se aplican con el MISMO código que el migrador del módulo, no
        // con una copia del script: si acá se probara una copia, la prueba diría
        // que la copia funciona.
        await using (var conexion = new NpgsqlConnection(cadena))
        {
            await conexion.OpenAsync();
            await PrivilegiosAsistente.AplicarAsync(
                conexion, rolSoloLectura, rolSoloLecturaPii, CancellationToken.None);
        }

        return new BaseDePrueba(cadena, rolSoloLectura, rolSoloLecturaPii, PasswordDeRol);
    }

    public async Task EliminarBaseAsync(BaseDePrueba baseDePrueba)
    {
        var nombre = new NpgsqlConnectionStringBuilder(baseDePrueba.Cadena).Database
            ?? throw new InvalidOperationException("La cadena no contiene una base de datos.");

        NpgsqlConnection.ClearAllPools();
        await using var conexion = new NpgsqlConnection(_contenedor.GetConnectionString());
        await conexion.OpenAsync();
        await EjecutarAsync(conexion, $"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)");

        // Los roles sobreviven al DROP DATABASE: son de cluster. Sin esta baja, cada
        // clase de test dejaría dos roles colgados en el contenedor compartido.
        foreach (var rol in new[] { baseDePrueba.RolSoloLectura, baseDePrueba.RolSoloLecturaPii })
        {
            await EjecutarAsync(conexion, $"DROP ROLE IF EXISTS \"{rol}\"");
        }
    }

    private async Task CrearRolesDelAsistenteAsync(string baseDeDatos, params string[] roles)
    {
        await using var conexion = new NpgsqlConnection(_contenedor.GetConnectionString());
        await conexion.OpenAsync();
        foreach (var rol in roles)
        {
            await EjecutarAsync(conexion,
                $"""
                CREATE ROLE "{rol}" WITH LOGIN PASSWORD '{PasswordDeRol}'
                    NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT
                """);

            // Igual que en producción: nombres calificados obligatorios.
            await EjecutarAsync(conexion,
                $"ALTER ROLE \"{rol}\" IN DATABASE \"{baseDeDatos}\" SET search_path = ''");
        }
    }

    private static async Task EjecutarAsync(NpgsqlConnection conexion, string sql)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    public static IdentityDbContext CrearIdentity(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(cadena, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .Options;
        return new IdentityDbContext(opciones);
    }

    public static DesignacionesDbContext CrearDesignaciones(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<DesignacionesDbContext>()
            .UseNpgsql(cadena)
            .Options;
        return new DesignacionesDbContext(opciones);
    }
}

public abstract class ClasePostgresAislada(PostgresFixture postgres, string prefijo) : IAsyncLifetime
{
    private BaseDePrueba? _base;

    protected string Cadena => _base?.Cadena ?? string.Empty;

    /// <summary>Rol de solo lectura del asistente, sin datos personales.</summary>
    protected string RolSoloLectura => _base?.RolSoloLectura ?? string.Empty;

    /// <summary>Rol de solo lectura del asistente, con datos personales.</summary>
    protected string RolSoloLecturaPii => _base?.RolSoloLecturaPii ?? string.Empty;

    public async ValueTask InitializeAsync()
    {
        _base = await postgres.CrearBaseMigradaAsync(prefijo);
    }

    public async ValueTask DisposeAsync()
    {
        if (_base is not null)
        {
            await postgres.EliminarBaseAsync(_base);
            _base = null;
        }
    }

    protected async Task<NpgsqlConnection> AbrirConexionAsync()
    {
        var conexion = new NpgsqlConnection(Cadena);
        await conexion.OpenAsync();
        return conexion;
    }

    /// <summary>
    /// Abre una conexión autenticada como uno de los dos roles del asistente. Es
    /// la única forma honesta de probar un límite que impone el motor: consultarlo
    /// desde el dueño de la base no prueba nada.
    /// </summary>
    protected async Task<NpgsqlConnection> AbrirConexionComoAsistenteAsync(bool conDatosPersonales)
    {
        var actual = _base ?? throw new InvalidOperationException("La base de prueba no está inicializada.");
        var cadena = new NpgsqlConnectionStringBuilder(actual.Cadena)
        {
            Username = conDatosPersonales ? actual.RolSoloLecturaPii : actual.RolSoloLectura,
            Password = actual.Password,
            Pooling = false,
        }.ConnectionString;

        var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync();
        return conexion;
    }
}
