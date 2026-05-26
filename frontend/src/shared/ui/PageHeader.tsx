import type { ReactNode } from "react";

interface PageHeaderProps {
  /** Small uppercase context line above the title (e.g. "Cuatrimestre 2026 · 1C"). */
  pretitle?: ReactNode;
  title: ReactNode;
  /** Secondary line under the title (counts, summary). */
  meta?: ReactNode;
  /** Right-aligned action buttons. */
  actions?: ReactNode;
}

/**
 * `.adoc-page-head` block from the design (pretitle / title / meta + actions).
 * Not a @ars-docendi/ui primitive — it's a page-layout piece, so it lives in
 * shared/ui for every feature page to reuse.
 */
export function PageHeader({ pretitle, title, meta, actions }: PageHeaderProps) {
  return (
    <div className="adoc-page-head">
      <div className="title-area">
        {pretitle && <div className="pretitle">{pretitle}</div>}
        <h1>{title}</h1>
        {meta && <div className="meta">{meta}</div>}
      </div>
      {actions && <div className="adoc-page-actions">{actions}</div>}
    </div>
  );
}
