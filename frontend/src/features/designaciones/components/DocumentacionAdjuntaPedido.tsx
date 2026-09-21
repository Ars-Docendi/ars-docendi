import { useState } from "react";
import { descargarAdjuntoPedido } from "../api/pedidosApi";
import type { Adjunto, TipoAdjunto } from "../types";

const ETIQUETA_ADJUNTO: Record<TipoAdjunto, string> = {
  cv: "CV",
  dni_frente: "DNI (frente)",
  dni_dorso: "DNI (dorso)",
  justificativo: "Justificativo",
};

interface DocumentacionAdjuntaPedidoProps {
  pedidoId: string;
  adjuntos: Adjunto[];
}

function estaDisponible(adjunto: Adjunto): boolean {
  return (
    Boolean(adjunto.archivoId) &&
    (adjunto.estadoArchivo === undefined || adjunto.estadoArchivo === "disponible")
  );
}

export function DocumentacionAdjuntaPedido({
  pedidoId,
  adjuntos,
}: DocumentacionAdjuntaPedidoProps) {
  const [abriendoId, setAbriendoId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function abrirAdjunto(adjunto: Adjunto) {
    if (!estaDisponible(adjunto) || !adjunto.archivoId) return;

    setAbriendoId(adjunto.id);
    setError(null);

    try {
      const contenido = await descargarAdjuntoPedido(pedidoId, adjunto.archivoId);
      const url = URL.createObjectURL(contenido);
      const ventana = window.open(url, "_blank", "noopener,noreferrer");

      if (!ventana) {
        URL.revokeObjectURL(url);
        throw new Error("El navegador bloqueó la nueva pestaña.");
      }

      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch {
      setError(`No se pudo abrir ${ETIQUETA_ADJUNTO[adjunto.tipo]}: ${adjunto.nombre}.`);
    } finally {
      setAbriendoId(null);
    }
  }

  return (
    <div className="adoc-adjuntos">
      <p className="adoc-eyebrow">Documentación adjunta</p>
      {error && (
        <p className="adoc-adjuntos-error" role="alert">
          {error} <span>Podés reintentar.</span>
        </p>
      )}
      <ul className="adoc-adjuntos-list">
        {adjuntos.map((adjunto) => {
          const etiqueta = ETIQUETA_ADJUNTO[adjunto.tipo];
          const disponible = estaDisponible(adjunto);
          const abriendo = abriendoId === adjunto.id;

          return (
            <li key={adjunto.id} className="adoc-adjunto">
              <span className="adoc-adjunto-tipo">{etiqueta}</span>
              <span className="adoc-adjunto-nombre">{adjunto.nombre}</span>
              {disponible ? (
                <button
                  type="button"
                  className="adoc-adjunto-abrir"
                  aria-label={`Abrir ${etiqueta}: ${adjunto.nombre}`}
                  aria-busy={abriendo}
                  disabled={abriendo}
                  onClick={() => void abrirAdjunto(adjunto)}
                >
                  {abriendo ? "Abriendo…" : "Abrir"}
                </button>
              ) : (
                <span className="adoc-adjunto-legacy">Metadata histórica</span>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
