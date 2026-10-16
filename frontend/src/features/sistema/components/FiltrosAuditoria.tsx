import { useEffect, useState } from "react";
import { Button, Input } from "@ars-docendi/ui";

import type { EstadoFiltrosAuditoria, FiltroAccion } from "../hooks/useFiltrosAuditoria";
import type { Periodo } from "../utils/zonaHoraria";
import { MODULOS_AUDITORIA } from "../utils/modulosAuditoria";
import { IconoListFilter, IconoSearch } from "../../../shared/ui/iconos";

interface FiltrosAuditoriaProps {
  filtros: EstadoFiltrosAuditoria;
  total: number;
  actualizarFiltros: (parcial: Partial<Omit<EstadoFiltrosAuditoria, "pagina" | "evento">>) => void;
  limpiarFiltros: () => void;
  difiereDelDefault: boolean;
  cantidadFiltrosAvanzados: number;
}

const PERIODOS: { valor: Periodo; etiqueta: string }[] = [
  { valor: "hoy", etiqueta: "Hoy" },
  { valor: "7d", etiqueta: "7 días" },
  { valor: "30d", etiqueta: "30 días" },
  { valor: "todo", etiqueta: "Todo" },
];

const ACCIONES: { valor: FiltroAccion; etiqueta: string }[] = [
  { valor: "", etiqueta: "Todas" },
  { valor: "INSERT", etiqueta: "Altas" },
  { valor: "UPDATE", etiqueta: "Cambios" },
  { valor: "DELETE", etiqueta: "Eliminaciones" },
];

/** Los filtros de la pestaña Auditoría (requisito «Auditoría tab filters and day-grouped list», design D13). */
export function FiltrosAuditoria({
  filtros,
  total,
  actualizarFiltros,
  limpiarFiltros,
  difiereDelDefault,
  cantidadFiltrosAvanzados,
}: FiltrosAuditoriaProps) {
  const [busqueda, setBusqueda] = useState(filtros.q);
  const [masFiltrosAbierto, setMasFiltrosAbierto] = useState(cantidadFiltrosAvanzados > 0);

  // Sincroniza con un cambio externo (p. ej. «Limpiar filtros») ajustando el
  // estado durante el render, sin un efecto — mismo patrón que
  // `GrupoColapsable` en `Sidebar.tsx`.
  const [ultimoQExterno, setUltimoQExterno] = useState(filtros.q);
  if (filtros.q !== ultimoQExterno) {
    setUltimoQExterno(filtros.q);
    setBusqueda(filtros.q);
  }

  // Debounce de la búsqueda: no dispara una consulta por tecla.
  useEffect(() => {
    if (busqueda === filtros.q) return;
    const temporizador = setTimeout(() => actualizarFiltros({ q: busqueda }), 300);
    return () => clearTimeout(temporizador);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [busqueda]);

  return (
    <>
      <div className="auditoria-filtros">
        <div className="auditoria-filtros-fila">
          <div className="auditoria-buscador">
            <span className="auditoria-buscador-icono">
              <IconoSearch />
            </span>
            <Input
              aria-label="Buscar"
              placeholder="Buscar por usuario, objeto o cambio"
              value={busqueda}
              onChange={(e) => setBusqueda(e.target.value)}
            />
          </div>
          <div className="auditoria-periodo" role="group" aria-label="Período">
            {PERIODOS.map((p) => (
              <button
                key={p.valor}
                type="button"
                aria-pressed={filtros.periodo === p.valor}
                className="auditoria-toggle"
                onClick={() => actualizarFiltros({ periodo: p.valor })}
              >
                {p.etiqueta}
              </button>
            ))}
          </div>
          <button
            type="button"
            className="auditoria-mas-filtros"
            aria-expanded={masFiltrosAbierto}
            onClick={() => setMasFiltrosAbierto((v) => !v)}
          >
            <IconoListFilter />
            Más filtros
            {cantidadFiltrosAvanzados > 0 && (
              <span className="auditoria-badge">{cantidadFiltrosAvanzados}</span>
            )}
          </button>
        </div>

        <div className="auditoria-filtros-fila">
          <div className="auditoria-chips" role="group" aria-label="Acción">
            <span className="auditoria-chips-etiqueta">Acción</span>
            {ACCIONES.map((a) => (
              <button
                key={a.valor || "todas"}
                type="button"
                aria-pressed={filtros.accion === a.valor}
                className="auditoria-chip"
                onClick={() => actualizarFiltros({ accion: a.valor })}
              >
                {a.etiqueta}
              </button>
            ))}
          </div>
          <div className="auditoria-chips" role="group" aria-label="Módulo">
            <span className="auditoria-chips-etiqueta">Módulo</span>
            <button
              type="button"
              aria-pressed={filtros.modulo === ""}
              className="auditoria-chip"
              onClick={() => actualizarFiltros({ modulo: "" })}
            >
              Todos
            </button>
            {MODULOS_AUDITORIA.map((m) => (
              <button
                key={m.valor}
                type="button"
                aria-pressed={filtros.modulo === m.valor}
                className="auditoria-chip"
                onClick={() => actualizarFiltros({ modulo: m.valor })}
              >
                {m.etiqueta}
              </button>
            ))}
          </div>
        </div>

        {masFiltrosAbierto && (
          <div className="auditoria-mas-filtros-grid">
            <label className="auditoria-campo">
              Desde
              <Input
                aria-label="Desde"
                type="datetime-local"
                value={filtros.desde}
                onChange={(e) => actualizarFiltros({ desde: e.target.value })}
              />
            </label>
            <label className="auditoria-campo">
              Hasta
              <Input
                aria-label="Hasta"
                type="datetime-local"
                value={filtros.hasta}
                onChange={(e) => actualizarFiltros({ hasta: e.target.value })}
              />
            </label>
            <label className="auditoria-campo">
              Tabla
              <Input
                aria-label="Tabla"
                placeholder="p. ej. identity.users"
                value={filtros.tabla}
                onChange={(e) => actualizarFiltros({ tabla: e.target.value })}
              />
            </label>
            <label className="auditoria-campo">
              Clave de fila
              <Input
                aria-label="Clave de fila"
                placeholder="p. ej. 1042"
                value={filtros.clave}
                onChange={(e) => actualizarFiltros({ clave: e.target.value })}
              />
            </label>
          </div>
        )}
      </div>

      <div className="auditoria-filtros-resumen">
        <span>
          <strong>{total}</strong> registros · solo lectura
        </span>
        {difiereDelDefault && (
          <Button variant="ghost" size="sm" type="button" onClick={limpiarFiltros}>
            Limpiar filtros
          </Button>
        )}
      </div>
    </>
  );
}
