using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Cada consulta del catálogo de ejemplos ejecuta contra el esquema real.
/// </summary>
/// <remarks>
/// LO QUE ESTE TEST EXISTE PARA EVITAR, y es peor que el caso equivalente de las
/// referencias de evaluación. Una referencia rota hace que la MEDICIÓN mienta; un
/// ejemplo roto se le INYECTA AL MODELO como par pregunta-SQL verificado, y le
/// enseña un join que no existe. El modelo copia la forma: un ejemplo que nombra
/// una columna equivocada produce consultas equivocadas para siempre, y el síntoma
/// no es un error sino una tasa de acierto más baja que parece del modelo.
///
/// El catálogo se llama «de ejemplos VERIFICADOS» desde que se creó. Hasta acá esa
/// palabra no la respaldaba nada.
///
/// Corre contra el seed sintético y no contra el fixture de evaluación: los
/// ejemplos viajan al prompt en producción, así que el esquema contra el que tienen
/// que ser válidos es el real.
/// </remarks>
public sealed class EjemplosEjecutablesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_ejemplos")
{
    [Fact]
    public async Task Cada_ejemplo_del_catalogo_ejecuta_contra_el_esquema()
    {
        await SembrarAsync();

        var rotos = new List<string>();

        foreach (var ejemplo in new SelectorDeEjemplos().Catalogo)
        {
            try
            {
                await using var conexion = await AbrirConexionAsync();
                await using var comando = new NpgsqlCommand(ejemplo.Sql, conexion);
                await using var lector = await comando.ExecuteReaderAsync(
                    TestContext.Current.CancellationToken);

                // Se recorre entero: un error de tipo aparece al leer, no al abrir.
                while (await lector.ReadAsync(TestContext.Current.CancellationToken))
                {
                }
            }
            catch (PostgresException error)
            {
                rotos.Add($"«{ejemplo.Pregunta}»: {error.SqlState} {error.MessageText}");
            }
        }

        Assert.True(
            rotos.Count == 0,
            "Hay ejemplos del catálogo que no ejecutan contra el esquema. Se le inyectan "
            + "al modelo como pares verificados, así que uno roto le enseña un join que no "
            + "existe:" + Environment.NewLine + string.Join(Environment.NewLine, rotos));
    }

    [Fact]
    public async Task Los_ejemplos_de_portal_devuelven_filas_contra_el_seed()
    {
        // Un ejemplo que ejecuta pero no devuelve nada le enseña al modelo una forma
        // válida sobre datos que no existen, y no hay forma de notar la diferencia
        // leyéndolo. Se exige sólo de los de portal porque son los nuevos: los de
        // designaciones ya llevan tiempo en uso y alguno puede ser legítimamente
        // vacío contra este seed.
        await SembrarAsync();

        var vacios = new List<string>();

        foreach (var ejemplo in new SelectorDeEjemplos().Catalogo
                     .Where(e => e.Sql.Contains("portal.", StringComparison.OrdinalIgnoreCase)))
        {
            await using var conexion = await AbrirConexionAsync();
            await using var comando = new NpgsqlCommand(ejemplo.Sql, conexion);
            await using var lector = await comando.ExecuteReaderAsync(
                TestContext.Current.CancellationToken);

            if (!await lector.ReadAsync(TestContext.Current.CancellationToken))
            {
                vacios.Add(ejemplo.Pregunta);
            }
        }

        Assert.True(
            vacios.Count == 0,
            "Hay ejemplos de portal que no devuelven ninguna fila contra el seed: "
            + string.Join(" · ", vacios));
    }

}
