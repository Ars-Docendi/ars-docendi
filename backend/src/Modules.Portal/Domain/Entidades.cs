namespace Modules.Portal.Domain;

public sealed class Perfil
{
    public Guid Id { get; set; }
    public Guid PersonaId { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public Contacto? Contacto { get; set; }
    public Cv? Cv { get; set; }
    public ICollection<Experiencia> Experiencias { get; set; } = [];
    public ICollection<Educacion> Educaciones { get; set; } = [];
    public ICollection<Certificacion> Certificaciones { get; set; } = [];
    public ICollection<Proyecto> Proyectos { get; set; } = [];
    public ICollection<DocenteHabilidad> Habilidades { get; set; } = [];
}

public sealed class Contacto
{
    public Guid Id { get; set; }
    public Guid PerfilId { get; set; }
    public string? Telefono { get; set; }
    public string? Mail { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class Cv
{
    public Guid Id { get; set; }
    public Guid PerfilId { get; set; }
    public required string Nombre { get; set; }
    public DateTimeOffset FechaCarga { get; set; }
    public string? Uri { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public abstract class ItemPerfil
{
    public Guid Id { get; set; }
    public Guid PerfilId { get; set; }
    public DateOnly Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class Experiencia : ItemPerfil
{
    public required string Puesto { get; set; }
    public required string Organizacion { get; set; }
    public required string Descripcion { get; set; }
}

public sealed class Educacion : ItemPerfil
{
    public required string Nivel { get; set; }
    public required string Carrera { get; set; }
    public required string Institucion { get; set; }
}

public sealed class Certificacion
{
    public Guid Id { get; set; }
    public Guid PerfilId { get; set; }
    public required string Nombre { get; set; }
    public required string Emisor { get; set; }
    public DateOnly Fecha { get; set; }
    public DateOnly? Vencimiento { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class Proyecto : ItemPerfil
{
    public required string Nombre { get; set; }
    public required string Rol { get; set; }
    public required string Descripcion { get; set; }
    public string? Doi { get; set; }
    public DocumentoProyecto? Documento { get; set; }
}

public sealed class DocumentoProyecto
{
    public Guid Id { get; set; }
    public Guid ProyectoId { get; set; }
    public required string Nombre { get; set; }
    public DateTimeOffset FechaCarga { get; set; }
    public string? Uri { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class Habilidad
{
    public Guid Id { get; set; }
    public required string Termino { get; set; }
    public required string TerminoNorm { get; set; }
    public bool Sugerido { get; set; }
    public Guid? CanonicaId { get; set; }
    public int Usos { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class DocenteHabilidad
{
    public Guid PerfilId { get; set; }
    public Guid HabilidadId { get; set; }
    public required string Tipo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public Habilidad? Habilidad { get; set; }
}
