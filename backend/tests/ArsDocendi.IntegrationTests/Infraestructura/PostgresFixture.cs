using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ArsDocendi.Storage.Infrastructure;
using Modules.Designaciones.Infrastructure;
using Modules.Portal.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: Xunit.AssemblyFixture(typeof(ArsDocendi.IntegrationTests.Infraestructura.PostgresFixture))]

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class PostgresFixture : IAsyncLifetime
{
    private const string BaseExternaEsperada = "arsdocendi_dev";
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _basesTemporales = new();
    private PostgreSqlContainer? _contenedor;
    private string _cadenaServidor = string.Empty;
    private bool _modoServidorExterno;

    // Sólo se expone para ensayos de dump/restore en el contenedor descartable del fixture.
    public string? ContenedorId => _contenedor?.Id;

    public async ValueTask InitializeAsync()
    {
        var externa = CrearCadenaServidorExterno();
        if (externa is not null)
        {
            if (!string.Equals(externa.Database, BaseExternaEsperada, StringComparison.Ordinal))
                throw new InvalidOperationException($"El fixture externo sólo admite la base de mantenimiento {BaseExternaEsperada}.");
            _cadenaServidor = externa.ConnectionString;
            _modoServidorExterno = true;
            await VerificarServidorExternoAsync(_cadenaServidor);
            return;
        }

        _contenedor = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("ars_docendi_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await _contenedor.StartAsync();
        _cadenaServidor = _contenedor.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_modoServidorExterno)
        {
            Exception? errorLimpieza = null;
            foreach (var nombre in _basesTemporales.Keys.OrderBy(n => n, StringComparer.Ordinal))
            {
                try
                {
                    await EliminarBaseEnServidorAsync(nombre, forzar: false);
                    _basesTemporales.TryRemove(nombre, out _);
                }
                catch (Exception error)
                {
                    errorLimpieza ??= error;
                }
            }
            if (errorLimpieza is not null)
                throw new InvalidOperationException("No se pudieron limpiar todas las bases temporales externas.", errorLimpieza);
        }

        if (_contenedor is not null)
            await _contenedor.DisposeAsync();
    }

    private static NpgsqlConnectionStringBuilder? CrearCadenaServidorExterno()
    {
        var host = Environment.GetEnvironmentVariable("ARS_DOCENDI_TEST_DB_HOST");
        var port = Environment.GetEnvironmentVariable("ARS_DOCENDI_TEST_DB_PORT");
        var database = Environment.GetEnvironmentVariable("ARS_DOCENDI_TEST_DB_NAME");
        var username = Environment.GetEnvironmentVariable("ARS_DOCENDI_TEST_DB_USER");
        var password = Environment.GetEnvironmentVariable("ARS_DOCENDI_TEST_DB_PASSWORD");
        var configurados = new[] { host, port, database, username, password }.Count(v => !string.IsNullOrWhiteSpace(v));
        if (configurados == 0) return null;
        if (configurados != 5)
            throw new InvalidOperationException("La conexión externa de pruebas requiere host, puerto, base, usuario y contraseña.");
        if (!int.TryParse(port, out var puerto) || puerto is < 1 or > 65535)
            throw new InvalidOperationException("El puerto de PostgreSQL de pruebas no es válido.");

        return new NpgsqlConnectionStringBuilder
        {
            Host = host!, Port = puerto, Database = database!, Username = username!,
            Password = password!, Pooling = false, Timeout = 15, CommandTimeout = 60
        };
    }

    private static async Task VerificarServidorExternoAsync(string cadena)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand("""
            SELECT current_database() = @esperada
               AND (SELECT rolsuper OR rolcreatedb FROM pg_roles WHERE rolname = current_user)
            """, conexion);
        comando.Parameters.AddWithValue("esperada", BaseExternaEsperada);
        if ((bool?)await comando.ExecuteScalarAsync() != true)
            throw new InvalidOperationException("La conexión externa no está en la base esperada o no puede crear bases temporales.");
    }

    public async Task<string> CrearBaseMigradaAsync(string prefijo, string? migracionDesignaciones = null)
    {
        var nombre = _modoServidorExterno
            ? $"arsdocendi_dev_audit_test_{Guid.NewGuid():N}"
            : $"{prefijo}_{Guid.NewGuid():N}";
        if (nombre.Length > 63 || nombre.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
            throw new InvalidOperationException("El nombre generado para la base temporal no es válido.");
        await using (var conexion = new NpgsqlConnection(_cadenaServidor))
        {
            await conexion.OpenAsync();
            await using var comando = new NpgsqlCommand($"CREATE DATABASE \"{nombre}\"", conexion);
            await comando.ExecuteNonQueryAsync();
        }
        _basesTemporales.TryAdd(nombre, 0);

        var cadena = new NpgsqlConnectionStringBuilder(_cadenaServidor)
        {
            Database = nombre,
            Pooling = false,
        }.ConnectionString;

        try
        {
            await using (var identity = CrearIdentity(cadena))
            {
                await identity.Database.MigrateAsync();
            }

            await using (var almacenamiento = CrearAlmacenamiento(cadena))
            {
                await almacenamiento.Database.MigrateAsync();
            }

            await using (var portal = CrearPortal(cadena))
            {
                await portal.Database.MigrateAsync();
            }

            await using (var designaciones = CrearDesignaciones(cadena))
            {
                await designaciones.GetService<IMigrator>().MigrateAsync(migracionDesignaciones);
            }

            return cadena;
        }
        catch
        {
            await EliminarBaseAsync(cadena);
            throw;
        }
    }

    public async Task EliminarBaseAsync(string cadena)
    {
        var nombre = new NpgsqlConnectionStringBuilder(cadena).Database
            ?? throw new InvalidOperationException("La cadena no contiene una base de datos.");
        if (!_basesTemporales.ContainsKey(nombre))
            throw new InvalidOperationException("El fixture sólo puede eliminar bases temporales creadas por esta instancia.");

        if (_modoServidorExterno) return;

        await EliminarBaseEnServidorAsync(nombre, forzar: true);
        _basesTemporales.TryRemove(nombre, out _);
    }

    private async Task EliminarBaseEnServidorAsync(string nombre, bool forzar)
    {
        if (nombre.Length > 63 || nombre.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
            throw new InvalidOperationException("El nombre de base temporal no es válido.");
        await using var conexion = new NpgsqlConnection(_cadenaServidor);
        await conexion.OpenAsync();
        var forceClause = forzar ? " WITH (FORCE)" : string.Empty;
        await using var comando = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{nombre}\"{forceClause}", conexion);
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

    public static PortalDbContext CrearPortal(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql(cadena)
            .Options;
        return new PortalDbContext(opciones);
    }

    public static AlmacenamientoDbContext CrearAlmacenamiento(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<AlmacenamientoDbContext>()
            .UseNpgsql(cadena, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AlmacenamientoDbContext.Schema))
            .Options;
        return new AlmacenamientoDbContext(opciones);
    }
}

public abstract class ClasePostgresAislada(PostgresFixture postgres, string prefijo) : IAsyncLifetime
{
    protected PostgresFixture Postgres => postgres;
    protected string Cadena { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        Cadena = await postgres.CrearBaseMigradaAsync(prefijo);
    }

    public async ValueTask DisposeAsync()
    {
        if (Cadena.Length > 0)
        {
            await postgres.EliminarBaseAsync(Cadena);
        }
    }

    protected async Task<NpgsqlConnection> AbrirConexionAsync()
    {
        var conexion = new NpgsqlConnection(Cadena);
        await conexion.OpenAsync();
        return conexion;
    }
}
