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

/** Los filtros que se escriben: se aplican con debounce, igual que la búsqueda. */
const CAMPOS_ESCRITOS = ["desde", "hasta", "tabla", "clave"] as const;
type CampoEscrito = (typeof CAMPOS_ESCRITOS)[number];
type ValoresEscritos = Record<CampoEscrito, string>;

function valoresEscritos(filtros: EstadoFiltrosAuditoria): ValoresEscritos {
  return { desde: filtros.desde, hasta: filtros.hasta, tabla: filtros.tabla, clave: filtros.clave };
}

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

  // Lo mismo para Desde, Hasta, Tabla y Clave: aplicados en cada tecla,
  // escribir «identity.users» dejaba 14 entradas en el historial y 14 consultas.
  const externos = valoresEscritos(filtros);
  const [escritos, setEscritos] = useState(externos);
  const [ultimosExternos, setUltimosExternos] = useState(externos);
  if (CAMPOS_ESCRITOS.some((campo) => externos[campo] !== ultimosExternos[campo])) {
    setUltimosExternos(externos);
    setEscritos(externos);
  }

  useEffect(() => {
    const cambios = CAMPOS_ESCRITOS.filter((campo) => escritos[campo] !== filtros[campo]);
    if (cambios.length === 0) return;
    const temporizador = setTimeout(
      () => actualizarFiltros(Object.fromEntries(cambios.map((campo) => [campo, escritos[campo]]))),
      300,
    );
    return () => clearTimeout(temporizador);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [escritos]);

  function escribir(campo: CampoEscrito, valor: string) {
    setEscritos((anteriores) => ({ ...anteriores, [campo]: valor }));
  }

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
                value={escritos.desde}
                onChange={(e) => escribir("desde", e.target.value)}
              />
            </label>
            <label className="auditoria-campo">
              Hasta
              <Input
                aria-label="Hasta"
                type="datetime-local"
                value={escritos.hasta}
                onChange={(e) => escribir("hasta", e.target.value)}
              />
            </label>
            <label className="auditoria-campo">
              Tabla
              <Input
                aria-label="Tabla"
                placeholder="p. ej. identity.users"
                value={escritos.tabla}
                onChange={(e) => escribir("tabla", e.target.value)}
              />
            </label>
            <label className="auditoria-campo">
              Clave de fila
              <Input
                aria-label="Clave de fila"
                aria-describedby="auditoria-clave-ayuda"
                placeholder="p. ej. 1042"
                value={escritos.clave}
                onChange={(e) => escribir("clave", e.target.value)}
              />
              <small id="auditoria-clave-ayuda" className="auditoria-campo-ayuda">
                Usá el JSON completo para una relación precisa. Una clave parcial legada consulta
                una agrupación histórica, no una relación individual.
              </small>
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
