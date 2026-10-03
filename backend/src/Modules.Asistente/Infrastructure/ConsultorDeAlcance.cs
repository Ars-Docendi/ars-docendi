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
internal sealed class ConsultorDeAlcance(AperturaDeLectura apertura) : IPerfilDelActor
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
    /// Permiso que habilita ver la consulta generada.
    /// </summary>
    /// <remarks>
    /// A diferencia del de datos personales, éste <b>no</b> se conjuga con el
    /// alcance global. El alcance acota qué filas ve el actor, y la consulta que se
    /// le muestra es la que se ejecutó con su propio alcance: verla no le agrega
    /// ninguna fila. Lo que le agrega es entender qué hizo el asistente, y eso es
    /// justamente lo que el permiso decide.
    /// </remarks>
    private const string PermisoDeVerLaConsulta = "asistente.ver_consulta";

    /// <summary>
    /// El permiso que la policy de RLS conjuga con el ámbito sobre las cuatro tablas
    /// del trámite.
    /// </summary>
    /// <remarks>
    /// Hay que leerlo acá porque <b>el ámbito solo no dice si el actor alcanza los
    /// datos</b>: la policy exige las dos cosas, así que un actor global sin este
    /// permiso ve cero filas igual que uno cuyo literal no matcheó. Ver
    /// <see cref="PerfilDelActor.AlcanzaTodo"/>.
    ///
    /// Es UN permiso porque hoy hay UN dominio con policies. Cuando haya un segundo
    /// —`portal` es el candidato inmediato— esto deja de ser un booleano y pasa a
    /// depender de qué tablas tocó la consulta.
    /// </remarks>
    private const string PermisoDeDominio = "designaciones.ver";

    /// <summary>El permiso de leer el perfil profesional de otra persona.</summary>
    /// <remarks>
    /// Se lee sólo para decidir qué anuncia la presentación. La frontera real la
    /// impone la policy de portal, que vuelve a preguntar por él en cada consulta:
    /// si este booleano quedara desactualizado, el actor vería un anuncio de más y
    /// cero filas, nunca filas de más.
    /// </remarks>
    private const string PermisoDeTrayectoriaAjena = "portal.ver_trayectoria_ajena";

    /// <summary>
    /// SQLSTATE con que PostgreSQL reporta un <c>RAISE EXCEPTION</c> de plpgsql.
    /// Es el que usa <c>identity.asistente_actor()</c> cuando el identificador no
    /// corresponde a un usuario activo.
    /// </summary>
    private const string RaiseDeLaFuncion = "P0001";

    public async Task<PerfilDelActor> ObtenerAsync(Guid actor, CancellationToken ct)
    {
        // Igual que el ejecutor: el rechazo del motor sale traducido. `ActorNoResuelto`
        // se lanza adentro y no lo toca este catch, porque ya no es una
        // PostgresException cuando llega acá.
        try
        {
            return await LeerPerfilAsync(actor, ct);
        }
        catch (PostgresException excepcion)
        {
            throw FallaDelMotor.Traducir(excepcion);
        }
    }

    /// <summary>
    /// Los seis valores del perfil, en UNA consulta.
    /// </summary>
    /// <remarks>
    /// Eran nueve viajes a la base por turno: uno por cada función de
    /// <c>identity</c> más la lectura del rol. Ahora son tres —dos del preámbulo y
    /// éste—, y el turno completo del asistente hace dos llamadas al modelo, así
    /// que seis round-trips de menos no es cosmética.
    ///
    /// <b>Ya no hay cortocircuito sobre el permiso de datos personales.</b> Antes,
    /// un actor no global no llegaba a preguntar por ese permiso; ahora la función
    /// se evalúa siempre y la conjunción se hace en C#. El resultado observable es
    /// idéntico —<c>esGlobal &amp;&amp; permiso</c> da lo mismo en los dos órdenes— y
    /// lo que se cambia es una llamada de función dentro de la misma consulta por
    /// un viaje de red entero.
    ///
    /// <c>identity.asistente_actor()</c> va primero en la lista de columnas por su
    /// efecto, no por su valor: levanta <c>P0001</c> si el identificador no
    /// corresponde a un usuario activo, y esa excepción sigue saliendo como
    /// <see cref="ActorNoResuelto"/> igual que cuando era su propia consulta.
    /// </remarks>
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

        // El acceso a datos personales exige alcance global ADEMÁS del permiso.
        // Ver la nota de IPerfilDelActor: la política de la aplicación es la
        // puerta, y el acotamiento de los datos es otra cosa que se aplica
        // después, en el controller. Sin la conjunción, el asistente heredaría la
        // puerta sin el acotamiento.
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

        // LA CONJUNCIÓN ES LA MISMA QUE HACE LA POLICY, y por eso se lee acá en vez
        // de reusar `esGlobal`. Los dos ejes son independientes: sin el permiso, el
        // ámbito global no alcanza ninguna fila, y con el permiso, un ámbito de
        // materia sigue sin alcanzar el resto. Cero filas solo significa «no hay»
        // cuando se cumplen los dos.
        // Se guardan los DOS: el permiso suelto y su conjunción con el ámbito. No es
        // redundancia — responden preguntas distintas. «¿Puede ver alguna fila del
        // trámite?» es el permiso, y sirve para anunciar el área; «¿cero filas
        // significa que no hay?» es la conjunción, que es lo que la policy exige.
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
    /// <remarks>
    /// La función ya levanta excepción con un mensaje que explica el caso. Se la
    /// envuelve en un tipo del módulo para que quien llama pueda distinguir «este
    /// actor no existe» de «la base no respondió»: el primero es un error de
    /// programación del llamador y el segundo es servicio degradado.
    /// </remarks>
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
