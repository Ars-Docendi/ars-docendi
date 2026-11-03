import { StatusBadge } from "@ars-docendi/ui";

import type { Persona } from "../api/personasApi";
import { ETIQUETAS_ESTADO, estadoDe } from "../filtrosPersonas";

const BADGE_CHICO = { fontSize: "11px", height: "20px", padding: "0 8px" } as const;

/** Role badges shared by the table cell and the phone card. */
export function InsigniasRoles({ persona }: { persona: Persona }) {
  if (persona.roles.length === 0) {
    return (
      <span className="adoc-badge s-rechazado" style={BADGE_CHICO}>
        Sin rol
      </span>
    );
  }
  return persona.roles.map((rol) => (
    <span
      key={rol.id}
      className={rol.codigo === "jefe_catedra" ? "adoc-badge s-aprobado" : "adoc-badge s-pendiente"}
      style={BADGE_CHICO}
    >
      {rol.nombre}
    </span>
  ));
}

export function InsigniasAsignaciones({ persona }: { persona: Persona }) {
  return persona.asignaciones.map((a) => (
    <span
      key={a.id}
      className="adoc-badge s-pendiente"
      style={BADGE_CHICO}
      title={`${a.materiaNombre} · ${a.horas} h`}
    >
      {a.materiaCodigo} – {a.cargoAbreviatura || a.cargoNombre}
    </span>
  ));
}

export function InsigniaEstado({ persona }: { persona: Persona }) {
  const estado = estadoDe(persona);
  return estado === "sin_cuenta" ? (
    <StatusBadge kind="pendiente" label="Sin cuenta" />
  ) : (
    <StatusBadge
      kind={estado === "activo" ? "aprobado" : "rechazado"}
      label={ETIQUETAS_ESTADO[estado]}
    />
  );
}
