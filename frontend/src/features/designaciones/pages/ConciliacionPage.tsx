import axios from "axios";
import { useMemo, useState } from "react";
import { Button, InlineAlert, Input, Select } from "@ars-docendi/ui";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import { Migas } from "../../../shared/navegacion/Migas";
import { useAbrirDetalle } from "../../../shared/navegacion/useEstadoVolver";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { PanelPestana, Pestanas } from "../../../shared/ui/Pestanas";
import { mostrarToast } from "../../../shared/ui/toasts";
import type { PedidoConciliacion } from "../api/conciliacionApi";
import { ModalRechazarConciliacion } from "../components/ModalRechazarConciliacion";
import { ModalSincronizarConciliacion } from "../components/ModalSincronizarConciliacion";
import {
  TablaPorConciliar,
  TablaRechazados,
  TablaSincronizados,
} from "../components/TablasConciliacion";
import {
  agruparPorDocente,
  carrerasDe,
  filasDe,
  FILTROS_CONCILIACION_INICIALES,
  nombreCompleto,
  pasaFiltros,
  pendientes,
  plural,
  type DocenteConciliacion,
  type FiltrosConciliacion,
  type PestanaConciliacion,
} from "../components/conciliacionModelo";
import {
  useConciliacion,
  useRechazarEnConciliacion,
  useSincronizarConciliacion,
} from "../hooks/useConciliacion";
import type { Novedad } from "../types";
import "../components/conciliacion.css";

const PREFIJO = "conciliacion";

/**
 * Lo que se aceptó de un docente: sus pedidos en lote con su versión. Si un pedido se edita,
 * se rechaza o llega otro, la firma cambia y el docente queda sin aceptar.
 */
const firma = (docente: DocenteConciliacion) =>
  pendientes(docente)
    .map((pedido) => `${pedido.id}:${pedido.version}`)
    .sort()
    .join("|");

const VACIOS: Record<PestanaConciliacion, { titulo: string; texto: string }> = {
  pendientes: {
    titulo: "No hay docentes por conciliar",
    texto:
      "Acá se ven únicamente los pedidos en lote del período activo. Cuando Decanato acepte un pedido, aparece para conciliar.",
  },
  rechazados: {
    titulo: "No hay cambios rechazados",
    texto:
      "Los cambios que rechaces en la conciliación quedan acá con su motivo y salen del Excel del lote.",
  },
  sincronizados: {
    titulo: "Todavía no se sincronizó nada",
    texto: "Lo que sincronices queda acá como registro y ya no se puede editar.",
  },
};

