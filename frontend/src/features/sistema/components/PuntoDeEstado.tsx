export type TonoDePunto = "positivo" | "negativo" | "advertencia" | "neutral";

interface PuntoDeEstadoProps {
  tono: TonoDePunto;
  /** Texto agregado tras la coma, p. ej. «sin problemas», «en mantenimiento». */
  etiqueta: string;
}

/**
 * Punto de color + equivalente textual oculto visualmente (design D10,
 * sistema-seccion-unificada): compone el nombre de cada pestaña de `Tabs`.
 * El punto en sí es `aria-hidden`; el nombre accesible de la pestaña lo arma
 * el texto visible más este sufijo `sr-only`.
 */
export function PuntoDeEstado({ tono, etiqueta }: PuntoDeEstadoProps) {
  return (
    <>
      <span className={`punto-estado punto-estado--${tono}`} aria-hidden="true" />
      <span className="sistema-sr-only">, {etiqueta}</span>
    </>
  );
}
