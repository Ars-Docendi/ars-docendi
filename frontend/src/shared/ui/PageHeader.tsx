import { useState, type ReactNode } from "react";

import { useTituloDePagina } from "../navegacion/pagina";

interface PageHeaderProps {
  title: ReactNode;
  /** Secondary line under the title (counts, summary). */
  meta?: ReactNode;
  /** Right-aligned action buttons. */
  actions?: ReactNode;
}

/**
 * Bloque `.adoc-page-head` del diseño (título / meta + acciones). No lleva pretitle:
 * la sección ya la indica el breadcrumb. No es un primitivo de @ars-docendi/ui sino
 * una pieza de layout de página, por eso vive en shared/ui.
 */
export function PageHeader({ title, meta, actions }: PageHeaderProps) {
  // El `h1` recibe el foco al navegar (`tabIndex={-1}`) y es la fuente del título del documento.
  const [h1, setH1] = useState<HTMLHeadingElement | null>(null);
  useTituloDePagina(h1);
  return (
    <div className="adoc-page-head">
      <div className="title-area">
        <h1 ref={setH1} tabIndex={-1}>
          {title}
        </h1>
        {meta && <div className="meta">{meta}</div>}
      </div>
      {actions && <div className="adoc-page-actions">{actions}</div>}
    </div>
  );
}
