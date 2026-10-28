import { Link } from "react-router-dom";

import { IsotipoArsDocendi } from "../../shared/marca/IsotipoArsDocendi";

/** Marca (isotipo + nombre) que lleva a Mi portal, el inicio de la aplicación. */
export function MarcaArsDocendi({ collapsed = false }: { collapsed?: boolean }) {
  return (
    <Link to="/portal" className="adoc-brand-link" aria-label="Ars Docendi, ir a Mi portal">
      <IsotipoArsDocendi className="mark" />
      {!collapsed && <span>Ars Docendi</span>}
    </Link>
  );
}
