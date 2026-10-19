import type { ReactNode } from "react";

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
  return (
    <div className="adoc-page-head">
      <div className="title-area">
        <h1>{title}</h1>
        {meta && <div className="meta">{meta}</div>}
      </div>
      {actions && <div className="adoc-page-actions">{actions}</div>}
    </div>
  );
}
