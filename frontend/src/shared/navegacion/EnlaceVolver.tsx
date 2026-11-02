import { Link } from "react-router-dom";

import { ESTADO_ES_VOLVER, type DestinoVolver } from "./volver";

function IconoVolver() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      width="16"
      height="16"
    >
      <path d="m12 19-7-7 7-7" />
      <path d="M19 12H5" />
    </svg>
  );
}

/** "Volver a <destino>": un enlace real con el aspecto del botón secundario. */
export function EnlaceVolver({ destino, id }: { destino: DestinoVolver; id?: string }) {
  return (
    <Link
      id={id}
      to={destino.ruta}
      state={{ [ESTADO_ES_VOLVER]: true }}
      className="adoc-btn v-secondary adoc-enlace-volver"
    >
      <span className="adoc-ico">
        <IconoVolver />
      </span>
      {destino.texto}
    </Link>
  );
}
