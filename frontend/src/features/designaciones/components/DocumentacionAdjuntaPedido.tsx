import { useState } from "react";
import { IconoExternalLink, IconoFileText } from "../../../shared/ui/iconos";
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

function extensionDe(nombre: string): string {
  const extension = nombre.split(".").pop()?.trim();
  return extension && extension !== nombre ? extension.toUpperCase() : "ARCHIVO";
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

    let ventana: Window | null = null;
    let urlTemporal: string | null = null;
    try {
      ventana = window.open("", "_blank");
      if (!ventana) throw new Error("El navegador bloqueó la nueva pestaña.");
      ventana.opener = null;

      const contenido = await descargarAdjuntoPedido(pedidoId, adjunto.archivoId);
      urlTemporal = URL.createObjectURL(contenido);
      ventana.location.href = urlTemporal;
      window.setTimeout(() => {
        if (urlTemporal) URL.revokeObjectURL(urlTemporal);
      }, 60_000);
    } catch {
      if (urlTemporal) URL.revokeObjectURL(urlTemporal);
      ventana?.close();
      setError(`No se pudo abrir ${ETIQUETA_ADJUNTO[adjunto.tipo]}: ${adjunto.nombre}.`);
    } finally {
      setAbriendoId(null);
    }
  }

  return (
    <div className="adoc-adjuntos">
      <div className="adoc-adjuntos-head">
        <p className="adoc-eyebrow">Documentación adjunta</p>
        <span className="adoc-adjuntos-count">
          {adjuntos.length} {adjuntos.length === 1 ? "archivo" : "archivos"}
        </span>
      </div>
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
          const extension = extensionDe(adjunto.nombre);

          const contenido = (
            <>
              <span className="adoc-adjunto-icon">
                <IconoFileText />
                <span className="adoc-adjunto-extension">{extension}</span>
              </span>
              <span className="adoc-adjunto-info">
                <span className="adoc-adjunto-nombre" title={adjunto.nombre}>
                  {adjunto.nombre}
                </span>
                <span className="adoc-adjunto-meta">
                  {etiqueta} · {extension}
                </span>
              </span>
            </>
          );

          return (
            <li key={adjunto.id}>
              {disponible ? (
                <button
                  type="button"
                  className="adoc-adjunto adoc-adjunto--disponible"
                  aria-label={`${abriendo ? "Abriendo" : "Abrir"} adjunto ${etiqueta}: ${adjunto.nombre}`}
                  aria-busy={abriendo}
                  disabled={abriendo}
                  onClick={() => void abrirAdjunto(adjunto)}
                >
                  {contenido}
                  <span className="adoc-adjunto-action" aria-hidden="true">
                    {abriendo ? "Abriendo…" : <IconoExternalLink />}
                  </span>
                </button>
              ) : (
                <div className="adoc-adjunto adoc-adjunto--legacy">
                  {contenido}
                  <span className="adoc-adjunto-estado">Metadata histórica</span>
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
