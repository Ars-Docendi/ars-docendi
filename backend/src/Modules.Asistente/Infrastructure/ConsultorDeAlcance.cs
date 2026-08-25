using System.Data;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Resuelve el perfil del actor con la conexión de lectura básica.
/// </summary>
/// <remarks>
/// Usa siempre la conexión básica, incluso cuando el turno vaya a leer datos
/// personales: la pregunta —«¿qué alcance tiene este actor?»— no depende de qué
/// columnas se van a leer después, y hacerla con el rol de menor privilegio es
/// gratis.
/// </remarks>
internal sealed class ConsultorDeAlcance(CadenaSoloLectura cadena) : IPerfilDelActor
{
    /// <summary>
    /// Permiso que habilita la conexión con datos personales.
    /// </summary>
    /// <remarks>
    /// Es el mismo permiso con que la aplicación deja ver el padrón de docentes.
    /// Se lee en vivo de <c>identity.rol_permisos</c>, así que revocarlo tiene
    /// efecto en el turno siguiente sin redesplegar nada.
    /// </remarks>
    private const string PermisoDeDatosPersonales = "usuarios.ver";

    /// <summary>
    /// SQLSTATE con que PostgreSQL reporta un <c>RAISE EXCEPTION</c> de plpgsql.
    /// Es el que usa <c>identity.asistente_actor()</c> cuando el identificador no
    /// corresponde a un usuario activo.
    /// </summary>
    private const string RaiseDeLaFuncion = "P0001";

    public async Task<PerfilDelActor> ObtenerAsync(Guid actor, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var transaccion = await conexion.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        await using (var soloLectura = new NpgsqlCommand(
            "SET TRANSACTION READ ONLY", conexion, transaccion))
        {
            await soloLectura.ExecuteNonQueryAsync(ct);
        }

        // Comandos separados y no una sola expresión con AND: PostgreSQL no
        // garantiza el orden de evaluación de los operandos, así que fijar el
        // ajuste y leerlo en la misma expresión podría leerlo antes de escribirlo,
        // y un AND que corte al primer operando falso podría saltearse la
        // validación del actor.
        await using (var fijarActor = new NpgsqlCommand(
            "SELECT set_config('app.asistente_user_id', @actor, true)",
            conexion, transaccion))
        {
            fijarActor.Parameters.AddWithValue("actor", actor.ToString());
            await fijarActor.ExecuteNonQueryAsync(ct);
        }

        await using (var validarActor = new NpgsqlCommand(
            "SELECT identity.asistente_actor()", conexion, transaccion))
        {
            object? identificado;
            try
            {
                identificado = await validarActor.ExecuteScalarAsync(ct);
            }
            catch (PostgresException excepcion) when (excepcion.SqlState == RaiseDeLaFuncion)
            {
                // La función ya levanta excepción con un mensaje que explica el
                // caso. Se la envuelve en un tipo del módulo para que quien llama
                // pueda distinguir «este actor no existe» de «la base no
                // respondió»: el primero es un error de programación del llamador
                // y el segundo es servicio degradado.
                throw new ActorNoResuelto(actor, excepcion);
            }

            if (identificado is null or DBNull)
            {
                throw new ActorNoResuelto(actor);
            }
        }

        var esGlobal = await LeerBooleanoAsync(
            conexion, transaccion, "SELECT identity.asistente_es_global()", ct);

        // El acceso a datos personales exige alcance global ADEMÁS del permiso.
        // Ver la nota de IPerfilDelActor: la política de la aplicación es la
        // puerta, y el acotamiento de los datos es otra cosa que se aplica
        // después, en el controller. Sin la conjunción, el asistente heredaría la
        // puerta sin el acotamiento.
        var veDatosPersonales = esGlobal && await LeerBooleanoAsync(
            conexion, transaccion, "SELECT identity.asistente_tiene_permiso(@permiso)", ct,
            ("permiso", PermisoDeDatosPersonales));

        // RIESGO RESIDUAL ACEPTADO, y registrado a propósito donde se toma la
        // decisión y no solo en un documento.
        //
        // La conjunción de arriba cierra el acceso a documento, CUIL, teléfono y
        // fecha de nacimiento para todo actor que no sea global. Lo que NO cierra:
        // un actor de ámbito de materia o de carrera sigue pudiendo listar nombre,
        // apellido y legajo de TODO el padrón, porque esas tres columnas se
        // conceden también al rol básico y `identity.personas` no tiene RLS — las
        // policies del asistente cubren únicamente las cuatro tablas de
        // `designaciones`.
        //
        // Se aceptó porque son datos que ya circulan en cualquier listado de
        // cátedra. El cierre completo es una policy propia sobre `identity.personas`
        // que acote por el alcance del actor, y tiene su propio ticket de
        // endurecimiento (ARS-69); no se adelanta acá porque es una migración con
        // impacto sobre consumidores que no son el asistente.
        return new PerfilDelActor(esGlobal, veDatosPersonales);
    }

    private static async Task<bool> LeerBooleanoAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction transaccion,
        string sql,
        CancellationToken ct,
        params (string Nombre, object Valor)[] parametros)
    {
        await using var comando = new NpgsqlCommand(sql, conexion, transaccion);

        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        return await comando.ExecuteScalarAsync(ct) is true;
    }
}
