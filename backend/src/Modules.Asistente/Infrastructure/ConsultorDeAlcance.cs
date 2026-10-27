using System.Data;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>Resuelve el perfil del actor con la conexión de lectura básica.</summary>
internal sealed class ConsultorDeAlcance(AperturaDeLectura apertura) : IPerfilDelActor
{
    /// <summary>Permiso que habilita la conexión con datos personales.</summary>
    private const string PermisoDeDatosPersonales = "usuarios.ver";

    /// <summary>Permiso que habilita ver la consulta generada.</summary>
    private const string PermisoDeVerLaConsulta = "asistente.ver_consulta";

    /// <summary>
    /// El permiso que la policy de RLS conjuga con el ámbito sobre las cuatro tablas del trámite.
    /// </summary>
    private const string PermisoDeDominio = "designaciones.ver";

    /// <summary>El permiso de leer el perfil profesional de otra persona.</summary>
    private const string PermisoDeTrayectoriaAjena = "portal.ver_trayectoria_ajena";

    /// <summary>SQLSTATE con que PostgreSQL reporta un <c>RAISE EXCEPTION</c> de plpgsql.</summary>
    private const string RaiseDeLaFuncion = "P0001";

    public async Task<PerfilDelActor> ObtenerAsync(Guid actor, CancellationToken ct)
    {
        // Igual que el ejecutor: el rechazo del motor sale traducido. `ActorNoResuelto` se lanza
        // adentro y no lo toca este catch, porque ya no es una PostgresException cuando llega acá.
        try
        {
            return await LeerPerfilAsync(actor, ct);
        }
        catch (PostgresException excepcion)
        {
            throw FallaDelMotor.Traducir(excepcion);
        }
    }

    /// <summary>Los seis valores del perfil, en UNA consulta.</summary>
    private async Task<PerfilDelActor> LeerPerfilAsync(Guid actor, CancellationToken ct)
    {
        const string Consulta = """
            SELECT identity.asistente_actor()                    AS actor,
                   identity.asistente_es_global()                AS es_global,
                   identity.asistente_tiene_permiso(@personales) AS ve_datos_personales,
                   identity.asistente_tiene_permiso(@consulta)   AS ve_la_consulta,
                   identity.asistente_tiene_permiso(@dominio)    AS ve_designaciones,
                   identity.asistente_tiene_permiso(@trayectoria) AS ve_trayectoria_ajena,
                   (SELECT string_agg(codigo, ',')
                      FROM (SELECT DISTINCT r.code AS codigo
                              FROM identity.user_roles ur
                              JOIN identity.roles r ON r.id = ur.role_id
                             WHERE ur.user_id = identity.asistente_actor()
                               AND ur.deleted_at IS NULL
                               AND r.is_active
                             LIMIT 2) roles)                     AS roles_vigentes
            """;

        await using var conexion = await apertura.AbrirAsync(ct);

        await using var transaccion = await conexion.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        await PreambuloDelActor.AplicarAsync(conexion, transaccion, actor, ct);

        await using var comando = new NpgsqlCommand(Consulta, conexion, transaccion);
        comando.Parameters.AddWithValue("personales", PermisoDeDatosPersonales);
        comando.Parameters.AddWithValue("consulta", PermisoDeVerLaConsulta);
        comando.Parameters.AddWithValue("dominio", PermisoDeDominio);
        comando.Parameters.AddWithValue("trayectoria", PermisoDeTrayectoriaAjena);

        await using var lector = await LeerOTraducirAsync(comando, actor, ct);

        if (!await lector.ReadAsync(ct) || await lector.IsDBNullAsync(0, ct))
        {
            throw new ActorNoResuelto(actor);
        }

        var esGlobal = lector.GetBoolean(1);

        // El acceso a datos personales exige alcance global ADEMÁS del permiso. Ver la nota de
        // IPerfilDelActor: la política de la aplicación es la puerta, y el acotamiento de los datos
        // es otra cosa que se aplica después, en el controller.
        var veDatosPersonales = esGlobal && lector.GetBoolean(2);

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
        var veLaConsulta = lector.GetBoolean(3);

        // LA CONJUNCIÓN ES LA MISMA QUE HACE LA POLICY, y por eso se lee acá en vez de reusar
        // `esGlobal`.
        var veDesignaciones = lector.GetBoolean(4);

        return new PerfilDelActor(
            esGlobal,
            veDatosPersonales,
            veLaConsulta,
            RolUnico(lector.IsDBNull(6) ? null : lector.GetString(6)),
            esGlobal && veDesignaciones,
            lector.GetBoolean(5),
            veDesignaciones);
    }

    /// <summary>
    /// Ejecuta la consulta traduciendo el <c>RAISE</c> de <c>asistente_actor()</c>.
    /// </summary>
    private static async Task<NpgsqlDataReader> LeerOTraducirAsync(
        NpgsqlCommand comando, Guid actor, CancellationToken ct)
    {
        try
        {
            return await comando.ExecuteReaderAsync(ct);
        }
        catch (PostgresException excepcion) when (excepcion.SqlState == RaiseDeLaFuncion)
        {
            throw new ActorNoResuelto(actor, excepcion);
        }
    }

    /// <summary>
    /// El código del único rol vigente del actor, o <c>null</c> si tiene varios.
    /// </summary>
    /// <remarks>
    /// <b>ES LA ÚNICA LECTURA DE ROL DE TODO EL MÓDULO, Y CONVIENE DECIR POR QUÉ SE
    /// PUEDE.</b> Las funciones de <c>identity</c> evitan a propósito nombrar
    /// cualquier código de rol: <c>identity.roles</c> no es un catálogo cerrado
    /// —Secretaría crea roles desde la aplicación— así que una lista embebida en el
    /// código falla ABIERTA y dejaría pasar por default a un rol que nadie evaluó.
    /// Esa regla protege la AUTORIZACIÓN, y sigue intacta: nada de lo que decide
    /// este consultor —alcance, datos personales, ver la consulta— mira este valor.
    /// Lo consume solamente <see cref="PresentacionPorRol"/>, para elegir el texto
    /// de bienvenida, donde no conocer un rol cae al genérico y no promete nada.
    ///
    /// Sale de <c>identity.user_roles</c> y <c>identity.roles</c>, las dos ya
    /// concedidas al rol de lectura, con los mismos filtros que
    /// <c>identity.asistente_es_global()</c>: una asignación dada de baja o un rol
    /// desactivado no cuentan.
    ///
    /// <c>DISTINCT</c> porque un Jefe de Cátedra de dos materias tiene dos
    /// asignaciones del mismo rol y eso sigue siendo un solo rol. <c>LIMIT 2</c>
    /// porque la pregunta es «¿uno solo?»: alcanza con saber si hay un segundo.
    /// </remarks>
    /// <summary>
    /// El código del único rol vigente, o <c>null</c> si el actor tiene varios.
    /// </summary>
    /// <remarks>
    /// Recibe lo que agregó la consulta: hasta dos códigos separados por coma. Una
    /// coma significa «hay un segundo», y ahí no hay un rol único que devolver.
    /// </remarks>
    private static string? RolUnico(string? codigos) =>
        codigos is null || codigos.Contains(',', StringComparison.Ordinal) ? null : codigos;

}
