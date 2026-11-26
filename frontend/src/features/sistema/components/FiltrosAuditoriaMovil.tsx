import { useEffect, useState } from "react";

import { HerramientasLista } from "../../../shared/ui/HerramientasLista";
import type { GrupoFiltro } from "../../../shared/ui/HojaFiltros";
import { fechaISOATexto, fechaTextoAISO } from "../../../shared/ui/camposFechaHora";
import type { EstadoFiltrosAuditoria, FiltroAccion } from "../hooks/useFiltrosAuditoria";
import { ACCIONES_AUDITORIA, MODULOS_AUDITORIA } from "../utils/modulosAuditoria";
import { SelectorPeriodo } from "./FiltrosAuditoria";

interface FiltrosAuditoriaMovilProps {
  filtros: EstadoFiltrosAuditoria;
  total: number;
  actualizarFiltros: (parcial: Partial<Omit<EstadoFiltrosAuditoria, "pagina" | "evento">>) => void;
}

/** The audit filters as the phone toolbar edits them: options as lists, dates as dd/mm/aaaa. */
interface FiltrosMovil {
  q: string;
  accion: string[];
  modulo: string[];
  desde: string;
  hasta: string;
  tabla: string;
  clave: string;
}

const VACIOS: FiltrosMovil = {
  q: "",
  accion: [],
  modulo: [],
  desde: "",
  hasta: "",
  tabla: "",
  clave: "",
};

const GRUPOS: GrupoFiltro<FiltrosMovil>[] = [
  { clave: "accion", titulo: "Acción", unica: true, opciones: [...ACCIONES_AUDITORIA] },
  { clave: "modulo", titulo: "Módulo", unica: true, opciones: [...MODULOS_AUDITORIA] },
  { clave: "desde", titulo: "Desde", tipo: "texto", placeholder: "dd/mm/aaaa" },
  { clave: "hasta", titulo: "Hasta", tipo: "texto", placeholder: "dd/mm/aaaa" },
  { clave: "tabla", titulo: "Tabla", tipo: "texto", placeholder: "p. ej. identity.users" },
  { clave: "clave", titulo: "Clave de fila", tipo: "texto", placeholder: "p. ej. 1042" },
];

const FECHA_ISO = /^\d{4}-\d{2}-\d{2}$/;

/**
 * A typed dd/mm/aaaa date as the `datetime-local` value the URL stores. The same date keeps the
 * hour it already had (set on desktop); a different date drops it and covers the whole day.
 */
function fechaAplicada(texto: string, actual: string, horaDelDia: string): string {
  const limpio = texto.trim();
  if (!limpio) return "";
  const iso = FECHA_ISO.test(limpio) ? limpio : fechaTextoAISO(limpio);
  if (!iso) return "";
  return actual.slice(0, 10) === iso ? actual : `${iso}T${horaDelDia}`;
}

/**
 * Phone toolbar of the Auditoría tab: search, the period toggle and the remaining filters in the
 * shared filter sheet. Reads and writes the same URL state as `FiltrosAuditoria`.
 */
export function FiltrosAuditoriaMovil({
  filtros,
  total,
  actualizarFiltros,
}: FiltrosAuditoriaMovilProps) {
  // Same debounce as the desktop search: one query (and one history entry) per pause.
  const [busqueda, setBusqueda] = useState(filtros.q);
  const [ultimoQExterno, setUltimoQExterno] = useState(filtros.q);
  if (filtros.q !== ultimoQExterno) {
    setUltimoQExterno(filtros.q);
    setBusqueda(filtros.q);
  }

  useEffect(() => {
    if (busqueda === filtros.q) return;
    const temporizador = setTimeout(() => actualizarFiltros({ q: busqueda }), 300);
    return () => clearTimeout(temporizador);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [busqueda]);

  const valores: FiltrosMovil = {
    q: busqueda,
    accion: filtros.accion ? [filtros.accion] : [],
    modulo: filtros.modulo ? [filtros.modulo] : [],
    desde: fechaISOATexto(filtros.desde.slice(0, 10)),
    hasta: fechaISOATexto(filtros.hasta.slice(0, 10)),
    tabla: filtros.tabla,
    clave: filtros.clave,
  };

  function aplicar(siguientes: FiltrosMovil) {
    const soloBusqueda = (Object.keys(VACIOS) as (keyof FiltrosMovil)[]).every(
      (clave) => clave === "q" || siguientes[clave] === valores[clave],
    );
    if (soloBusqueda) {
      setBusqueda(siguientes.q);
      return;
    }
    setBusqueda(siguientes.q);
    actualizarFiltros({
      q: siguientes.q,
      accion: (siguientes.accion[0] ?? "") as FiltroAccion,
      modulo: siguientes.modulo[0] ?? "",
      desde: fechaAplicada(siguientes.desde, filtros.desde, "00:00"),
      hasta: fechaAplicada(siguientes.hasta, filtros.hasta, "23:59"),
      tabla: siguientes.tabla.trim(),
      clave: siguientes.clave.trim(),
    });
  }

  return (
    <div className="auditoria-filtros-movil">
      <HerramientasLista
        grupos={GRUPOS}
        filtros={valores}
        vacios={VACIOS}
        onFiltrosChange={aplicar}
        busqueda={{ clave: "q", placeholder: "Buscar por usuario, objeto o cambio" }}
      />
      <SelectorPeriodo
        periodo={filtros.periodo}
        onCambiar={(periodo) => actualizarFiltros({ periodo })}
      />
      <p className="auditoria-filtros-resumen">
        <span>
          <strong>{total}</strong> registros · solo lectura
        </span>
      </p>
    </div>
  );
}
