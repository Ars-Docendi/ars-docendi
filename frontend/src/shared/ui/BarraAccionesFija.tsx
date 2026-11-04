import type { ReactNode } from "react";

import "./BarraAccionesFija.css";

/**
 * Primary actions of a detail page, pinned to the bottom of the viewport on phones. On wider
 * screens it renders inline, so the same markup serves both.
 */
export function BarraAccionesFija({ children }: { children: ReactNode }) {
  return <div className="adoc-barra-acciones-fija">{children}</div>;
}
