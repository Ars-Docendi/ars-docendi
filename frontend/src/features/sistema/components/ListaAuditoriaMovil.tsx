import { Button } from "@ars-docendi/ui";

import "../../../shared/ui/ListaTarjetas.css";
import type { EventoAuditoria } from "../api/sistemaApi";
import { DetalleAuditoria } from "./DetalleAuditoria";

interface ListaAuditoriaMovilProps {
  registros: EventoAuditoria[];
  detallesAbiertos: Set<number>;
  fechaLocal: (valor: string) => string;
  onAlternarDetalle: (id: number) => void;
}

/**
 * Phone rendering of the audit table. The page is already paginated by the server, so this
 * does not use `ListaTarjetas` (client-side 20 per page): it only reuses its card styles.
 */
export function ListaAuditoriaMovil({
  registros,
  detallesAbiertos,
  fechaLocal,
  onAlternarDetalle,
}: ListaAuditoriaMovilProps) {
  if (registros.length === 0) return null;
  return (
    <ul className="adoc-lista-tarjetas" aria-label="Registros de auditoría">
      {registros.map((registro) => {
        const abierto = detallesAbiertos.has(registro.id);
        return (
          <li key={registro.id} className="adoc-tarjeta">
            <strong>{registro.resumen}</strong>
            <div className="adoc-tarjeta-meta">
              <time dateTime={registro.cambiadoEn}>{fechaLocal(registro.cambiadoEn)}</time>
            </div>
            <div className="adoc-tarjeta-linea">
              {registro.actor} · {registro.modulo} · {registro.objeto}
            </div>
            <div className="adoc-tarjeta-insignias">
              <span className="auditoria-accion">{registro.accionEtiqueta}</span>
            </div>
            <Button
              variant="ghost"
              size="sm"
              type="button"
              className="auditoria-detalle-boton"
              aria-expanded={abierto}
              onClick={() => onAlternarDetalle(registro.id)}
            >
              {abierto ? "Ocultar detalle" : "Ver detalle"}
            </Button>
            {abierto && <DetalleAuditoria registro={registro} />}
          </li>
        );
      })}
    </ul>
  );
}
