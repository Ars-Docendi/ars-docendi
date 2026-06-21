import type { ReactElement } from "react";

/** Vista activa del tablero de revisión: Kanban ("tablero") o tabla ("tabla"). */
export type VistaActiva = "tablero" | "tabla";

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

const ICONO_COLUMNS = (
  <IconoLucide>
    <rect width="18" height="18" x="3" y="3" rx="2" />
    <path d="M9 3v18" />
    <path d="M15 3v18" />
  </IconoLucide>
);

const ICONO_LIST = (
  <IconoLucide>
    <line x1="8" x2="21" y1="6" y2="6" />
    <line x1="8" x2="21" y1="12" y2="12" />
    <line x1="8" x2="21" y1="18" y2="18" />
    <line x1="3" x2="3.01" y1="6" y2="6" />
    <line x1="3" x2="3.01" y1="12" y2="12" />
    <line x1="3" x2="3.01" y1="18" y2="18" />
  </IconoLucide>
);

const OPCIONES: { id: VistaActiva; etiqueta: string; icono: ReactElement }[] = [
  { id: "tablero", etiqueta: "Tablero", icono: ICONO_COLUMNS },
  { id: "tabla", etiqueta: "Tabla", icono: ICONO_LIST },
];

interface SwitchVistaProps {
  vista: VistaActiva;
  onCambiar: (vista: VistaActiva) => void;
}

/** Switcher Tablero | Tabla (segmented control) para alternar la presentación del tablero de revisión. */
export function SwitchVista({ vista, onCambiar }: SwitchVistaProps) {
  return (
    <div className="adoc-vista-switch" role="group" aria-label="Vista del tablero de revisión">
      {OPCIONES.map((opcion) => (
        <button
          key={opcion.id}
          type="button"
          className={`adoc-vista-switch-btn${vista === opcion.id ? " activa" : ""}`}
          aria-pressed={vista === opcion.id}
          onClick={() => onCambiar(opcion.id)}
        >
          {opcion.icono}
          {opcion.etiqueta}
        </button>
      ))}
    </div>
  );
}
