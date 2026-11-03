interface ProcedenciaDeLosDatosProps {
  /** Códigos de schema que mandó el backend (`origenes`). */
  origenes: string[];
}

/** Cómo se nombra cada área para el usuario. Un código que no está acá se omite. */
const AREAS: Record<string, string> = {
  designaciones: "Designaciones",
  portal: "Portal",
  identity: "Institucional",
};

/**
 * «Datos de Designaciones»: de qué áreas salen los datos de la respuesta
 * (asistente-respuestas-con-vinculos, D4).
 */
export function ProcedenciaDeLosDatos({ origenes }: ProcedenciaDeLosDatosProps) {
  const nombres = [
    ...new Set(origenes.flatMap((codigo) => (Object.hasOwn(AREAS, codigo) ? [AREAS[codigo]] : []))),
  ];
  if (nombres.length === 0) return null;

  return <p className="adoc-asistente-procedencia">Datos de {nombres.join(" · ")}</p>;
}
