export interface ErrorValidacionPedido {
  campo: string;
  mensaje: string;
}

const ETIQUETAS_CAMPOS: Record<string, string> = {
  cargosolicitadoid: "Cargo solicitado",
  dedicacionsolicitadaid: "Dedicación solicitada",
  horas: "Horas",
  horasdocencia: "Horas de docencia",
  horasinvestigacion: "Horas de investigación",
  horasexternas: "Horas externas",
  tipobaja: "Tipo de baja",
  personaid: "Docente",
  justificacion: "Justificación",
  documentacion: "Documentación",
  adjuntos: "Documentación adjunta",
  materias: "Materias",
  novedad: "Novedad",
  periodoid: "Período",
  catedraid: "Cátedra",
  carreraid: "Carrera",
  vigentedesde: "Fecha de vigencia",
};

function esRegistro(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === "object" && valor !== null && !Array.isArray(valor);
}

/** Extrae errores ProblemDetails sin exponer nombres internos de campos desconocidos. */
export function erroresValidacionPedido(error: unknown): ErrorValidacionPedido[] {
  if (!esRegistro(error) || !esRegistro(error.response)) return [];
  const { data } = error.response;
  if (!esRegistro(data) || !esRegistro(data.errors)) return [];

  return Object.entries(data.errors).flatMap(([clave, valores]) => {
    if (!Array.isArray(valores)) return [];
    const normalizada = clave.replace(/[^a-z0-9]/gi, "").toLowerCase();
    const campo = ETIQUETAS_CAMPOS[normalizada] ?? "Datos del pedido";

    return valores
      .filter(
        (mensaje): mensaje is string => typeof mensaje === "string" && mensaje.trim().length > 0,
      )
      .map((mensaje) => ({ campo, mensaje }));
  });
}
