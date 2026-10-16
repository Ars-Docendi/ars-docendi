import { useEffect, useRef, useState } from "react";
import { Button, Input } from "@ars-docendi/ui";

import { IconoSquarePen } from "../../../shared/ui/iconos";
import { formatearUsd } from "../utils/formatoDeUso";

interface TopeOrganizacionalCardProps {
  /**
   * El tope organizacional vigente, leído de `GET …/presupuestos` (tarea
   * 12.8 de sistema-seccion-unificada). `0` desactiva el tope; `undefined`
   * mientras la consulta está en vuelo.
   */
  topeConocido: number | undefined;
  /**
   * El gasto estimado del mes calendario en curso, del mismo endpoint —
   * siempre una estimación, nunca la factura real del proveedor.
   * `undefined` mientras la consulta está en vuelo.
   */
  gastoEstimadoDelMes: number | undefined;
  /** Si `GET …/presupuestos` falló. */
  error: boolean;
  onGuardar: (topeMensualUsd: number) => Promise<void>;
  /** Texto para la región viva compartida de la página (tasks.md 13.2). */
  onGuardado: (mensaje: string) => void;
}

/** A partir de qué porcentaje del tope el color de la barra cambia (DetectorDeUmbrales, backend). */
const UMBRAL_ADVERTENCIA = 50;
const UMBRAL_PELIGRO = 80;

/** El color de la barra según el mismo criterio 50/80/100% que ya loguea el backend. */
function colorDelGasto(porcentaje: number): string {
  if (porcentaje >= UMBRAL_PELIGRO) return "var(--danger-500)";
  if (porcentaje >= UMBRAL_ADVERTENCIA) return "var(--warning-500)";
  return "var(--success-500)";
}

/** «septiembre», con mayúscula inicial — el canvas titula la tarjeta «Tope de {Mes}». */
function mesActualCapitalizado(): string {
  const mes = new Intl.DateTimeFormat("es-AR", { month: "long" }).format(new Date());
  return mes.charAt(0).toUpperCase() + mes.slice(1);
}

/**
 * La quinta tarjeta de la fila de KPIs (rediseño «Uso del asistente», 1:1
 * con el canvas de Claude Design): el tope organizacional de gasto mensual,
 * titulada «Tope de {mes}», con el gasto del mes como número principal y el
 * tope al lado («de US$ X»), y el mismo patrón de confirmación inline al
 * BAJAR un valor que `EditorDeLimite` ya usaba. El nombre accesible del
 * grupo se mantiene «Tope organizacional mensual» a propósito —es lo que ya
 * usan los tests de integración— aunque la etiqueta VISIBLE ahora lleve el
 * mes, igual que un botón de ícono puede tener un `aria-label` más
 * descriptivo que lo que se ve.
 *
 * Lee el tope y el gasto del mes de `GET /api/asistente/administracion/presupuestos`
 * (tarea 12.8 de sistema-seccion-unificada) — YA NO depende de «lo último que
 * este admin guardó en esta sesión»: abrir la pantalla, o que la edite otro
 * admin, refleja el valor persistido. La barra de gasto-vs-tope usa los
 * mismos umbrales 50/80/100% que `DetectorDeUmbrales` ya loguea del lado del
 * backend.
 */
