using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Infrastructure;
using Modules.Portal.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArsDocendi.IntegrationTests.Infraestructura;

[CollectionDefinition(Nombre)]
public sealed class ColeccionPostgres : ICollectionFixture<PostgresFixture>
{
    public const string Nombre = "PostgreSQL 18";
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("ars_docendi_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public ValueTask InitializeAsync() => new(_contenedor.StartAsync());

    public ValueTask DisposeAsync() => new(_contenedor.DisposeAsync().AsTask());

    public async Task<string> CrearBaseMigradaAsync(string prefijo)
    {
        var nombre = $"{prefijo}_{Guid.NewGuid():N}";
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

        await using (var portal = CrearPortal(cadena))
        {
            await portal.Database.MigrateAsync();
        }

        await using (var designaciones = CrearDesignaciones(cadena))
        {
            await designaciones.Database.MigrateAsync();
        }

        return cadena;
    }

    public async Task EliminarBaseAsync(string cadena)
    {
        var nombre = new NpgsqlConnectionStringBuilder(cadena).Database
            ?? throw new InvalidOperationException("La cadena no contiene una base de datos.");

        NpgsqlConnection.ClearAllPools();
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
}

public abstract class ClasePostgresAislada(PostgresFixture postgres, string prefijo) : IAsyncLifetime
{
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
