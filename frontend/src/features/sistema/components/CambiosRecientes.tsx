import { useQuery } from "@tanstack/react-query";

import { listarAuditoria } from "../api/auditoriaApi";
import { horaCortaEnZona } from "../utils/zonaHoraria";

interface CambiosRecientesProps {
  onAbrirCambio: (idEvento: string) => void;
  onVerTodo: () => void;
}

const CHEVRON = (
  <svg width="14" height="14" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
    <path
      d="M9 6l6 6-6 6"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

/**
 * «Cambios recientes» bajo las tarjetas de Estado (requisito «Recent changes
 * on the Estado tab», design D12): los últimos 4 eventos del feed unificado,
 * sin filtro de período, sólo con `auditoria.ver`.
 */
export function CambiosRecientes({ onAbrirCambio, onVerTodo }: CambiosRecientesProps) {
  const consulta = useQuery({
    queryKey: ["administracion", "auditoria", "recientes"],
    queryFn: () => listarAuditoria({ pagina: 1, tamanoPagina: 4 }),
    refetchOnWindowFocus: false,
  });

  const ahora = new Date();
  const elementos = consulta.data?.elementos ?? [];

  return (
    <section className="sistema-cambios-recientes" aria-labelledby="sistema-cambios-titulo">
      <header className="sistema-cambios-encabezado">
        <span id="sistema-cambios-titulo" className="sistema-cambios-titulo">
          Cambios recientes
        </span>
        <button type="button" className="sistema-enlace" onClick={onVerTodo}>
          Ver todo en Auditoría →
        </button>
      </header>

      {consulta.isPending && <p role="status">Cargando cambios recientes…</p>}

      {consulta.data && elementos.length === 0 && (
        <p className="sistema-cambios-vacio">Todavía no hay cambios registrados.</p>
      )}

      {elementos.length > 0 && (
        <ul className="sistema-cambios-lista">
          {elementos.map((evento) => (
            <li key={evento.id}>
              <button
                type="button"
                className="sistema-cambios-fila"
                onClick={() => onAbrirCambio(evento.id)}
              >
                <span className="sistema-cambios-hora">
                  {horaCortaEnZona(new Date(evento.cambiadoEn), ahora)}
                </span>
                <span className="sistema-cambios-resumen">
                  <span className="sistema-cambios-texto">{evento.resumen}</span>
                  <span className="sistema-cambios-actor"> · {evento.actor}</span>
                </span>
                <span className={`sistema-chip-accion sistema-chip-accion--${evento.accion}`}>
                  {evento.accionEtiqueta}
                </span>
                {CHEVRON}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
