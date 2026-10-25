import { useQuery } from "@tanstack/react-query";

import { listarAuditoria, type EventoAuditoria } from "../api/auditoriaApi";
import { horaCortaEnZona } from "../utils/zonaHoraria";

interface CambiosDelModeloProps {
  onAbrirCambio: (idEvento: string) => void;
  onVerTodo: () => void;
}

const MAXIMO_VISIBLE = 5;

/**
 * Los tipos de acción del backend que son del modelo (asistente-modelo-en-caliente,
 * `AdministracionDelModelo`). El feed de auditoría los entrega con el `tipo`
 * como `tabla`, así que es por ahí que se los reconoce.
 */
const TEXTO_POR_TIPO: Record<string, string> = {
  "modelo.seleccionar": "Modelo del asistente cambiado",
  "modelo.restablecer": "Modelo del asistente: vuelta a la configuración del servidor",
  "credencial.crear": "Clave de una empresa guardada",
  "credencial.reemplazar": "Clave de una empresa reemplazada",
};

function esDelModelo(evento: EventoAuditoria): boolean {
  return evento.tabla in TEXTO_POR_TIPO;
}

/**
 * Los últimos eventos del modelo y de sus claves. El feed no filtra por varios
 * tipos a la vez, así que se piden dos búsquedas acotadas al módulo del
 * asistente («modelo» y «credencial») y se quedan los eventos conocidos. La
 * clave de la query cuelga de `["modelo"]`: cambiar el modelo desde la pestaña
 * invalida este listado sin que las dos features se conozcan.
 */
export function CambiosDelModelo({ onAbrirCambio, onVerTodo }: CambiosDelModeloProps) {
  const consulta = useQuery({
    queryKey: ["modelo", "cambios"],
    queryFn: async () => {
      const [delModelo, deLasClaves] = await Promise.all(
        ["modelo", "credencial"].map((q) =>
          listarAuditoria({ pagina: 1, tamanoPagina: MAXIMO_VISIBLE * 2, modulo: "asistente", q }),
        ),
      );
      // Un mismo evento puede matchear las dos búsquedas: se cuenta una vez.
      const porId = new Map(
        [...delModelo.elementos, ...deLasClaves.elementos].map((e) => [e.id, e]),
      );
      return [...porId.values()]
        .filter(esDelModelo)
        .sort((a, b) => b.cambiadoEn.localeCompare(a.cambiadoEn))
        .slice(0, MAXIMO_VISIBLE);
    },
    refetchOnWindowFocus: false,
  });

  const ahora = new Date();
  const elementos = consulta.data ?? [];

  return (
    <section className="sistema-cambios-recientes" aria-labelledby="modelo-cambios-titulo">
      <header className="sistema-cambios-encabezado">
        <span id="modelo-cambios-titulo" className="sistema-cambios-titulo">
          Cambios del modelo
        </span>
        <button type="button" className="sistema-enlace" onClick={onVerTodo}>
          Ver todo en Auditoría →
        </button>
      </header>

      {consulta.isPending && <p role="status">Cargando cambios del modelo…</p>}

      {consulta.isError && (
        <div className="sistema-cambios-vacio" role="alert">
          <p>No se pudieron cargar los cambios del modelo.</p>
          <button type="button" className="sistema-enlace" onClick={() => void consulta.refetch()}>
            Reintentar
          </button>
        </div>
      )}

      {consulta.data && elementos.length === 0 && (
        <p className="sistema-cambios-vacio">Todavía no hay cambios del modelo registrados.</p>
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
                  <span className="sistema-cambios-texto">{TEXTO_POR_TIPO[evento.tabla]}</span>
                  <span className="sistema-cambios-actor"> · {evento.actor}</span>
                </span>
                <span className={`sistema-chip-accion sistema-chip-accion--${evento.accion}`}>
                  {evento.accionEtiqueta}
                </span>
                <svg
                  width="14"
                  height="14"
                  viewBox="0 0 24 24"
                  aria-hidden="true"
                  focusable="false"
                >
                  <path
                    d="M9 6l6 6-6 6"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
