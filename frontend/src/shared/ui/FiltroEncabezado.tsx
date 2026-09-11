import { createPortal } from "react-dom";
import { useEffect, useId, useRef, useState, type CSSProperties, type ReactNode } from "react";

import { IconoFilter } from "./iconos";
import "./FiltroEncabezado.css";

interface PosicionMenu {
  top: number;
  left: number;
}

export interface FiltroEncabezadoProps {
  etiqueta: string;
  activo: boolean;
  onLimpiar: () => void;
  children: ReactNode;
}

/** Control compartido para filtros de columna; el contenido del menú es propio de cada feature. */
export function FiltroEncabezado({ etiqueta, activo, onLimpiar, children }: FiltroEncabezadoProps) {
  const [abierto, setAbierto] = useState(false);
  const [posicion, setPosicion] = useState<PosicionMenu>({ top: 8, left: 8 });
  const botonRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const estabaAbierto = useRef(false);
  const idMenu = useId();

  function actualizarPosicion() {
    const rect = botonRef.current?.getBoundingClientRect();
    if (!rect) return;

    const escala = Number.parseFloat(getComputedStyle(document.documentElement).zoom) || 1;
    const margen = 8;
    const altoVentana = window.innerHeight / escala;
    const anchoVentana = window.innerWidth / escala;
    const alto = Math.min(420, altoVentana - margen * 2);
    const ancho = Math.min(300, anchoVentana - margen * 2);
    const left = Math.max(margen, Math.min(rect.left / escala, anchoVentana - ancho - margen));
    const debajo = rect.bottom / escala + 2;
    const arriba = rect.top / escala - alto - 2;
    const top =
      debajo + alto <= altoVentana - margen
        ? debajo
        : arriba >= margen
          ? arriba
          : Math.max(margen, altoVentana - alto - margen);

    setPosicion({ top, left });
  }

  useEffect(() => {
    if (!abierto) {
      if (estabaAbierto.current) botonRef.current?.focus();
      estabaAbierto.current = false;
      return;
    }

    estabaAbierto.current = true;
    actualizarPosicion();
    menuRef.current?.querySelector<HTMLElement>("input, button, select, textarea")?.focus();

    function cerrarPorFuera(evento: MouseEvent) {
      const objetivo = evento.target as Node;
      if (!botonRef.current?.contains(objetivo) && !menuRef.current?.contains(objetivo)) {
        setAbierto(false);
      }
    }
    function cerrarConEscape(evento: KeyboardEvent) {
      if (evento.key === "Escape") setAbierto(false);
    }
    function reposicionar() {
      actualizarPosicion();
    }

    document.addEventListener("mousedown", cerrarPorFuera);
    document.addEventListener("keydown", cerrarConEscape);
    window.addEventListener("resize", reposicionar);
    window.addEventListener("scroll", reposicionar, true);
    return () => {
      document.removeEventListener("mousedown", cerrarPorFuera);
      document.removeEventListener("keydown", cerrarConEscape);
      window.removeEventListener("resize", reposicionar);
      window.removeEventListener("scroll", reposicionar, true);
    };
  }, [abierto]);

  const estilo: CSSProperties = { top: posicion.top, left: posicion.left };

  return (
    <span className="adoc-filtro-encabezado">
      <button
        ref={botonRef}
        type="button"
        className="adoc-filtro-encabezado-trigger"
        aria-label={`Filtrar ${etiqueta}`}
        aria-haspopup="dialog"
        aria-expanded={abierto}
        aria-controls={abierto ? idMenu : undefined}
        onClick={(evento) => {
          evento.stopPropagation();
          setAbierto((valor) => !valor);
        }}
      >
        <IconoFilter />
        {activo && <span className="adoc-filtro-encabezado-indicador" aria-label="Filtro activo" />}
      </button>
      {abierto &&
        createPortal(
          <div
            ref={menuRef}
            id={idMenu}
            className="adoc-filtro-encabezado-menu"
            role="dialog"
            aria-label={`Filtro de ${etiqueta}`}
            style={estilo}
            onClick={(evento) => evento.stopPropagation()}
          >
            <div className="adoc-filtro-encabezado-titulo">{etiqueta}</div>
            {children}
            <button
              type="button"
              className="adoc-filtro-encabezado-limpiar"
              onClick={() => {
                onLimpiar();
                setAbierto(false);
              }}
            >
              Limpiar filtro
            </button>
          </div>,
          document.body,
        )}
    </span>
  );
}
