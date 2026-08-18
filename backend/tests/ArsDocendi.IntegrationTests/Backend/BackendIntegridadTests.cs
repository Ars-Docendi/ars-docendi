using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Modules.Designaciones.Repositories;
using Modules.Designaciones.Services;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Backend;

[Collection(ColeccionPostgres.Nombre)]
public sealed class BackendIntegridadTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "backend")
{
    [Fact]
    public async Task Fallo_al_abrir_el_cambio_revierte_el_cierre_anterior()
    {
        var ct = TestContext.Current.CancellationToken;
        var datos = await PrepararCambioAsync();
        await using (var db = PostgresFixture.CrearDesignaciones(Cadena))
        {
            var repositorio = new RepositorioDesignaciones(db);
            var materializador = new MaterializadorDesignaciones(repositorio);
            var unidad = new UnidadDeTrabajo(db);
            var pedido = new Pedido
            {
                Id = datos.Pedido,
                Numero = "TEST-CAMBIO-ROLLBACK",
                PeriodoId = datos.Periodo,
                PersonaId = datos.Persona,
                MateriaId = datos.Materia,
                Novedad = Novedades.CambioDeCargoODedicacion,
                Estado = EstadosPedido.EnLote,
                CargoSolicitadoId = Guid.NewGuid(),
                Horas = 20,
            };

            await Assert.ThrowsAsync<DbUpdateException>(() => unidad.EjecutarEnTransaccionAsync(
                async ct =>
                {
                    await materializador.MaterializarAsync(pedido, ct);
                    await db.SaveChangesAsync(ct);
                }, ct));
        }

        await using var verificacion = PostgresFixture.CrearDesignaciones(Cadena);
        var anterior = await verificacion.Designaciones.SingleAsync(d => d.Id == datos.Designacion, ct);
        Assert.Null(anterior.VigenteHasta);
        Assert.Equal(1, await verificacion.Designaciones.CountAsync(d =>
            d.PersonaId == datos.Persona && d.MateriaId == datos.Materia, ct));
    }

    [Fact]
    public async Task Rol_no_sistema_no_puede_aceptar_rechazar_ni_devolver()
    {
        var ct = TestContext.Current.CancellationToken;
        var carrera = Guid.NewGuid();
        var materia = Guid.NewGuid();
        var usuario = Guid.NewGuid();
        await using (var conexion = await AbrirConexionAsync())
        {
            await EjecutarAsync(conexion, """
                INSERT INTO identity.carreras (id, code, name) VALUES (@id, @code, 'Carrera test');
                INSERT INTO identity.materias (id, code, name, carrera_id)
                    VALUES (@materia, @codigo_materia, 'Materia test', @id);
                INSERT INTO identity.users (id, azure_oid, upn, display_name)
                    VALUES (@usuario, @oid, @upn, 'Revisor custom');
                INSERT INTO identity.roles (id, code, name, scope, es_sistema)
                    VALUES (@rol, 'coordinador_custom', 'Coordinador custom', 'carrera', FALSE);
                INSERT INTO identity.user_roles (user_id, role_id, carrera_id)
                    VALUES (@usuario, @rol, @id);
                """,
                new NpgsqlParameter("id", carrera), new NpgsqlParameter("code", $"C-{carrera:N}"), new NpgsqlParameter("materia", materia),
                new NpgsqlParameter("codigo_materia", $"M-{materia:N}"), new NpgsqlParameter("usuario", usuario),
                new NpgsqlParameter("oid", Guid.NewGuid()), new NpgsqlParameter("upn", $"{usuario:N}@unlam.edu.ar"),
                new NpgsqlParameter("rol", Guid.NewGuid()));
        }

        await using var identity = PostgresFixture.CrearIdentity(Cadena);
        var consultas = new ConsultasIdentity(identity);
        var rolesSistema = await consultas.ObtenerCodigosDeRolesDeSistemaAsync(usuario, ct);
        var actor = new ActorContexto(
            usuario, rolesSistema.ToHashSet(), new HashSet<Guid>(), new HashSet<Guid> { carrera });
        var pedido = new Pedido
        {
            Numero = "TEST-ROL-CUSTOM",
            PeriodoId = Guid.NewGuid(),
            PersonaId = Guid.NewGuid(),
            MateriaId = materia,
            Novedad = Novedades.Alta,
            Estado = EstadosPedido.EnRevisionCoordinador,
        };
        AccionPedido[] acciones =
        [
            new AccionPedido.Aceptar(),
            new AccionPedido.Rechazar("No corresponde"),
            new AccionPedido.Devolver("Corregir"),
        ];

        Assert.Empty(rolesSistema);
        foreach (var accion in acciones)
        {
            Assert.Throws<ErrorDominioPedido>(() =>
                MaquinaEstadosPedido.AplicarAccion(pedido, carrera, accion, actor));
        }
    }

    private async Task<DatosCambio> PrepararCambioAsync()
    {
        var carrera = Guid.NewGuid();
        var materia = Guid.NewGuid();
        var persona = Guid.NewGuid();
        var periodo = Guid.NewGuid();
        var pedido = Guid.NewGuid();
        var designacion = Guid.NewGuid();
        await using var conexion = await AbrirConexionAsync();
        await EjecutarAsync(conexion, """
            INSERT INTO identity.carreras (id, code, name) VALUES (@carrera, @codigo_carrera, 'Carrera test');
            INSERT INTO identity.materias (id, code, name, carrera_id)
                VALUES (@materia, @codigo_materia, 'Materia test', @carrera);
            INSERT INTO identity.personas (id, documento, nombre, apellido)
                VALUES (@persona, @documento, 'Barbara', 'Liskov');
            INSERT INTO designaciones.periodos
                (id, nombre, carga_desde, carga_hasta, impacto_desde, impacto_hasta)
                VALUES (@periodo, 'Periodo test', DATE '2026-01-01', DATE '2026-02-01',
                        DATE '2026-03-01', DATE '2026-12-31');
            INSERT INTO designaciones.pedidos
                (id, numero, periodo_id, persona_id, materia_id, novedad, estado, cargo_solicitado_id, horas)
                VALUES (@pedido, 'TEST-CAMBIO-ROLLBACK', @periodo, @persona, @materia,
                        'Cambio de cargo o dedicación', 'en_lote',
                        'c3000000-0000-4000-8000-000000000003', 20);
            INSERT INTO designaciones.designaciones
                (id, persona_id, materia_id, cargo_id, horas, vigente_desde)
                VALUES (@designacion, @persona, @materia,
                        'c3000000-0000-4000-8000-000000000004', 10, DATE '2025-01-01');
            """, new NpgsqlParameter("carrera", carrera), new NpgsqlParameter("codigo_carrera", $"C-{carrera:N}"),
            new NpgsqlParameter("materia", materia), new NpgsqlParameter("codigo_materia", $"M-{materia:N}"),
            new NpgsqlParameter("persona", persona), new NpgsqlParameter("documento", $"D-{persona:N}"),
            new NpgsqlParameter("periodo", periodo), new NpgsqlParameter("pedido", pedido), new NpgsqlParameter("designacion", designacion));
        return new DatosCambio(persona, materia, periodo, pedido, designacion);
    }

    private static async Task EjecutarAsync(
        NpgsqlConnection conexion, string sql, params NpgsqlParameter[] parametros)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddRange(parametros);
        await comando.ExecuteNonQueryAsync();
    }

    private sealed record DatosCambio(
        Guid Persona,
        Guid Materia,
        Guid Periodo,
        Guid Pedido,
        Guid Designacion);
}