export function ConciliacionPage() {
  const abrir = useAbrirDetalle();
  const { data, isLoading, isError, refetch } = useConciliacion();
  const sincronizar = useSincronizarConciliacion();
  const rechazar = useRechazarEnConciliacion();

  const [pestana, setPestana] = useState<PestanaConciliacion>("pendientes");
  const [filtros, setFiltros] = useState<FiltrosConciliacion>(FILTROS_CONCILIACION_INICIALES);
  const [aceptadosPorFirma, setAceptadosPorFirma] = useState<Map<string, string>>(new Map());
  const [abiertos, setAbiertos] = useState<Set<string>>(new Set());
  const [rechazando, setRechazando] = useState<{ docente: string; pedido: PedidoConciliacion }>();
  const [clavePreview, setClavePreview] = useState<string>();
  const [errorPagina, setErrorPagina] = useState<string>();

  const docentes = useMemo(() => agruparPorDocente(data?.docentes ?? []), [data]);
  const porConciliar = docentes.filter((docente) => pendientes(docente).length > 0);
  const aceptados = new Set(
    porConciliar
      .filter((docente) => aceptadosPorFirma.get(docente.personaId) === firma(docente))
      .map((docente) => docente.personaId),
  );
  const docentesAceptados = porConciliar.filter((docente) => aceptados.has(docente.personaId));
  const cambiosAceptados = docentesAceptados.reduce((s, d) => s + pendientes(d).length, 0);
  const carreras = [...new Set(docentes.flatMap(carrerasDe))].sort((a, b) =>
    a.localeCompare(b, "es"),
  );

  const visiblesPendientes = porConciliar.filter((docente) =>
    pasaFiltros(docente, pendientes(docente), filtros),
  );
  const filasRechazados = filasDe(docentes, "rechazados", filtros);
  const filasSincronizados = filasDe(docentes, "sincronizados", filtros);
  const cuenta =
    pestana === "pendientes"
      ? plural(visiblesPendientes.length, "docente", "docentes")
      : plural(
          (pestana === "rechazados" ? filasRechazados : filasSincronizados).length,
          "cambio",
          "cambios",
        );
  const hayEnPestana =
    pestana === "pendientes"
      ? porConciliar.length > 0
      : docentes.some((docente) =>
          docente.pedidos.some(
            (pedido) => pedido.estado === (pestana === "rechazados" ? "no_aprobado" : "aprobado"),
          ),
        );

  function aceptar(lista: DocenteConciliacion[], valor: boolean) {
    setAceptadosPorFirma((previo) => {
      const siguiente = new Map(previo);
      for (const docente of lista) {
        if (valor) siguiente.set(docente.personaId, firma(docente));
        else siguiente.delete(docente.personaId);
      }
      return siguiente;
    });
  }

  function alternar(personaId: string) {
    setAbiertos((previo) => {
      const siguiente = new Set(previo);
      if (!siguiente.delete(personaId)) siguiente.add(personaId);
      return siguiente;
    });
  }

  function editar(pedido: PedidoConciliacion) {
    abrir(`/designaciones/pedidos/${pedido.id}/editar`);
  }

  function abrirRechazo(docente: DocenteConciliacion, pedido: PedidoConciliacion) {
    rechazar.reset();
    setRechazando({ docente: nombreCompleto(docente), pedido });
  }

  function confirmarRechazo(motivo: string) {
    if (!rechazando) return;
    const { docente, pedido } = rechazando;
    rechazar.mutate(
      { id: pedido.id, motivo },
      {
        onSuccess: () => {
          setRechazando(undefined);
          mostrarToast({
            tipo: "success",
            titulo: `Cambio ${pedido.numero} rechazado`,
            descripcion: `${docente} quedó sin aceptar: revisalo de nuevo.`,
          });
        },
      },
    );
  }

  function abrirPreview() {
    sincronizar.reset();
    setErrorPagina(undefined);
    // Una clave por preview: reintentar tras un corte de red no aplica dos veces.
    setClavePreview(crypto.randomUUID());
  }

  function confirmarSincronizacion() {
    if (!clavePreview) return;
    const pedidos = docentesAceptados
      .flatMap(pendientes)
      .map((p) => ({ id: p.id, version: p.version }));
    const cantidadDocentes = docentesAceptados.length;
    sincronizar.mutate(
      { pedidos, clave: clavePreview },
      {
        onSuccess: () => {
          setClavePreview(undefined);
          setAceptadosPorFirma(new Map());
          setAbiertos(new Set());
          mostrarToast({
            tipo: "success",
            titulo: "Sincronización completa",
            descripcion: `Se aplicaron ${plural(pedidos.length, "cambio", "cambios")} de ${plural(cantidadDocentes, "docente", "docentes")}.`,
          });
        },
        onError: (error) => {
          // Un pedido cambió mientras se conciliaba: no se aplicó nada y hay que volver a revisar.
          if (axios.isAxiosError(error) && error.response?.status === 409) {
            setClavePreview(undefined);
            setErrorPagina(
              mensajeProblema(
                error,
                "Un pedido cambió mientras conciliabas. Revisalo y volvé a aceptarlo.",
              ),
            );
          }
        },
      },
    );
  }

  const meta = data?.periodo
    ? `Pedidos en lote del período ${data.periodo.nombre}`
    : "Pedidos en lote del período activo";

  return (
    <>
      <Migas items={[{ label: "Inicio", href: "/" }, { label: "Conciliación" }]} />
      <PageHeader title="Conciliación" meta={meta} />

      {isLoading && <p className="conc-cargando">Cargando la conciliación del período…</p>}

      {isError && (
        <InlineAlert severity="danger" title="No se pudo cargar la conciliación">
          Hubo un problema al obtener los pedidos en lote.{" "}
          <Button variant="ghost" size="sm" onClick={() => refetch()}>
            Reintentar
          </Button>
        </InlineAlert>
      )}

      {data && !data.periodo && (
        <InlineAlert severity="info" title="No hay un período activo">
          La conciliación trabaja sobre los pedidos en lote del período activo.
        </InlineAlert>
      )}

      {data?.periodo && (
        <>
          {errorPagina && (
            <InlineAlert
              severity="warning"
              title="No se sincronizó nada"
              onClose={() => setErrorPagina(undefined)}
            >
              {errorPagina}
            </InlineAlert>
          )}

          <div className="conc-tabs-row">
            <Pestanas
              prefijo={PREFIJO}
              aria-label="Estado de la conciliación"
              value={pestana}
              onChange={(id) => setPestana(id as PestanaConciliacion)}
              items={[
                { id: "pendientes", label: "Por conciliar", count: porConciliar.length },
                {
                  id: "rechazados",
                  label: "Rechazados",
                  count: filasDe(docentes, "rechazados", FILTROS_CONCILIACION_INICIALES).length,
                },
                {
                  id: "sincronizados",
                  label: "Sincronizados",
                  count: filasDe(docentes, "sincronizados", FILTROS_CONCILIACION_INICIALES).length,
                },
              ]}
            />
            {pestana === "pendientes" && (
              <div className="conc-sincronizar">
                <Button
                  variant="primary"
                  disabled={cambiosAceptados === 0}
                  title={cambiosAceptados === 0 ? "Tildá al menos un docente" : undefined}
                  onClick={abrirPreview}
                >
                  Sincronizar
                  {cambiosAceptados > 0 && ` ${plural(cambiosAceptados, "cambio", "cambios")}`}
                </Button>
              </div>
            )}
          </div>

          <PanelPestana prefijo={PREFIJO} id={pestana}>
            {!hayEnPestana ? (
              <InlineAlert
                severity="info"
                title={VACIOS[pestana].titulo}
                className="conc-vacio-alerta"
              >
                {VACIOS[pestana].texto}
              </InlineAlert>
            ) : (
              <>
                <div className="conc-toolbar">
                  <Input
                    type="search"
                    className="conc-buscar"
                    placeholder="Buscar docente o CUIL"
                    aria-label="Buscar docente o CUIL"
                    value={filtros.texto}
                    onChange={(e) => setFiltros({ ...filtros, texto: e.target.value })}
                  />
                  <Select
                    aria-label="Novedad"
                    value={filtros.novedad}
                    onChange={(e) =>
                      setFiltros({ ...filtros, novedad: e.target.value as Novedad | "" })
                    }
                  >
                    <option value="">Novedad: Todas</option>
                    <option value="Alta">Alta</option>
                    <option value="Cambio de cargo o dedicación">Cambio</option>
                    <option value="Baja">Baja</option>
                  </Select>
                  {carreras.length > 1 && (
                    <Select
                      aria-label="Carrera"
                      value={filtros.carrera}
                      onChange={(e) => setFiltros({ ...filtros, carrera: e.target.value })}
                    >
                      <option value="">Carrera: Todas</option>
                      {carreras.map((carrera) => (
                        <option key={carrera} value={carrera}>
                          {carrera}
                        </option>
                      ))}
                    </Select>
                  )}
                  <span className="conc-cuenta">{cuenta}</span>
                </div>

                {pestana === "pendientes" && (
                  <TablaPorConciliar
                    docentes={visiblesPendientes}
                    aceptados={aceptados}
                    abiertos={abiertos}
                    onAceptar={aceptar}
                    onAlternar={alternar}
                    onEditar={editar}
                    onRechazar={abrirRechazo}
                  />
                )}
                {pestana === "rechazados" && (
                  <TablaRechazados filas={filasRechazados} onEditar={editar} />
                )}
                {pestana === "sincronizados" && <TablaSincronizados filas={filasSincronizados} />}
              </>
            )}
          </PanelPestana>
        </>
      )}

      {rechazando && (
        <ModalRechazarConciliacion
          pedido={rechazando.pedido}
          docente={rechazando.docente}
          rechazando={rechazar.isPending}
          error={
            rechazar.isError
              ? mensajeProblema(rechazar.error, "No se pudo rechazar el cambio. Reintentá.")
              : undefined
          }
          onCancelar={() => setRechazando(undefined)}
          onConfirmar={confirmarRechazo}
        />
      )}

      {clavePreview && (
        <ModalSincronizarConciliacion
          aceptados={docentesAceptados}
          sincronizando={sincronizar.isPending}
          error={
            sincronizar.isError
              ? mensajeProblema(sincronizar.error, "No se pudo sincronizar. Reintentá.")
              : undefined
          }
          onCancelar={() => setClavePreview(undefined)}
          onConfirmar={confirmarSincronizacion}
        />
      )}
    </>
  );
}
