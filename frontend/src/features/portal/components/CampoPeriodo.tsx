import { Checkbox, Input, Select } from "@ars-docendi/ui";

import { MESES, anioDe, componerFecha, mesDe } from "../formato";
import "./portal.css";

interface SelectorFechaProps {
  etiqueta: string;
  valor: string;
  requerido?: boolean;
  error?: string;
  deshabilitado?: boolean;
  onChange: (fecha: string) => void;
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
  const anio = anioDe(valor);
  const mes = mesDe(valor);

  return (
    // No se usa `Field`: clona su hijo para inyectar el id, y acá hay dos
    // controles. Cada uno lleva su propia etiqueta accesible.
    <div className="portal-campo">
      <span className="portal-campo-label">
        {etiqueta}
        {requerido && <span aria-hidden="true"> *</span>}
      </span>
      <div className="portal-fecha">
        <Select
          value={mes}
          aria-label={`Mes de ${etiqueta.toLowerCase()}`}
          disabled={deshabilitado}
          invalid={Boolean(error)}
          onChange={(e) => onChange(componerFecha(anio, e.target.value))}
        >
          <option value="">Mes</option>
          {MESES.map((m) => (
            <option value={m.valor} key={m.valor}>
              {m.nombre}
            </option>
          ))}
        </Select>
        <Input
          value={anio}
          inputMode="numeric"
          maxLength={4}
          placeholder="Año"
          aria-label={`Año de ${etiqueta.toLowerCase()}`}
          disabled={deshabilitado}
          aria-invalid={Boolean(error) || undefined}
          onChange={(e) => onChange(componerFecha(e.target.value.replace(/\D/g, ""), mes))}
        />
      </div>
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
  onDesde: (fecha: string) => void;
  onHasta: (fecha: string) => void;
  onEnCurso: (enCurso: boolean) => void;
}

/** Período desde–hasta con mes opcional y la opción de marcarlo en curso. */
export function CampoPeriodo({
  desde,
  hasta,
  enCurso,
  etiquetaEnCurso,
  errorDesde,
  onDesde,
  onHasta,
  onEnCurso,
}: CampoPeriodoProps) {
  return (
    <>
      <div className="portal-form-grid">
        <SelectorFecha
          etiqueta="Desde"
          valor={desde}
          requerido
          error={errorDesde}
          onChange={onDesde}
        />
        <SelectorFecha
          etiqueta="Hasta"
          valor={enCurso ? "" : (hasta ?? "")}
          deshabilitado={enCurso}
          onChange={onHasta}
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
