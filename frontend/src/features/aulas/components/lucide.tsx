// ============================================================
// Iconos Lucide (subset usado por Reserva de Aulas). Paths oficiales de
// lucide.dev — viewBox 24, stroke currentColor. Tamaño controlado por CSS.
// ============================================================
import type { ReactNode } from "react";

function Svg({ children }: { children: ReactNode }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {children}
    </svg>
  );
}

export const IconoPlus = () => (
  <Svg>
    <path d="M5 12h14" />
    <path d="M12 5v14" />
  </Svg>
);

export const IconoX = () => (
  <Svg>
    <path d="M18 6 6 18" />
    <path d="m6 6 12 12" />
  </Svg>
);

export const IconoClock = () => (
  <Svg>
    <circle cx="12" cy="12" r="10" />
    <polyline points="12 6 12 12 16 14" />
  </Svg>
);

export const IconoCircleCheck = () => (
  <Svg>
    <circle cx="12" cy="12" r="10" />
    <path d="m9 12 2 2 4-4" />
  </Svg>
);

export const IconoBan = () => (
  <Svg>
    <circle cx="12" cy="12" r="10" />
    <path d="m4.9 4.9 14.2 14.2" />
  </Svg>
);

export const IconoCircleX = () => (
  <Svg>
    <circle cx="12" cy="12" r="10" />
    <path d="m15 9-6 6" />
    <path d="m9 9 6 6" />
  </Svg>
);
