import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent,
  type RefObject,
} from "react";
import { Button } from "@ars-docendi/ui";

import { registrarCapa } from "../../../shared/ui/capas";
import {
  useCantidadNoLeidas,
  useListaNotificaciones,
  useMarcarLeida,
  useMarcarLeidas,
} from "../hooks/useNotificaciones";
import {
  alternar,
  aSolicitud,
  cantidadSeleccionada,
  iniciarSeleccion,
  SELECCION_INICIAL,
  todasSeleccionadas,
  type Seleccion,
} from "../seleccion";
import type { EstadoLectura, Notificacion } from "../types";
import { IconoCerrar, IconoListaCheck } from "./iconos";
import { ListaNotificaciones } from "./ListaNotificaciones";
import "./notificaciones.css";

const PESTANIAS: { id: EstadoLectura; etiqueta: string }[] = [
  { id: "NO_LEIDAS", etiqueta: "No leídas" },
  { id: "LEIDAS", etiqueta: "Leídas" },
];

interface PanelNotificacionesProps {
  abierto: boolean;
  onCerrar: () => void;
  triggerRef: RefObject<HTMLElement | null>;
  /** Width in px of the sidebar the panel opens beside; 0 on narrow screens. */
  desplazamiento: number;
  /** Active role code: part of every query key. */
  rol: string;
  onNavegar: (notificacion: Notificacion) => void;
}

/** ARIA tabs: arrows, Home and End move the focus and activate. */
function teclasPestania(actual: EstadoLectura, activar: (id: EstadoLectura) => void) {
  return (evento: KeyboardEvent<HTMLButtonElement>) => {
    const ids = PESTANIAS.map((p) => p.id);
    const i = ids.indexOf(actual);
    let j = -1;
    if (evento.key === "ArrowRight") j = (i + 1) % ids.length;
    else if (evento.key === "ArrowLeft") j = (i - 1 + ids.length) % ids.length;
    else if (evento.key === "Home") j = 0;
    else if (evento.key === "End") j = ids.length - 1;
    if (j < 0) return;
    evento.preventDefault();
    activar(ids[j]);
    document.getElementById(`notif-tab-${ids[j]}`)?.focus();
  };
}

/**
 * Left drawer with the notifications of the active role. It is mounted only while open, so a new
 * opening always starts on "No leídas" with no selection and fetches only that tab.
 */
export function PanelNotificaciones(props: PanelNotificacionesProps) {
  return props.abierto ? <PanelAbierto {...props} /> : null;
}

