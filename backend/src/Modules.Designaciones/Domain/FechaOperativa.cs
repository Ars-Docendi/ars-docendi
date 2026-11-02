namespace Modules.Designaciones.Domain;

/// <summary>
/// Día operativo del circuito: el calendario de Argentina. Las vigencias se fechan con él.
/// Usar la fecha UTC corre el día entre las 21:00 y las 24:00 locales.
/// </summary>
public static class FechaOperativa
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public static DateOnly Hoy(TimeProvider reloj) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), Zona).DateTime);
}
