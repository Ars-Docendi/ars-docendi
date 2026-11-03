import type { CambioAuditoria, EventoAuditoria } from "../api/sistemaApi";

function mostrarValor(cambio: CambioAuditoria, lado: "anterior" | "nuevo"): string {
  if (cambio.oculto) return "Enmascarado por política";
  const valor = lado === "anterior" ? cambio.valorAnterior : cambio.valorNuevo;
  return valor ?? "—";
}

/** Detalle expandido de un evento; lo comparten la tabla y las tarjetas. */
export function DetalleAuditoria({ registro }: { registro: EventoAuditoria }) {
  return (
    <section className="auditoria-detalle" aria-label={`Detalle del evento ${registro.id}`}>
      <dl className="auditoria-metadatos">
        <div>
          <dt>Tabla</dt>
          <dd>
            {registro.schema}.{registro.tabla}
          </dd>
        </div>
        <div>
          <dt>Clave de fila</dt>
          <dd>{registro.rowPk}</dd>
        </div>
        <div>
          <dt>Solicitud</dt>
          <dd>{registro.requestId ?? "—"}</dd>
        </div>
      </dl>
      {registro.cambios.length > 0 && (
        <div className="auditoria-detalle-tabla">
          <table aria-label={`Cambios del registro ${registro.id}`}>
            <thead>
              <tr>
                <th scope="col">Campo</th>
                <th scope="col">Anterior</th>
                <th scope="col">Nuevo</th>
              </tr>
            </thead>
            <tbody>
              {registro.cambios.map((cambio) => (
                <tr key={cambio.campo}>
                  <th scope="row">{cambio.etiquetaCampo}</th>
                  <td>{mostrarValor(cambio, "anterior")}</td>
                  <td>{mostrarValor(cambio, "nuevo")}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
