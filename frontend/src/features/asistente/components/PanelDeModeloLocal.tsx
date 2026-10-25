import { Select } from "@ars-docendi/ui";

import { nombreDelServidor } from "../utils/modeloDelAsistente";

interface PanelDeModeloLocalProps {
  servidores: string[];
  servidor: string | null;
  modelos: string[];
  modelo: string | null;
  onServidor: (servidor: string) => void;
  onModelo: (modelo: string) => void;
}

const ICONO_AYUDA = (
  <svg
    width="16"
    height="16"
    viewBox="0 0 20 20"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.5"
    aria-hidden="true"
  >
    <circle cx="10" cy="10" r="8" />
    <path d="M8 8a2 2 0 1 1 2.8 1.8c-.5.3-.8.7-.8 1.2v.5M10 14v.5" />
  </svg>
);

/**
 * El modelo local (tarea 4.5): un servidor de la lista cerrada que devuelve el
 * backend, por nombre, y un modelo del catálogo. No hay campo de URL ni de
 * credencial: las credenciales de un servidor viven en su configuración, y el
 * tooltip lo dice.
 */
export function PanelDeModeloLocal({
  servidores,
  servidor,
  modelos,
  modelo,
  onServidor,
  onModelo,
}: PanelDeModeloLocalProps) {
  return (
    <div className="adoc-asistente-modelo-campos">
      <div className="adoc-field">
        <span className="adoc-asistente-modelo-etiqueta">
          <label className="adoc-label" htmlFor="modelo-servidor-local">
            Servidor
            <span className="req" aria-hidden="true">
              *
            </span>
          </label>
          <span className="adoc-asistente-modelo-ayuda">
            <button
              type="button"
              className="adoc-asistente-modelo-ayuda-boton"
              aria-label="Ayuda sobre el servidor"
              aria-describedby="modelo-ayuda-servidor"
            >
              {ICONO_AYUDA}
            </button>
            <span
              id="modelo-ayuda-servidor"
              role="tooltip"
              className="adoc-asistente-modelo-ayuda-burbuja"
            >
              Sólo aparecen los servidores habilitados en la configuración del servidor. Si alguno
              pide credenciales, se configuran ahí junto al servidor, no en esta pantalla.
            </span>
          </span>
        </span>
        <Select
          id="modelo-servidor-local"
          value={servidor ?? ""}
          onChange={(e) => onServidor(e.target.value)}
          aria-describedby="modelo-servidor-local-pista"
        >
          {servidores.map((s) => (
            <option key={s} value={s}>
              {nombreDelServidor(s)}
            </option>
          ))}
        </Select>
        <div id="modelo-servidor-local-pista" className="adoc-hint">
          Sólo los servidores habilitados en la configuración del servidor.
        </div>
      </div>

      <div className="adoc-field">
        <label className="adoc-label" htmlFor="modelo-local">
          Modelo
          <span className="req" aria-hidden="true">
            *
          </span>
        </label>
        <Select
          id="modelo-local"
          value={modelo ?? ""}
          onChange={(e) => onModelo(e.target.value)}
          disabled={modelos.length === 0}
          aria-describedby="modelo-local-pista"
        >
          {modelos.map((m) => (
            <option key={m} value={m}>
              {m}
            </option>
          ))}
        </Select>
        <div id="modelo-local-pista" className="adoc-hint">
          {modelos.length === 0
            ? "El catálogo no tiene modelos para este servidor."
            : "Los modelos del catálogo y el que ya usa este servidor."}
        </div>
      </div>
    </div>
  );
}
