import type { ButtonHTMLAttributes } from "react";
import { Button } from "@ars-docendi/ui";
import "./filaClickeable.css";

/** Acción "Eliminar" de una fila de grilla: siempre con texto y en color de peligro. */
export function BotonEliminarFila(props: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <Button variant="ghost" size="sm" className="adoc-accion-eliminar" {...props}>
      Eliminar
    </Button>
  );
}
