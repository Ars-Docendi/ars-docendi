import { Link } from "react-router-dom";

import { arrowRightIcon, fileTextIcon } from "../../../app/shell/icons";
import { destinoDe, tipoConFicha } from "../utils/destinos";
import type { PrincipalDeLaRespuesta } from "../types";

type Tono = "exito" | "aviso" | "peligro" | "neutro";

/**
 * El tono lo decide el cliente a partir del código de `estado`; la etiqueta, en
 * cambio, la pone Designaciones (D2). Un código que no se conoce queda neutro.
 */
const TONO_POR_ESTADO: Record<string, Tono> = {
  en_lote: "exito",
  en_revision_coordinador: "aviso",
  en_revision_secretaria: "aviso",
  en_revision_decanato: "aviso",
  devuelto: "peligro",
  rechazado: "peligro",
};

const CLASE_DEL_TONO: Record<Tono, string> = {
  exito: "adoc-asistente-ficha-estado adoc-asistente-ficha-estado--exito",
  aviso: "adoc-asistente-ficha-estado adoc-asistente-ficha-estado--aviso",
  peligro: "adoc-asistente-ficha-estado adoc-asistente-ficha-estado--peligro",
  neutro: "adoc-asistente-ficha-estado adoc-asistente-ficha-estado--neutro",
};

function tonoDe(estado: string | undefined): Tono {
  return estado !== undefined && Object.hasOwn(TONO_POR_ESTADO, estado)
    ? TONO_POR_ESTADO[estado]
    : "neutro";
}

/** `2026-03-01T15:00:00Z` → `01/03/2026`, en la zona horaria de quien mira. */
function fechaCorta(iso: string | undefined): string | undefined {
  if (!iso) return undefined;
  const fecha = new Date(iso);
  if (Number.isNaN(fecha.getTime())) return undefined;

  return new Intl.DateTimeFormat("es-AR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(fecha);
}

interface FichaDelElementoProps {
  principal: PrincipalDeLaRespuesta;
}

/**
 * La ficha del elemento principal de una respuesta: de qué trata, en qué estado está, sus datos
 * clave y el botón que lo abre (asistente-respuestas-con-vinculos, D2/D3/D8).
 */
export function FichaDelElemento({ principal }: FichaDelElementoProps) {
  const destino = destinoDe(principal.tipo);
  if (!destino || !tipoConFicha(principal.tipo)) return null;

  const { datos } = principal;
  const campos: Array<[string, string | undefined]> = [
    ["Novedad", datos.novedad],
    ["Período", datos.periodo],
    ["Cargo", datos.cargo],
    ["Dedicación", datos.dedicacion],
    ["Última actividad", fechaCorta(datos.ultimaActividad)],
  ];

  // `div` y no `header`/`footer`: dentro del modal, la ui-lib y `asistente.css`
  // estilizan esos elementos como la cabecera y el pie del propio modal.
  return (
    <article className="adoc-asistente-ficha">
      <div className="adoc-asistente-ficha-cabecera">
        <span className="adoc-asistente-ficha-icono" aria-hidden="true">
          {fileTextIcon}
        </span>

        <div className="adoc-asistente-ficha-titulos">
          <span className="adoc-asistente-ficha-eyebrow">Pedido de designación</span>
          <p className="adoc-asistente-ficha-titulo">
            {datos.numero} · {datos.materia}
          </p>
        </div>

        {datos.estadoEtiqueta && (
          <span className={CLASE_DEL_TONO[tonoDe(datos.estado)]}>{datos.estadoEtiqueta}</span>
        )}
      </div>

      <dl className="adoc-asistente-ficha-datos">
        {campos.map(([etiqueta, valor]) =>
          valor ? (
            <div key={etiqueta} className="adoc-asistente-ficha-dato">
              <dt>{etiqueta}</dt>
              <dd>{valor}</dd>
            </div>
          ) : null,
        )}
      </dl>

      <div className="adoc-asistente-ficha-pie">
        <Link
          className="adoc-asistente-ficha-abrir"
          to={destino.ruta(principal.id)}
          data-vinculo-del-asistente=""
        >
          Abrir pedido
          <span className="adoc-asistente-ficha-flecha" aria-hidden="true">
            {arrowRightIcon}
          </span>
        </Link>
      </div>
    </article>
  );
}
