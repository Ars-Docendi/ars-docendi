import { useEffect, useRef } from "react";
import { useQuery } from "@tanstack/react-query";

import { listarAuditoria } from "../api/auditoriaApi";
import type { EventoAuditoria, FiltrosAuditoria as FiltrosAuditoriaApi } from "../api/auditoriaApi";
import { useFiltrosAuditoria } from "../hooks/useFiltrosAuditoria";
import { limiteDesdeParaPeriodo, datetimeLocalAIso } from "../utils/zonaHoraria";
import { FiltrosAuditoria } from "./FiltrosAuditoria";
import { ListaAuditoria } from "./ListaAuditoria";
import { PanelDetalleEvento } from "./PanelDetalleEvento";

const TAMANO_PAGINA = 50;

function filtrosParaApi(
  filtros: ReturnType<typeof useFiltrosAuditoria>["filtros"],
): FiltrosAuditoriaApi {
  // Un rango custom (Desde/Hasta) pisa el período (design D9).
  const desde = filtros.desde
    ? datetimeLocalAIso(filtros.desde)
    : limiteDesdeParaPeriodo(filtros.periodo);
  const hasta = filtros.hasta ? datetimeLocalAIso(filtros.hasta) : undefined;
  return {
    pagina: filtros.pagina,
    tamanoPagina: TAMANO_PAGINA,
    ...(filtros.q && { q: filtros.q }),
    ...(desde && { desde }),
    ...(hasta && { hasta }),
    ...(filtros.accion && { accion: filtros.accion }),
    ...(filtros.modulo && { modulo: filtros.modulo }),
    ...(filtros.tabla && { tabla: filtros.tabla }),
    ...(filtros.clave && { rowPk: filtros.clave }),
  };
}

/** La pestaña Auditoría (requisitos «Auditoría tab filters...», «Inline audit event detail panel»). */
export function PestanaAuditoria() {
  const {
    filtros,
    actualizarFiltros,
    irAPagina,
    limpiarFiltros,
    abrirEvento,
    difiereDelDefault,
    cantidadFiltrosAvanzados,
  } = useFiltrosAuditoria();

  const filtrosApi = filtrosParaApi(filtros);
  const consulta = useQuery({
    queryKey: ["administracion", "auditoria", filtrosApi],
    queryFn: () => listarAuditoria(filtrosApi),
    refetchOnWindowFocus: false,
  });

  const filaAnteriorRef = useRef<string | null>(null);

  const eventoAbierto: EventoAuditoria | undefined = filtros.evento
    ? consulta.data?.elementos.find((e) => e.id === filtros.evento)
    : undefined;

  // El detalle se cierra solo cuando su evento deja de estar en la página
  // cargada (requisito «Event leaves the result»): otro filtro, página o
  // refresh que ya no lo incluya.
  useEffect(() => {
    if (!consulta.data || !filtros.evento) return;
    const sigueEnLaPagina = consulta.data.elementos.some((e) => e.id === filtros.evento);
    if (!sigueEnLaPagina) abrirEvento(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [consulta.data, filtros.evento]);

  function seleccionarFila(evento: EventoAuditoria) {
    if (filtros.evento === evento.id) {
      filaAnteriorRef.current = evento.id;
      abrirEvento(null);
    } else {
      abrirEvento(evento.id);
    }
  }

  function cerrarDetalle() {
    const idPrevio = filtros.evento;
    abrirEvento(null);
    filaAnteriorRef.current = idPrevio;
  }

  // Devuelve el foco a la fila que abrió el panel, al cerrarlo (design D10).
  useEffect(() => {
    if (filtros.evento || !filaAnteriorRef.current) return;
    document.getElementById(`auditoria-fila-${filaAnteriorRef.current}`)?.focus();
    filaAnteriorRef.current = null;
  }, [filtros.evento]);

  return (
    <div className="sistema-pestana-auditoria">
      <FiltrosAuditoria
        filtros={filtros}
        total={consulta.data?.total ?? 0}
        actualizarFiltros={actualizarFiltros}
        limpiarFiltros={limpiarFiltros}
        difiereDelDefault={difiereDelDefault}
        cantidadFiltrosAvanzados={cantidadFiltrosAvanzados}
      />

      <div className={`auditoria-grid ${eventoAbierto ? "auditoria-grid--con-detalle" : ""}`}>
        <ListaAuditoria
          pagina={consulta.data}
          cargando={consulta.isPending}
          esError={consulta.isError}
          eventoSeleccionado={filtros.evento}
          onSeleccionarFila={seleccionarFila}
          onCambiarPagina={irAPagina}
          onReintentar={() => void consulta.refetch()}
        />
        {eventoAbierto && <PanelDetalleEvento evento={eventoAbierto} onCerrar={cerrarDetalle} />}
      </div>
    </div>
  );
}
