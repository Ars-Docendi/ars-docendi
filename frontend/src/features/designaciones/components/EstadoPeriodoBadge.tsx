import type { EstadoPeriodo } from "../types";

interface EstadoPeriodoBadgeProps {
  estado: EstadoPeriodo;
}

const CONFIG: Record<EstadoPeriodo, { bg: string; fg: string; etiqueta: string }> = {
  abierto: {
    bg: "var(--color-status-success-bg)",
    fg: "var(--color-status-success-fg)",
    etiqueta: "Abierto",
  },
  proximo: {
    bg: "var(--color-status-warning-bg)",
    fg: "var(--color-status-warning-fg)",
    etiqueta: "Próximo",
  },
  cerrado: {
    bg: "var(--color-status-neutral-bg)",
    fg: "var(--color-status-neutral-fg)",
    etiqueta: "Cerrado",
  },
};

export function EstadoPeriodoBadge({ estado }: EstadoPeriodoBadgeProps) {
  const { bg, fg, etiqueta } = CONFIG[estado];
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: "var(--space-1)",
        padding: "2px var(--space-2)",
        borderRadius: "var(--radius-pill)",
        background: bg,
        color: fg,
        fontSize: "var(--text-micro-size)",
        fontWeight: "var(--weight-medium)",
        lineHeight: "var(--text-micro-lh)",
        whiteSpace: "nowrap",
      }}
    >
      <span
        style={{
          width: 6,
          height: 6,
          borderRadius: "var(--radius-pill)",
          background: "currentColor",
          flexShrink: 0,
        }}
        aria-hidden="true"
      />
      {etiqueta}
    </span>
  );
}
