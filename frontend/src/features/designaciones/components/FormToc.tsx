export interface SeccionToc {
  id: string;
  label: string;
  done?: boolean;
  error?: boolean;
}

interface FormTocProps {
  items: SeccionToc[];
  /** id de la sección actualmente activa. */
  active?: string;
}

/**
 * TOC de secciones del pedido — sticky a la izquierda, con estado por sección
 * (actual / hecho `✓` / error `!`). Composición de pantalla; no es un primitivo
 * de @ars-docendi/ui.
 */
export function FormToc({ items, active }: FormTocProps) {
  return (
    <aside className="pedido-toc">
      <div className="pedido-toc-label">Secciones del pedido</div>
      {items.map((it, i) => (
        <a
          key={it.id}
          href={`#${it.id}`}
          className={[
            active === it.id ? "current" : "",
            it.done ? "done" : "",
            it.error ? "error" : "",
          ]
            .filter(Boolean)
            .join(" ")}
        >
          <span>
            {i + 1}. {it.label}
          </span>
          <span className="step">{it.error ? "!" : it.done ? "✓" : ""}</span>
        </a>
      ))}
    </aside>
  );
}
