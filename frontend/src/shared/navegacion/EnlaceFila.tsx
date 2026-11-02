import type { ReactNode } from "react";
import { Link } from "react-router-dom";

import { useEstadoVolver } from "./useEstadoVolver";

interface EnlaceFilaProps {
  ruta: string;
  /** Nombre accesible cuando el texto de la celda no alcanza ("Ver la solicitud del 12/03"). */
  etiqueta?: string;
  children: ReactNode;
}

/**
 * Celda principal de una fila clickeable: un enlace real (teclado, botón del medio, barra de
 * estado) junto al click de toda la fila. Lleva el origen para que "Volver" restaure la lista.
 */
export function EnlaceFila({ ruta, etiqueta, children }: EnlaceFilaProps) {
  const estado = useEstadoVolver();
  return (
    <Link to={ruta} state={estado} className="adoc-fila-enlace" aria-label={etiqueta}>
      {children}
    </Link>
  );
}