function PanelAbierto({
  onCerrar,
  triggerRef,
  desplazamiento,
  rol,
  onNavegar,
}: PanelNotificacionesProps) {
  const [pestania, setPestania] = useState<EstadoLectura>("NO_LEIDAS");
  const [seleccion, setSeleccion] = useState<Seleccion>(SELECCION_INICIAL);
  const panelRef = useRef<HTMLDivElement>(null);
  const cerrarRef = useRef<HTMLButtonElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const cerrarPorNavegacion = useRef(false);
  const idTitulo = useId();

  const noLeidas = useListaNotificaciones(rol, "NO_LEIDAS", pestania === "NO_LEIDAS");
  const leidas = useListaNotificaciones(rol, "LEIDAS", pestania === "LEIDAS");
  const conteo = useCantidadNoLeidas(rol);
  const marcarLeida = useMarcarLeida(rol);
  const marcarLeidas = useMarcarLeidas(rol);

  const cargadasNoLeidas = useMemo(
    () => noLeidas.data?.pages.flatMap((p) => p.datos) ?? [],
    [noLeidas.data],
  );
  const idsCargadas = useMemo(() => cargadasNoLeidas.map((n) => n.id), [cargadasNoLeidas]);
  const totalNoLeidas = conteo.data ?? cargadasNoLeidas.length;
  const puedeSeleccionar = pestania === "NO_LEIDAS" && cargadasNoLeidas.length > 0;
  const seleccionActiva = seleccion.activa && puedeSeleccionar;

  // Selection ends by itself when there is nothing left to select (everything was marked).
  const [vistoPuede, setVistoPuede] = useState(puedeSeleccionar);
  if (vistoPuede !== puedeSeleccionar) {
    setVistoPuede(puedeSeleccionar);
    if (!puedeSeleccionar && seleccion.activa) setSeleccion(SELECCION_INICIAL);
  }

  const cerrar = () => onCerrar();
  const cerrarRefFn = useRef(cerrar);
  useEffect(() => {
    cerrarRefFn.current = cerrar;
  });

  // Overlay stack: Esc and the Tab trap act on the topmost layer, panel included.
  useEffect(
    () =>
      registrarCapa({ contenedor: () => panelRef.current, cerrar: () => cerrarRefFn.current() }),
    [],
  );

  // Focus into the dialog on open; back to the bell on close, unless a navigation took over.
  useEffect(() => {
    cerrarRef.current?.focus();
    const disparador = triggerRef.current;
    return () => {
      if (!cerrarPorNavegacion.current) setTimeout(() => disparador?.focus(), 0);
    };
  }, [triggerRef]);

  // An outside press closes it. The bell toggles by itself, so it does not count as outside.
  useEffect(() => {
    function alPresionar(evento: MouseEvent) {
      const objetivo = evento.target as Node;
      if (panelRef.current?.contains(objetivo)) return;
      if (triggerRef.current?.contains(objetivo)) return;
      cerrarRefFn.current();
    }
    document.addEventListener("mousedown", alPresionar);
    return () => document.removeEventListener("mousedown", alPresionar);
  }, [triggerRef]);

  function abrir(notificacion: Notificacion) {
    if (notificacion.leidaEn === null) marcarLeida.mutate(notificacion);
    cerrarPorNavegacion.current = true;
    onNavegar(notificacion);
  }

  function confirmarSeleccion() {
    const solicitud = aSolicitud(seleccion, cargadasNoLeidas);
    if (!solicitud) return;
    // Selection mode ends right away; the update is optimistic and rolled back with a toast on failure.
    setSeleccion(SELECCION_INICIAL);
    marcarLeidas.mutate(solicitud);
  }

  function cambiarPestania(id: EstadoLectura) {
    setSeleccion(SELECCION_INICIAL);
    setPestania(id);
  }

  const cuenta = cantidadSeleccionada(seleccion, idsCargadas, totalNoLeidas);
  const todas = todasSeleccionadas(seleccion, idsCargadas, totalNoLeidas);

  return (
    <div
      className="adoc-notif-capa adoc-ui"
      style={{ "--notif-offset": `${desplazamiento}px` } as CSSProperties}
    >
      <div className="adoc-notif-fondo" aria-hidden />
      <div
        ref={panelRef}
        className="adoc-notif-panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby={idTitulo}
        id="panel-notificaciones"
        tabIndex={-1}
      >
        <div className="adoc-notif-cab">
          <h2 id={idTitulo}>Notificaciones</h2>
          <button
            ref={cerrarRef}
            type="button"
            id="notif-cerrar"
            className="adoc-notif-cerrar"
            aria-label="Cerrar notificaciones"
            onClick={cerrar}
          >
            <IconoCerrar />
          </button>
        </div>
        <div className="adoc-notif-cuerpo">
          <div className="adoc-notif-barra">
            <div className="adoc-notif-tabs" role="tablist" aria-label="Estado de lectura">
              {PESTANIAS.map((p) => (
                <button
                  key={p.id}
                  type="button"
                  role="tab"
                  id={`notif-tab-${p.id}`}
                  className="adoc-notif-tab"
                  aria-selected={pestania === p.id}
                  aria-controls="notif-panel-lista"
                  tabIndex={pestania === p.id ? 0 : -1}
                  onClick={() => cambiarPestania(p.id)}
                  onKeyDown={teclasPestania(p.id, cambiarPestania)}
                >
                  {p.etiqueta}
                </button>
              ))}
            </div>
            {puedeSeleccionar &&
              (seleccionActiva ? (
                <button
                  type="button"
                  id="notif-seleccion-cancelar"
                  className="adoc-notif-accion peligro"
                  onClick={() => setSeleccion(SELECCION_INICIAL)}
                >
                  Cancelar
                </button>
              ) : (
                <button
                  type="button"
                  id="notif-seleccion-iniciar"
                  className="adoc-notif-accion"
                  onClick={() => setSeleccion(iniciarSeleccion())}
                >
                  <IconoListaCheck />
                  Marcar como leídas
                </button>
              ))}
          </div>
          <div
            ref={scrollRef}
            id="notif-panel-lista"
            role="tabpanel"
            aria-labelledby={`notif-tab-${pestania}`}
            className="adoc-notif-lista-scroll"
          >
            <ListaNotificaciones
              key={pestania}
              estado={pestania}
              consulta={pestania === "NO_LEIDAS" ? noLeidas : leidas}
              raizScroll={scrollRef}
              modoSeleccion={seleccionActiva && pestania === "NO_LEIDAS"}
              seleccion={seleccion}
              onAbrir={abrir}
              onAlternar={(id) => setSeleccion((actual) => alternar(actual, id, idsCargadas))}
            />
          </div>
        </div>
        {seleccionActiva && (
          <div className="adoc-notif-pie">
            <Button
              id="notif-seleccion-confirmar"
              variant="primary"
              disabled={cuenta === 0 || marcarLeidas.isPending}
              onClick={confirmarSeleccion}
            >
              {cuenta > 0 ? `Marcar como leídas (${cuenta})` : "Marcar como leídas"}
            </Button>
            <Button
              id="notif-seleccion-todas"
              variant="secondary"
              disabled={marcarLeidas.isPending}
              onClick={() =>
                setSeleccion(todas ? iniciarSeleccion() : { activa: true, modo: "todas", ids: [] })
              }
            >
              {todas ? "Deseleccionar todas" : "Seleccionar todas"}
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
