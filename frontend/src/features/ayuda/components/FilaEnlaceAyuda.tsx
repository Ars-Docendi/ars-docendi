import type { ReactNode } from "react";
import { Link } from "react-router-dom";

interface FilaEnlaceAyudaProps {
  a: string;
  titulo: string;
  descripcion: string;
  /** Línea secundaria sobre la descripción (p. ej. el módulo de un resultado). */
  etiqueta?: ReactNode;
}

/** Fila navegable: título, descripción e indicador de navegación. */
export function FilaEnlaceAyuda({ a, titulo, descripcion, etiqueta }: FilaEnlaceAyudaProps) {
  return (
    <li>
      <Link to={a} className="ayuda-fila">
        <span className="ayuda-fila-texto">
          {etiqueta && <span className="ayuda-fila-etiqueta">{etiqueta}</span>}
          <span className="ayuda-fila-titulo">{titulo}</span>
          <span className="ayuda-fila-descripcion">{descripcion}</span>
        </span>
        <span className="ayuda-fila-chevron" aria-hidden="true">
          ›
        </span>
      </Link>
    </li>
  );
}
