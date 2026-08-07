namespace Modules.Designaciones.Domain;

/// <summary>
/// Valores admitidos por los CHECK constraints del schema <c>designaciones</c>.
/// <para>
/// Se modelan como constantes de texto y no como <c>enum</c> a propósito: son
/// exactamente las cadenas que la base valida, así que cualquier desalineación se
/// vuelve un error de compilación acá en vez de una violación de constraint en
/// runtime. La validación de dominio (qué transición es legal, qué campo es
/// obligatorio según la novedad) vive en la capa de servicios, no en estos tipos.
/// </para>
/// </summary>
public static class Novedades
{
    public const string SinNovedad = "Sin novedad";
    public const string Alta = "Alta";
    public const string Baja = "Baja";
    public const string CambioDeCargoODedicacion = "Cambio de cargo o dedicación";

    public static readonly IReadOnlySet<string> Todas =
        new HashSet<string> { SinNovedad, Alta, Baja, CambioDeCargoODedicacion };
}

/// <summary>Estados del pedido. Coincide con el CHECK <c>pedidos_estado_valido</c>.</summary>
public static class EstadosPedido
{
    public const string Borrador = "borrador";
    public const string EnRevisionCoordinador = "en_revision_coordinador";
    public const string EnRevisionSecretaria = "en_revision_secretaria";
    public const string EnRevisionDecanato = "en_revision_decanato";
    public const string Devuelto = "devuelto";
    public const string EnLote = "en_lote";
    public const string Rechazado = "rechazado";
    public const string Cancelado = "cancelado";

    /// <summary>
    /// Estados terminales: no admiten ninguna acción posterior
    /// [BR-designaciones-011]. <c>en_lote</c> es terminal para el alcance actual.
    /// </summary>
    public static readonly IReadOnlySet<string> Terminales =
        new HashSet<string> { EnLote, Rechazado, Cancelado };

    /// <summary>
    /// Estados que NO ocupan el cupo de BR-designaciones-001. Debe coincidir con el
    /// <c>WHERE</c> del índice <c>pedidos_uno_por_docente_periodo</c>: si se
    /// desalinean, el backend y la base discrepan sobre qué es un duplicado.
    /// </summary>
    public static readonly IReadOnlySet<string> NoOcupanCupo =
        new HashSet<string> { Rechazado, Cancelado };
}

/// <summary>Escala de dedicación, descendente: <c>Categoría 0</c> es la de mayor jerarquía.</summary>
public static class Dedicaciones
{
    public static readonly IReadOnlyList<string> EnOrdenDescendente =
    [
        "Categoría 0", "Categoría 1", "Categoría 2", "Categoría 3",
        "Categoría 4", "Categoría 5", "Categoría 6",
    ];

    /// <summary>
    /// Indica si <paramref name="solicitada"/> mejora estrictamente a
    /// <paramref name="actual"/>. "Mejor" es un índice estrictamente menor; no se
    /// admite igual. Devuelve <c>false</c> si alguna no pertenece a la escala.
    /// </summary>
    public static bool Mejora(string actual, string solicitada)
    {
        var indiceActual = EnOrdenDescendente.ToList().IndexOf(actual);
        var indiceSolicitada = EnOrdenDescendente.ToList().IndexOf(solicitada);

        return indiceActual >= 0 && indiceSolicitada >= 0 && indiceSolicitada < indiceActual;
    }
}

/// <summary>Tipos de baja. "Otro" exige detalle en texto libre (lo valida el servicio).</summary>
public static class TiposBaja
{
    public const string Renuncia = "Renuncia";
    public const string Jubilacion = "Jubilación";
    public const string Otro = "Otro";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string> { Renuncia, Jubilacion, Otro };
}

/// <summary>Tipos de adjunto. Coincide con el CHECK <c>pedido_adjuntos_tipo_valido</c>.</summary>
public static class TiposAdjunto
{
    public const string Cv = "cv";
    public const string DniFrente = "dni_frente";
    public const string DniDorso = "dni_dorso";
    public const string Justificativo = "justificativo";
}

/// <summary>Acciones registrables en el historial del trámite.</summary>
public static class AccionesHistorial
{
    public const string Crear = "crear";
    public const string Enviar = "enviar";
    public const string Aceptar = "aceptar";
    public const string Rechazar = "rechazar";
    public const string Devolver = "devolver";
    public const string Reenviar = "reenviar";
    public const string Editar = "editar";
    public const string Cancelar = "cancelar";
    public const string Priorizar = "priorizar";
    public const string Despriorizar = "despriorizar";
}

/// <summary>
/// Códigos de los roles de SISTEMA que participan del circuito de aprobación.
/// Coinciden con <c>identity.roles.code</c> de las filas con <c>es_sistema</c>.
/// Un rol creado por el operador nunca aparece acá — no participa del circuito.
/// </summary>
public static class RolesCircuito
{
    public const string JefeCatedra = "jefe_catedra";
    public const string CoordinadorCarrera = "coordinador_carrera";
    public const string Secretaria = "secretaria";
    public const string Decanato = "decanato";
    public const string Administrativo = "administrativo";

    /// <summary>Rol revisor que resuelve cada etapa de revisión.</summary>
    public static readonly IReadOnlyDictionary<string, string> RolPorEtapa =
        new Dictionary<string, string>
        {
            [EstadosPedido.EnRevisionCoordinador] = CoordinadorCarrera,
            [EstadosPedido.EnRevisionSecretaria] = Secretaria,
            [EstadosPedido.EnRevisionDecanato] = Decanato,
        };
}
