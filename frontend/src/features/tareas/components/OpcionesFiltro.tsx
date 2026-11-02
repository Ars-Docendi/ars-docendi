/** Lista de checkboxes para el menú de un filtro de encabezado de columna. */
export function OpcionesFiltro({
  opciones,
  valores,
  onToggle,
  etiquetas,
}: {
  opciones: string[];
  valores: string[];
  onToggle: (valor: string) => void;
  etiquetas?: Record<string, string>;
}) {
  return (
    <div className="adoc-filtro-encabezado-opciones">
      {opciones.map((opcion) => (
        <label className="adoc-filtro-encabezado-opcion" key={opcion}>
          <input
            type="checkbox"
            checked={valores.includes(opcion)}
            onChange={() => onToggle(opcion)}
          />
          {etiquetas?.[opcion] ?? opcion}
        </label>
      ))}
    </div>
  );
}
