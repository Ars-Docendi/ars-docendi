import type { ReactNode } from "react";

import { IconoChevronRight } from "../../../shared/ui/iconos";
import "./portal.css";

interface FilaDatoProps {
  icono: ReactNode;
  /** El dato en sí: es el titular de la fila. */
  valor: ReactNode;
  /** Qué es ese dato: va como bajada, en segundo plano. */
  etiqueta: string;
  /** Sin valor cargado todavía. */
  vacio?: boolean;
  /** Si se pasa, la fila entera es el control para editar ese campo. */
  onEditar?: () => void;
}

/**
 * Una fila de dato del perfil. El **valor** es el titular y la **etiqueta** la
 * bajada: el docente entra a ver sus datos, no a leer los nombres de los campos.
 *
 * Cuando el campo se edita, la fila entera es la afordancia y termina en un
 * chevron; cuando no, no hay chevron ni nada que sugiera que abre algo.
 */
export function FilaDato({ icono, valor, etiqueta, vacio = false, onEditar }: FilaDatoProps) {
  const cuerpo = (
    <>
      <span className="portal-fila-icono">{icono}</span>
      <span className="portal-fila-cuerpo">
        <span className={vacio ? "portal-fila-valor portal-fila-vacio" : "portal-fila-valor"}>
          {vacio ? "Sin cargar" : valor}
        </span>
        <span className="portal-fila-etiqueta">{etiqueta}</span>
      </span>
      {onEditar && (
        <span className="portal-fila-chevron">
          <IconoChevronRight />
        </span>
      )}
    </>
  );

  if (onEditar) {
    return (
      <button
        type="button"
        className="portal-fila portal-fila-accion"
        onClick={onEditar}
        aria-label={`Editar ${etiqueta.toLowerCase()}`}
      >
        {cuerpo}
      </button>
    );
  }

  return <div className="portal-fila">{cuerpo}</div>;
}
