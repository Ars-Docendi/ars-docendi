import type { ReactNode } from "react";
import { Button } from "@ars-docendi/ui";

import "./portal.css";

interface SeccionPerfilProps {
  titulo: string;
  /**
   * Una sección sin datos se muestra como una fila, no como una tarjeta hueca.
   * Al tener contenido se expande: así la página crece con el perfil.
   */
  vacia?: boolean;
  /** Acción del encabezado (Editar / + Agregar / Reemplazar). */
  accion?: { etiqueta: string; onClick: () => void };
  children?: ReactNode;
}

/**
 * Contenedor de una sección del perfil. El bloque Perfil se renderiza sin
 * `accion`: la ausencia de afordancia es lo que comunica que es de solo
 * lectura, sin un texto que lo explique.
 */
export function SeccionPerfil({ titulo, vacia = false, accion, children }: SeccionPerfilProps) {
  if (vacia) {
    return (
      <section className="portal-fila-vacia">
        <h2 className="portal-seccion-titulo">{titulo}</h2>
        {accion && (
          <Button variant="ghost" size="sm" onClick={accion.onClick}>
            {accion.etiqueta}
          </Button>
        )}
      </section>
    );
  }

  return (
    <section className="portal-seccion">
      <div className="portal-seccion-head">
        <h2 className="portal-seccion-titulo">{titulo}</h2>
        {accion && (
          <Button variant="ghost" size="sm" onClick={accion.onClick}>
            {accion.etiqueta}
          </Button>
        )}
      </div>
      <div className="portal-seccion-body">{children}</div>
    </section>
  );
}
