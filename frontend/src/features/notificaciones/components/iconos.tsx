import type { ReactNode } from "react";

/** Lucide-shaped stroke icons, `currentColor`, sized by the consuming CSS. */
function Icono({ children }: { children: ReactNode }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {children}
    </svg>
  );
}

export const IconoCampana = () => (
  <Icono>
    <path d="M10.268 21a2 2 0 0 0 3.464 0" />
    <path d="M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326" />
  </Icono>
);
export const IconoCampanaApagada = () => (
  <Icono>
    <path d="M10.268 21a2 2 0 0 0 3.464 0" />
    <path d="M17 17H4a1 1 0 0 1-.74-1.673C4.59 13.956 6 12.499 6 8a6 6 0 0 1 .258-1.742" />
    <path d="m2 2 20 20" />
    <path d="M8.668 3.01A6 6 0 0 1 18 8c0 2.687.77 4.653 1.707 6.05" />
  </Icono>
);
export const IconoCerrar = () => (
  <Icono>
    <path d="M18 6 6 18" />
    <path d="m6 6 12 12" />
  </Icono>
);
export const IconoListaCheck = () => (
  <Icono>
    <path d="m3 17 2 2 4-4" />
    <path d="m3 7 2 2 4-4" />
    <path d="M13 6h8" />
    <path d="M13 12h8" />
    <path d="M13 18h8" />
  </Icono>
);
export const IconoCheck = () => (
  <Icono>
    <path d="M20 6 9 17l-5-5" />
  </Icono>
);
export const IconoRefrescar = () => (
  <Icono>
    <path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8" />
    <path d="M21 3v5h-5" />
    <path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16" />
    <path d="M8 16H3v5" />
  </Icono>
);
export const IconoRotarIzquierda = () => (
  <Icono>
    <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
    <path d="M3 3v5h5" />
  </Icono>
);
export const IconoUsuarioCheck = () => (
  <Icono>
    <path d="M2 21a8 8 0 0 1 13.292-6" />
    <circle cx={10} cy={8} r={5} />
    <path d="m16 19 2 2 4-4" />
  </Icono>
);
export const IconoPuerta = () => (
  <Icono>
    <path d="M13 4h3a2 2 0 0 1 2 2v14" />
    <path d="M2 20h3" />
    <path d="M13 20h9" />
    <path d="M10 12v.01" />
    <path d="M13 4.562v16.157a1 1 0 0 1-1.242.97L5 20V5.562a2 2 0 0 1 1.515-1.94l4-1A2 2 0 0 1 13 4.561Z" />
  </Icono>
);
export const IconoCirculoX = () => (
  <Icono>
    <circle cx={12} cy={12} r={10} />
    <path d="m15 9-6 6" />
    <path d="m9 9 6 6" />
  </Icono>
);
export const IconoProhibido = () => (
  <Icono>
    <circle cx={12} cy={12} r={10} />
    <path d="m4.9 4.9 14.2 14.2" />
  </Icono>
);
export const IconoBandeja = () => (
  <Icono>
    <path d="M22 12h-6l-2 3h-4l-2-3H2" />
    <path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z" />
  </Icono>
);
export const IconoDevolver = () => (
  <Icono>
    <path d="M9 14 4 9l5-5" />
    <path d="M20 20v-7a4 4 0 0 0-4-4H4" />
  </Icono>
);
export const IconoMensaje = () => (
  <Icono>
    <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
  </Icono>
);
