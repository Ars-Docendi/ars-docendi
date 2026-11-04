import { Fragment } from "react";
import { Link } from "react-router-dom";

import { RUTA_AYUDA } from "../utils/rutas";

export interface MigaAyuda {
  etiqueta: string;
  /** Sin `a` es la miga actual (la última). */
  a?: string;
}

/**
 * Breadcrumb de la ayuda. Usa las clases `.adoc-crumbs` de @ars-docendi/ui pero con
 * `Link` del router: el `Breadcrumbs` de la librería renderiza `<a href>` y recargaría
 * la app entera al navegar dentro de la ayuda.
 */
export function MigasAyuda({ items }: { items: readonly MigaAyuda[] }) {
  const migas: MigaAyuda[] = [
    { etiqueta: "Inicio", a: "/" },
    { etiqueta: "Centro de ayuda", a: items.length > 0 ? RUTA_AYUDA : undefined },
    ...items,
  ];
  return (
    <nav aria-label="Migas de pan" className="adoc-crumbs">
      {migas.map((miga, indice) => {
        const ultima = indice === migas.length - 1;
        return (
          <Fragment key={`${indice}-${miga.etiqueta}`}>
            {ultima ? (
              <span className="current" aria-current="page">
                {miga.etiqueta}
              </span>
            ) : (
              <Link to={miga.a ?? "/"}>{miga.etiqueta}</Link>
            )}
            {!ultima && (
              <span className="sep" aria-hidden="true">
                ›
              </span>
            )}
          </Fragment>
        );
      })}
    </nav>
  );
}
