using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ArsDocendi.Storage.Infrastructure;
using Modules.Designaciones.Infrastructure;
using Modules.Portal.Infrastructure;
using Modules.Tareas.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: Xunit.AssemblyFixture(typeof(ArsDocendi.IntegrationTests.Infraestructura.PostgresFixture))]

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("ars_docendi_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public ValueTask InitializeAsync() => new(_contenedor.StartAsync());

    public ValueTask DisposeAsync() => new(_contenedor.DisposeAsync().AsTask());

    private const string Plantilla = "plantilla_migrada";
    // Migrar una vez y clonar evita repetir todas las migraciones en cada test.
    // PostgreSQL rechaza clonar una plantilla mientras otra copia la está leyendo: se serializa.
    private readonly SemaphoreSlim _clonado = new(1, 1);
    private readonly Lock _creacionPlantilla = new();
    private Task? _plantilla;

    public async Task<string> CrearBaseMigradaAsync(string prefijo, string? migracionDesignaciones = null)
    {
        if (migracionDesignaciones is not null)
        {
            return await CrearYMigrarAsync($"{prefijo}_{Guid.NewGuid():N}", migracionDesignaciones);
        }

        lock (_creacionPlantilla)
        {
            _plantilla ??= CrearYMigrarAsync(Plantilla, null);
        }

        await _plantilla;
        var nombre = $"{prefijo}_{Guid.NewGuid():N}";
        await _clonado.WaitAsync();
        try
        {
            await EjecutarEnServidorAsync($"CREATE DATABASE \"{nombre}\" TEMPLATE \"{Plantilla}\"");
        }
        finally
        {
            _clonado.Release();
        }

        return Cadena(nombre);
    }

    private async Task EjecutarEnServidorAsync(string sql)
    {
        await using var conexion = new NpgsqlConnection(_contenedor.GetConnectionString());
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private string Cadena(string nombre) =>
        new NpgsqlConnectionStringBuilder(_contenedor.GetConnectionString())
        {
            Database = nombre,
            Pooling = false,
        }.ConnectionString;

    private async Task<string> CrearYMigrarAsync(string nombre, string? migracionDesignaciones)
    {
        await EjecutarEnServidorAsync($"CREATE DATABASE \"{nombre}\"");

        var cadena = Cadena(nombre);

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

        await using (var tareas = CrearTareas(cadena))
        {
            await tareas.Database.MigrateAsync();
        }

        await using (var designaciones = CrearDesignaciones(cadena))
        {
            await designaciones.GetService<IMigrator>().MigrateAsync(migracionDesignaciones);
        }

        return cadena;
    }

    public async Task EliminarBaseAsync(string cadena)
    {
        var nombre = new NpgsqlConnectionStringBuilder(cadena).Database
            ?? throw new InvalidOperationException("La cadena no contiene una base de datos.");

        await using var conexion = new NpgsqlConnection(_contenedor.GetConnectionString());
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)", conexion);
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
            .UseNpgsql(cadena, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DesignacionesDbContext.Schema))
            .Options;
        return new DesignacionesDbContext(opciones);
    }

    public static TareasDbContext CrearTareas(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<TareasDbContext>()
            .UseNpgsql(cadena, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", TareasDbContext.Schema))
            .Options;
        return new TareasDbContext(opciones);
    }

    public static PortalDbContext CrearPortal(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql(cadena, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PortalDbContext.Schema))
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

    /// <summary>
    /// Asignación vigente de un rol en una materia: la sesión opera con una sola, así
    /// que quien tiene el rol en varias materias elige con cuál probar.
    /// </summary>
    protected async Task<Guid> AsignacionEnMateriaAsync(Guid usuario, string rol, Guid materia)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            SELECT ur.id FROM identity.user_roles ur JOIN identity.roles r ON r.id = ur.role_id
            WHERE ur.user_id = @usuario AND ur.materia_id = @materia AND r.code = @rol
              AND ur.deleted_at IS NULL
            """, conexion);
        comando.Parameters.AddWithValue("usuario", usuario);
        comando.Parameters.AddWithValue("materia", materia);
        comando.Parameters.AddWithValue("rol", rol);
        return (Guid)(await comando.ExecuteScalarAsync())!;
    }
}
