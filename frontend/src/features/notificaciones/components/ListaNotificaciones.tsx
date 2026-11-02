import { useEffect, useMemo, useRef, type RefObject } from "react";
import { Button } from "@ars-docendi/ui";
import type { UseInfiniteQueryResult } from "@tanstack/react-query";

import { agruparPorFecha } from "../presentacion";
import { estaSeleccionada, type Seleccion } from "../seleccion";
import type { EstadoLectura, Notificacion, PaginaNotificaciones } from "../types";
import { IconoCampanaApagada, IconoRotarIzquierda } from "./iconos";
import { ItemNotificacion } from "./ItemNotificacion";

type Consulta = UseInfiniteQueryResult<{ pages: PaginaNotificaciones[] }, Error>;

interface ListaNotificacionesProps {
  estado: EstadoLectura;
  consulta: Consulta;
  raizScroll: RefObject<HTMLElement | null>;
  modoSeleccion: boolean;
  seleccion: Seleccion;
  onAbrir: (notificacion: Notificacion) => void;
  onAlternar: (id: string) => void;
}

function Esqueleto() {
  return (
    <div className="adoc-notif-esqueleto" role="status" aria-label="Cargando notificaciones">
      <div className="adoc-sk" style={{ height: 24, width: 64, marginBottom: 8 }} />
      {[0, 1, 2].map((i) => (
        <div key={i} className="adoc-sk-fila">
          <div className="adoc-sk" style={{ width: 24, height: 24, flexShrink: 0 }} />
          <div style={{ flex: 1, minWidth: 0, display: "flex", flexDirection: "column", gap: 6 }}>
            <div className="adoc-sk" style={{ height: 24, width: "85%" }} />
            <div className="adoc-sk" style={{ height: 20, width: "100%" }} />
            <div className="adoc-sk" style={{ height: 20, width: 120, maxWidth: "100%" }} />
          </div>
        </div>
      ))}
    </div>
  );
}

/** One tab: skeleton, error, empty and the grouped list with its infinite scroll. */
export function ListaNotificaciones({
  estado,
  consulta,
  raizScroll,
  modoSeleccion,
  seleccion,
  onAbrir,
  onAlternar,
}: ListaNotificacionesProps) {
  const sentinela = useRef<HTMLDivElement>(null);
  const { data, hasNextPage, isFetchingNextPage, isFetchNextPageError, fetchNextPage } = consulta;
  const todas = useMemo(() => data?.pages.flatMap((p) => p.datos) ?? [], [data]);
  const grupos = useMemo(() => agruparPorFecha(todas, new Date()), [todas]);
  const puedeCargarMas = hasNextPage && !isFetchingNextPage && !isFetchNextPageError;

  useEffect(() => {
    const el = sentinela.current;
    if (!el || !puedeCargarMas || typeof IntersectionObserver === "undefined") return;
    const observador = new IntersectionObserver(
      (entradas) => {
        if (entradas[0]?.isIntersecting) void fetchNextPage();
      },
      { root: raizScroll.current, rootMargin: "160px" },
    );
    observador.observe(el);
    return () => observador.disconnect();
  }, [puedeCargarMas, fetchNextPage, raizScroll, todas.length]);

  if (consulta.isPending) return <Esqueleto />;

  if (consulta.isError && todas.length === 0) {
    return (
      <div className="adoc-notif-error">
        <IconoRotarIzquierda />
        <div>
          <p className="t">No pudimos cargar las notificaciones</p>
          <p className="d">Revisá tu conexión e intentá nuevamente.</p>
        </div>
        <Button id="notif-reintentar" variant="secondary" onClick={() => void consulta.refetch()}>
          Reintentar
        </Button>
      </div>
    );
  }

  if (todas.length === 0) {
    const noLeidas = estado === "NO_LEIDAS";
    return (
      <div className="adoc-notif-vacio">
        <IconoCampanaApagada />
        <h3>
          {noLeidas ? "No tenés notificaciones sin leer" : "Todavía no tenés notificaciones leídas"}
        </h3>
        <p>
          {noLeidas
            ? "Las nuevas notificaciones van a aparecer acá."
            : "Cuando abras una notificación, la vas a encontrar acá."}
        </p>
      </div>
    );
  }

  return (
    <div
      className={modoSeleccion ? "adoc-notif-modo-seleccion" : undefined}
      aria-label={estado === "NO_LEIDAS" ? "Notificaciones no leídas" : "Notificaciones leídas"}
      role="group"
    >
      {consulta.isRefetchError && !isFetchNextPageError && (
        <div className="adoc-notif-aviso" role="alert">
          <span>No pudimos actualizar la lista.</span>
          <button
            type="button"
            className="adoc-notif-accion"
            onClick={() => void consulta.refetch()}
          >
            Reintentar
          </button>
        </div>
      )}
      {grupos.map((grupo) => (
        <section key={grupo.etiqueta} className="adoc-notif-grupo" aria-label={grupo.etiqueta}>
          <p className="adoc-notif-grupo-titulo">{grupo.etiqueta}</p>
          <ul className="adoc-notif-ul">
            {grupo.notificaciones.map((n) => (
              <li key={n.id}>
                <ItemNotificacion
                  notificacion={n}
                  modoSeleccion={modoSeleccion}
                  seleccionada={estaSeleccionada(seleccion, n.id)}
                  onAbrir={onAbrir}
                  onAlternar={(item) => onAlternar(item.id)}
                />
              </li>
            ))}
          </ul>
        </section>
      ))}
      <div ref={sentinela} style={{ minHeight: 1 }} aria-hidden />
      {isFetchingNextPage && (
        <div className="adoc-notif-mas" role="status">
          <span className="adoc-notif-spin" aria-hidden />
          Cargando más notificaciones…
        </div>
      )}
      {isFetchNextPageError && (
        <div className="adoc-notif-mas error" role="alert">
          <span>No pudimos cargar más notificaciones.</span>
          <button type="button" className="adoc-notif-accion" onClick={() => void fetchNextPage()}>
            Reintentar
          </button>
        </div>
      )}
    </div>
  );
}
