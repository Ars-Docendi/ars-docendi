import type { ReactElement } from "react";
import type { Novedad } from "../types";
import { etiquetaNovedad } from "./tableroRevisionModelo";

/** Clase de color del chip según la novedad (mapea a los tonos del design system). */
const CLASE_POR_NOVEDAD: Record<Novedad, string> = {
  "Sin novedad": "sin-novedad",
  Alta: "alta",
  Baja: "baja",
  "Cambio de cargo o dedicación": "cambio",
};

/** Wrapper de icono Lucide (viewBox 24, stroke currentColor) — tal cual el screens.pen. */
function IconoLucide({ children }: { children: ReactElement | ReactElement[] }) {
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

// Iconos Lucide exactos del diseño.
const ICONO_REFRESH = (
  <IconoLucide>
    <path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8" />
    <path d="M21 3v5h-5" />
    <path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16" />
    <path d="M8 16H3v5" />
  </IconoLucide>
);

const ICONO_USER_PLUS = (
  <IconoLucide>
    <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
    <circle cx="9" cy="7" r="4" />
    <line x1="19" x2="19" y1="8" y2="14" />
    <line x1="22" x2="16" y1="11" y2="11" />
  </IconoLucide>
);

const ICONO_USER_MINUS = (
  <IconoLucide>
    <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
    <circle cx="9" cy="7" r="4" />
    <line x1="22" x2="16" y1="11" y2="11" />
  </IconoLucide>
);

const ICONO_POR_NOVEDAD: Record<Novedad, ReactElement> = {
  "Sin novedad": ICONO_REFRESH,
  Alta: ICONO_USER_PLUS,
  Baja: ICONO_USER_MINUS,
  "Cambio de cargo o dedicación": ICONO_REFRESH,
};

/** Chip de novedad (Alta / Baja / Cambio) con icono Lucide y color de estado. */
export function NovedadChip({ novedad }: { novedad: Novedad }) {
  return (
    <span className={`adoc-novedad-chip ${CLASE_POR_NOVEDAD[novedad]}`}>
      {ICONO_POR_NOVEDAD[novedad]}
      {etiquetaNovedad(novedad)}
    </span>
  );
}
