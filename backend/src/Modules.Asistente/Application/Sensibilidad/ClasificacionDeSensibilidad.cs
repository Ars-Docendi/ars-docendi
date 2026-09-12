namespace Modules.Asistente.Application;

/// <summary>
/// Qué le pasa a una columna cuando el resultado va camino al proveedor del modelo.
/// </summary>
/// <remarks>
/// Es una frontera de <b>salida</b>, y es distinta de la que ya impone el motor.
/// Los <c>GRANT</c> por columna deciden quién puede <b>leer</b> qué, y eso lo
/// hace cumplir PostgreSQL. Esto decide qué <b>sale hacia un tercero</b>, y no
/// puede imponerlo el motor porque el motor no sabe qué hacemos con las filas
/// después de devolverlas.
/// </remarks>
public enum ClasificacionDeSensibilidad
{
    /// <summary>Viaja al modelo tal cual.</summary>
    Publica,

    /// <summary>
    /// Al modelo va un marcador estable; el valor real sigue viaje al llamador.
    /// </summary>
    SensibleValor,

    /// <summary>
    /// No viaja al modelo en absoluto: se suprime la columna entera, nombre
    /// incluido.
    /// </summary>
    SensibleTexto,

    /// <summary>
    /// El motor no reportó de qué columna viene, así que no se la pudo clasificar.
    /// </summary>
    /// <remarks>
    /// Pasa con toda columna que no sea una referencia directa a una columna de
    /// tabla: <c>count(*)</c>, <c>documento || ''</c>, <c>substring(telefono, 1, 4)</c>.
    ///
    /// Se trata como pública, y es una decisión tomada, no un olvido. Enmascarar
    /// todo origen desconocido rompería <c>count(*)</c>, que es la forma más común
    /// de consulta agregada, para cubrir un caso que exige que el modelo
    /// activamente envuelva una columna personal en una expresión.
    ///
    /// <b>Lo que acota el riesgo vale para cinco columnas y no para las ocho.</b>
    /// Las cinco <c>sensible-valor</c> —<c>documento</c>, <c>cuil</c>,
    /// <c>fecha_nacimiento</c>, <c>telefono</c>, <c>upn</c>— sólo son legibles con
    /// la conexión de datos personales, que exige permiso <b>y</b> alcance global:
    /// un actor sin ella no puede construir la expresión aunque quiera, porque el
    /// motor rechaza la consulta antes de ejecutarla.
    ///
    /// <b>Para las tres <c>sensible-texto</c> ese argumento es falso</b>, y estuvo
    /// escrito acá como si valiera para todas. <c>designaciones.pedidos.justificacion</c>,
    /// <c>designaciones.pedidos.tipo_baja_detalle</c> y
    /// <c>designaciones.pedido_historial.comentario</c> están concedidas a
    /// <b>los dos</b> roles: cualquier actor con acceso al trámite puede envolverlas
    /// —<c>to_jsonb(h)</c>, <c>json_agg(h)</c>, <c>lower(comentario)</c>— y el texto
    /// libre viaja al proveedor sin enmascarar.
    ///
    /// <b>Se decidió dejarlo así el 2026-09-08.</b> No es un olvido y no está
    /// mitigado: es texto que el actor ya puede leer en la pantalla del trámite, y
    /// cerrarlo revocando el GRANT apagaba una capacidad en alcance —«¿por qué se
    /// rechazó?»— que el catálogo de preguntas ofrece. Queda registrado como
    /// TD-009, que ahora dice esto mismo.
    /// </remarks>
    Desconocida,
}
