import { useRef, useState, type ReactNode } from "react";
import { Pagination } from "@ars-docendi/ui";

import { EnlaceFila } from "../navegacion/EnlaceFila";
import "./ListaTarjetas.css";
import { MenuAcciones, type AccionMenu } from "./MenuAcciones";

const POR_PAGINA = 20;

export interface Tarjeta {
  id: string;
  /** Plain-text name of the row, used for the actions button label. */
  nombre: string;
  titulo: ReactNode;
  /** Destination of the stretched title link; without it `onAbrir` runs instead (modal rows). */
  ruta?: string;
  onAbrir?: () => void;
  /** Accessible name when the title text is not enough ("Ver a Ana Pérez"). */
  etiquetaEnlace?: string;
  /** Monospace line (document, code, dates). */
  meta?: ReactNode;
  linea?: ReactNode;
  /** The module's real status badges. */
  insignias?: ReactNode;
  /** Row actions; the "⋯" button is omitted when empty. */
  acciones?: AccionMenu[];
  tinte?: "por-vencer" | "vencida";
}

interface ListaTarjetasProps {
  etiqueta: string;
  tarjetas: Tarjeta[];
  vacio: string;
  /** Changes whenever filters, search, sort or tab change: the list goes back to page 1. */
  reinicio: string;
}

/**
 * Phone rendering of a data table: a `<ul>` of cards sharing the table's rows, order and
 * handlers, paginated on the client (20 per page).
 */
export function ListaTarjetas({ etiqueta, tarjetas, vacio, reinicio }: ListaTarjetasProps) {
  const [estado, setEstado] = useState({ pagina: 1, reinicio });
  const lista = useRef<HTMLUListElement>(null);
  const cantidadPaginas = Math.max(1, Math.ceil(tarjetas.length / POR_PAGINA));
  const pagina = estado.reinicio === reinicio ? Math.min(estado.pagina, cantidadPaginas) : 1;
  const desde = (pagina - 1) * POR_PAGINA;
  const visibles = tarjetas.slice(desde, desde + POR_PAGINA);

  const cambiarPagina = (nueva: number) => {
    setEstado({ pagina: nueva, reinicio });
    lista.current?.scrollIntoView?.({ block: "start" });
  };

  if (tarjetas.length === 0) return <p className="adoc-lista-vacia">{vacio}</p>;

  return (
    <>
      <ul ref={lista} className="adoc-lista-tarjetas" aria-label={etiqueta}>
        {visibles.map((t) => (
          <li key={t.id} className={`adoc-tarjeta${t.tinte ? ` ${t.tinte}` : ""}`}>
            <div className="adoc-tarjeta-cabecera">
              {t.ruta ? (
                <EnlaceFila ruta={t.ruta} etiqueta={t.etiquetaEnlace}>
                  {t.titulo}
                </EnlaceFila>
              ) : (
                <button
                  type="button"
                  className="adoc-fila-enlace adoc-tarjeta-abrir"
                  aria-label={t.etiquetaEnlace}
                  onClick={t.onAbrir}
                >
                  {t.titulo}
                </button>
              )}
              {t.acciones && t.acciones.length > 0 && (
                <MenuAcciones acciones={t.acciones} etiquetaAria={`Acciones de ${t.nombre}`} />
              )}
            </div>
            {t.meta && <div className="adoc-tarjeta-meta">{t.meta}</div>}
            {t.linea && <div className="adoc-tarjeta-linea">{t.linea}</div>}
            {t.insignias && <div className="adoc-tarjeta-insignias">{t.insignias}</div>}
          </li>
        ))}
      </ul>
      {cantidadPaginas > 1 && (
        <Pagination
          className="adoc-lista-paginacion"
          aria-label="Paginación"
          page={pagina}
          pageCount={cantidadPaginas}
          siblingCount={0}
          onChange={cambiarPagina}
          previousLabel="Anterior"
          nextLabel="Siguiente"
          meta={`${desde + 1}–${desde + visibles.length} de ${tarjetas.length}`}
        />
      )}
    </>
  );
}
