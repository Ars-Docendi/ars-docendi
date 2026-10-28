import { Link } from "react-router-dom";

import type { ArticuloAyuda } from "../tipos";
import { idPaso, rutaPaso } from "../utils/rutas";

interface ListaPasosAyudaProps {
  articulo: ArticuloAyuda;
  /** Id (`paso-N`) del paso que se está leyendo, según el scroll-spy. */
  pasoActivo?: string;
}

/** Pasos del artículo abierto, como un nivel más bajo su entrada en la sidebar. */
export function ListaPasosAyuda({ articulo, pasoActivo }: ListaPasosAyudaProps) {
  // Un artículo de un solo paso no necesita índice.
  if (articulo.pasos.length < 2) return null;
  return (
    <ol className="ayuda-sidebar-pasos" aria-label="En este artículo">
      {articulo.pasos.map((paso, indice) => {
        const id = idPaso(indice + 1);
        const activo = id === pasoActivo;
        return (
          <li key={id}>
            <Link
              to={rutaPaso(articulo.moduloSlug, articulo.slug, indice + 1)}
              className={activo ? "activo" : undefined}
              aria-current={activo ? "location" : undefined}
            >
              {paso.etiquetaIndice ?? paso.titulo}
            </Link>
          </li>
        );
      })}
    </ol>
  );
}
