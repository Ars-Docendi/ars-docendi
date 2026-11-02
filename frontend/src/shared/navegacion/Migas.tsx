import { Fragment } from "react";
import { Link } from "react-router-dom";

export interface Miga {
  label: string;
  /** Sin `href` es la miga actual (la última). */
  href?: string;
}

interface MigasProps {
  items: readonly Miga[];
  /** La página ya ofrece "Volver": en pantallas angostas las migas se ocultan. */
  conVolver?: boolean;
}

/**
 * Migas de pan con `Link` del router: el `Breadcrumbs` de la librería renderiza `<a href>` y
 * recargaría la aplicación entera. Mismas clases `.adoc-crumbs`.
 */
export function Migas({ items, conVolver = false }: MigasProps) {
  return (
    <nav aria-label="Migas de pan" className={`adoc-crumbs${conVolver ? " con-volver" : ""}`}>
      {items.map((miga, indice) => {
        const ultima = indice === items.length - 1;
        return (
          <Fragment key={`${indice}-${miga.label}`}>
            {ultima || !miga.href ? (
              <span
                className={ultima ? "current" : undefined}
                aria-current={ultima ? "page" : undefined}
              >
                {miga.label}
              </span>
            ) : (
              <Link to={miga.href}>{miga.label}</Link>
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
