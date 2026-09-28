import { Checkbox, MonthYearPicker } from "@ars-docendi/ui";

import type { MesSinAnio } from "./mesSinAnio";
import "./portal.css";

interface SelectorFechaProps {
  etiqueta: string;
  valor: string;
  requerido?: boolean;
  error?: string;
  deshabilitado?: boolean;
  onChange: (fecha: string, faltaAnio: boolean) => void;
}

/** Mes (opcional) + año. El mes solo hace falta para desempatar dentro del año. */
function SelectorFecha({
  etiqueta,
  valor,
  requerido,
  error,
  deshabilitado,
  onChange,
}: SelectorFechaProps) {
  return (
    // No se usa `Field`: clona su hijo para inyectar el id, y acá hay dos
    // controles. Cada uno lleva su propia etiqueta accesible.
    <div className="portal-campo">
      <span className="portal-campo-label">
        {etiqueta}
        {requerido && <span aria-hidden="true"> *</span>}
      </span>
      <MonthYearPicker
        value={valor}
        aria-label={etiqueta}
        disabled={deshabilitado}
        invalid={Boolean(error)}
        onChange={(fecha, { missingYear }) => onChange(fecha, missingYear)}
      />
      {error && <span className="portal-campo-error">{error}</span>}
    </div>
  );
}

interface CampoPeriodoProps {
  desde: string;
  hasta: string | null;
  enCurso: boolean;
  /** Texto de la opción de "sigue vigente", propio de cada sección. */
  etiquetaEnCurso: string;
  errorDesde?: string;
  errorHasta?: string;
  onDesde: (fecha: string) => void;
  onHasta: (fecha: string) => void;
  onEnCurso: (enCurso: boolean) => void;
  /** Avisa cuando un campo queda con mes elegido y sin año, para bloquear el guardado. */
  onMesSinAnio?: (campo: keyof MesSinAnio, faltaAnio: boolean) => void;
}

/** Período desde–hasta con mes opcional y la opción de marcarlo en curso. */
export function CampoPeriodo({
  desde,
  hasta,
  enCurso,
  etiquetaEnCurso,
  errorDesde,
  errorHasta,
  onDesde,
  onHasta,
  onEnCurso,
  onMesSinAnio,
}: CampoPeriodoProps) {
  return (
    <>
      <div className="portal-form-grid">
        <SelectorFecha
          etiqueta="Desde"
          valor={desde}
          requerido
          error={errorDesde}
          onChange={(fecha, faltaAnio) => {
            onDesde(fecha);
            onMesSinAnio?.("desde", faltaAnio);
          }}
        />
        <SelectorFecha
          etiqueta="Hasta"
          valor={enCurso ? "" : (hasta ?? "")}
          deshabilitado={enCurso}
          error={enCurso ? undefined : errorHasta}
          onChange={(fecha, faltaAnio) => {
            onHasta(fecha);
            onMesSinAnio?.("hasta", faltaAnio);
          }}
        />
      </div>
      <Checkbox
        label={etiquetaEnCurso}
        checked={enCurso}
        onChange={(e) => onEnCurso(e.target.checked)}
      />
    </>
  );
}