export function TopeOrganizacionalCard({
  topeConocido,
  gastoEstimadoDelMes,
  error,
  onGuardar,
  onGuardado,
}: TopeOrganizacionalCardProps) {
  const [editando, setEditando] = useState(false);
  const [valor, setValor] = useState("");
  const [confirmando, setConfirmando] = useState<number | null>(null);
  const [enviando, setEnviando] = useState(false);
  // Mismo motivo que `EditorDeCupoEnFila`: el botón que reaparece en modo
  // vista recibe el foco cuando la edición termina, para que un guardado o un
  // cancelar nunca lo suelten al `<body>` (tasks.md 13.2).
  const botonEditarRef = useRef<HTMLButtonElement>(null);
  // Ref, no estado: sólo decide qué hace el próximo efecto, nunca dispara un
  // render por sí sola — `setState` dentro de un efecto encadenaría otro render.
  const volverAEnfocarRef = useRef(false);

  useEffect(() => {
    if (volverAEnfocarRef.current && !editando && confirmando === null) {
      botonEditarRef.current?.focus();
      volverAEnfocarRef.current = false;
    }
  }, [editando, confirmando]);

  const etiqueta = `Tope de ${mesActualCapitalizado()}`;

  async function aplicar(nuevoTope: number) {
    setEnviando(true);
    try {
      await onGuardar(nuevoTope);
      onGuardado(
        `Tope organizacional mensual: ${nuevoTope === 0 ? "sin tope" : formatearUsd(nuevoTope)}.`,
      );
      volverAEnfocarRef.current = true;
      setEditando(false);
      setConfirmando(null);
    } finally {
      setEnviando(false);
    }
  }

  function alGuardar() {
    const numero = Number.parseFloat(valor);
    if (Number.isNaN(numero) || numero < 0) return;
    if (topeConocido !== undefined && numero > 0 && numero < topeConocido) {
      setConfirmando(numero);
      return;
    }
    void aplicar(numero);
  }

  function cancelar() {
    volverAEnfocarRef.current = true;
    setEditando(false);
    setConfirmando(null);
  }

  if (error) {
    return (
      <div
        className="adoc-asistente-admin-kpi adoc-asistente-admin-tope"
        role="group"
        aria-label="Tope organizacional mensual"
      >
        <span className="adoc-asistente-admin-kpi-etiqueta">{etiqueta}</span>
        <p>No se pudo cargar el tope organizacional.</p>
      </div>
    );
  }

  if (topeConocido === undefined || gastoEstimadoDelMes === undefined) {
    return (
      <div
        className="adoc-asistente-admin-kpi adoc-asistente-admin-tope"
        role="group"
        aria-label="Tope organizacional mensual"
      >
        <span className="adoc-asistente-admin-kpi-etiqueta">{etiqueta}</span>
        <p aria-live="polite">Cargando…</p>
      </div>
    );
  }

  if (confirmando !== null) {
    return (
      <div
        className="adoc-asistente-admin-kpi adoc-asistente-admin-tope"
        role="group"
        aria-label="Tope organizacional mensual"
      >
        <span className="adoc-asistente-admin-kpi-etiqueta">{etiqueta}</span>
        <p className="adoc-asistente-admin-confirmar">
          ¿Bajar de {formatearUsd(topeConocido)} a {formatearUsd(confirmando)}? Si el gasto ya lo
          supera, se bloquea al instante.
        </p>
        <div className="adoc-asistente-admin-tope-acciones">
          <Button
            variant="secondary"
            size="sm"
            loading={enviando}
            disabled={enviando}
            onClick={() => void aplicar(confirmando)}
          >
            Confirmar
          </Button>
          <Button variant="ghost" size="sm" disabled={enviando} onClick={cancelar}>
            Cancelar
          </Button>
        </div>
      </div>
    );
  }

  if (editando) {
    return (
      <div
        className="adoc-asistente-admin-kpi adoc-asistente-admin-tope"
        role="group"
        aria-label="Tope organizacional mensual"
      >
        <span className="adoc-asistente-admin-kpi-etiqueta">{etiqueta}</span>
        <Input
          type="number"
          min={0}
          step="0.01"
          autoFocus
          aria-label="Tope mensual (USD)"
          value={valor}
          onChange={(e) => setValor(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") alGuardar();
            if (e.key === "Escape") cancelar();
          }}
        />
        <div className="adoc-asistente-admin-tope-acciones">
          <Button size="sm" loading={enviando} disabled={enviando} onClick={alGuardar}>
            Guardar
          </Button>
          <Button variant="ghost" size="sm" disabled={enviando} onClick={cancelar}>
            Cancelar
          </Button>
        </div>
        <span className="adoc-asistente-admin-kpi-nota">
          Gasto estimado de todos los usuarios juntos. 0 desactiva el tope.
        </span>
      </div>
    );
  }

  const hayTope = topeConocido > 0;
  const porcentaje = hayTope ? Math.min(100, (gastoEstimadoDelMes / topeConocido) * 100) : 0;

  return (
    <div
      className="adoc-asistente-admin-kpi adoc-asistente-admin-tope"
      role="group"
      aria-label="Tope organizacional mensual"
    >
      <div className="adoc-asistente-admin-tope-encabezado">
        <span className="adoc-asistente-admin-kpi-etiqueta">{etiqueta}</span>
        <Button
          ref={botonEditarRef}
          variant="ghost"
          size="sm"
          className="adoc-asistente-admin-tope-editar"
          aria-label="Editar tope"
          title="Editar tope"
          onClick={() => {
            setValor(String(topeConocido));
            setEditando(true);
          }}
        >
          <IconoSquarePen />
        </Button>
      </div>
      <span className="adoc-asistente-admin-kpi-valor-linea">
        <span>{formatearUsd(gastoEstimadoDelMes)}</span>
        <span className="adoc-asistente-admin-tope-de">
          {hayTope ? `de ${formatearUsd(topeConocido)} (estimado)` : "gastado este mes"}
        </span>
      </span>
      {hayTope && (
        <>
          <div
            className="adoc-asistente-admin-tope-barra"
            role="progressbar"
            aria-label="Gasto del mes respecto del tope"
            aria-valuenow={Math.round(porcentaje)}
            aria-valuemin={0}
            aria-valuemax={100}
          >
            <div
              className="adoc-asistente-admin-tope-barra-relleno"
              style={{ width: `${porcentaje}%`, background: colorDelGasto(porcentaje) }}
            />
            <span className="adoc-asistente-admin-tope-barra-marca" style={{ left: "50%" }} />
            <span className="adoc-asistente-admin-tope-barra-marca" style={{ left: "75%" }} />
          </div>
          <span className="adoc-asistente-admin-kpi-nota">
            {Math.round(porcentaje)}% usado · al 100% se bloquean las consultas
          </span>
        </>
      )}
      {!hayTope && (
        <span className="adoc-asistente-admin-kpi-nota">
          Sin tope: el gasto no bloquea consultas.
        </span>
      )}
    </div>
  );
}
