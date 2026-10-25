import { useQuery } from "@tanstack/react-query";

import { listarAuditoria } from "../api/auditoriaApi";
import { horaCortaEnZona } from "../utils/zonaHoraria";

interface CambiosDelModeloProps {
  onAbrirCambio: (idEvento: string) => void;
  onVerTodo: () => void;
}

const MAXIMO_VISIBLE = 5;

/**
 * La tabla con la que el feed de auditoría marca los eventos del modelo y de sus
 * claves (`modelo.*` y `credencial.*` en `FuenteAuditoriaAsistente`). El feed
 * filtra por una sola tabla, así que una sola consulta alcanza.
 */
const TABLA_DEL_MODELO = "modelo_del_asistente";

/**
 * Los últimos eventos del modelo y de sus claves, con las etiquetas y el
 * resumen que arma el propio feed. La clave de la query cuelga de `["modelo"]`:
 * cambiar el modelo desde la pestaña invalida este listado sin que las dos
 * features se conozcan.
 */
export function CambiosDelModelo({ onAbrirCambio, onVerTodo }: CambiosDelModeloProps) {
  const consulta = useQuery({
    queryKey: ["modelo", "cambios"],
    queryFn: () =>
      listarAuditoria({
        pagina: 1,
        tamanoPagina: MAXIMO_VISIBLE,
        modulo: "asistente",
        tabla: TABLA_DEL_MODELO,
      }),
    refetchOnWindowFocus: false,
  });

  const ahora = new Date();
  const elementos = consulta.data?.elementos ?? [];

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
                  <span className="sistema-cambios-texto">{evento.resumen}</span>
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
