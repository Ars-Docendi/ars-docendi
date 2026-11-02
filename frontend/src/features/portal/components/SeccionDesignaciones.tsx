import { InlineAlert } from "@ars-docendi/ui";
import { SeccionPerfil } from "../../../shared/perfil/components/SeccionPerfil";
import type { DesignacionPropia } from "../api/designacionesApi";
import { useDesignacionesVigentes } from "../hooks/useDesignacionesVigentes";

const TITULO = "Designaciones vigentes";

const horasTotales = (d: DesignacionPropia) =>
  d.horas + (d.horasInvestigacion ?? 0) + (d.horasExternas ?? 0);

function detalleHoras(d: DesignacionPropia): string {
  const partes = [`${d.horas} h de materia`];
  if (d.horasInvestigacion) partes.push(`${d.horasInvestigacion} h de investigación`);
  if (d.horasExternas) partes.push(`${d.horasExternas} h externas`);
  return partes.join(" · ");
}

/** Sólo lectura: las designaciones las cambia el circuito de pedidos, no el docente. */
export function SeccionDesignaciones() {
  const consulta = useDesignacionesVigentes();

  if (consulta.isError) {
    return (
      <InlineAlert severity="danger" title="No pudimos cargar tus designaciones">
        Reintentá en unos minutos.
      </InlineAlert>
    );
  }
  if (!consulta.data) return null;
  if (consulta.data.length === 0) {
    return (
      <section className="portal-fila-vacia">
        <h2 className="portal-seccion-titulo">{TITULO}</h2>
        <span className="portal-item-detalle">Sin designaciones vigentes</span>
      </section>
    );
  }

  const total = consulta.data.reduce((suma, d) => suma + horasTotales(d), 0);
  return (
    <SeccionPerfil titulo={TITULO}>
      <ul className="portal-items">
        {consulta.data.map((d) => (
          <li className="portal-item" key={d.id}>
            <div className="portal-item-cuerpo">
              <span className="portal-item-titulo">
                {d.materia}
                {d.carrera && ` · ${d.carrera}`}
              </span>
              <span className="portal-item-detalle">
                {[d.cargo, d.dedicacion].filter(Boolean).join(" · ")}
              </span>
              <span className="portal-item-nota">{detalleHoras(d)}</span>
            </div>
          </li>
        ))}
      </ul>
      <p className="portal-item-detalle">{`Total de horas: ${total}`}</p>
    </SeccionPerfil>
  );
}
