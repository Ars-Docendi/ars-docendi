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

import { esCapaSuperior, registrarCapa } from "../../../shared/ui/capas";
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
import { useDispositivoPush } from "../push/useDispositivoPush";
import { IconoAjustes, IconoCerrar, IconoListaCheck, IconoVolver } from "./iconos";
import { ListaNotificaciones } from "./ListaNotificaciones";
import { PreferenciasNotificaciones } from "./PreferenciasNotificaciones";
import { SugerenciaPush } from "./SugerenciaPush";
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
  /** Permissions of the active role: they decide which push groups are offered. Unknown shows all. */
  permisos?: readonly string[];
  /** Ruta de la página: si cambia mientras el panel está abierto, la navegación se llevó el foco. */
  rutaActual?: string;
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
  permisos,
  rutaActual,
  onNavegar,
}: PanelNotificacionesProps) {
  const [vista, setVista] = useState<"lista" | "preferencias">("lista");
  const dispositivo = useDispositivoPush();
  const ajustesRef = useRef<HTMLButtonElement>(null);
  const volverRef = useRef<HTMLButtonElement>(null);
  const vistaPrevia = useRef(vista);
  const [pestania, setPestania] = useState<EstadoLectura>("NO_LEIDAS");
  const [seleccion, setSeleccion] = useState<Seleccion>(SELECCION_INICIAL);
  const panelRef = useRef<HTMLDivElement>(null);
  const cerrarRef = useRef<HTMLButtonElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  // Navegó si la ruta cambió desde que se abrió: entonces el foco es de la página nueva.
  const rutaInicial = useRef(rutaActual);
  const navego = useRef(false);
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

  // La página cambió con el panel abierto (una notificación abrió su entidad): se cierra sin
  // devolver el foco a la campana.
  useEffect(() => {
    if (rutaActual === rutaInicial.current) return;
    navego.current = true;
    cerrarRefFn.current();
  }, [rutaActual]);

  // Overlay stack: Esc and the Tab trap act on the topmost layer, panel included.
  useEffect(
    () =>
      registrarCapa({ contenedor: () => panelRef.current, cerrar: () => cerrarRefFn.current() }),
    [],
  );

  // Focus into the dialog on open; back to the bell on close, unless a navigation took over.
  // El retorno se cancela si el efecto se vuelve a montar (StrictMode): el panel sigue abierto.
  const retorno = useRef<number | undefined>(undefined);
  useEffect(() => {
    window.clearTimeout(retorno.current);
    cerrarRef.current?.focus();
    const disparador = triggerRef.current;
    return () => {
      if (!navego.current) {
        retorno.current = window.setTimeout(() => disparador?.focus(), 0);
      }
    };
  }, [triggerRef]);

  // Changing view moves the focus to the control that undoes it (never on the first render, where
  // the focus goes to the close button).
  useEffect(() => {
    if (vistaPrevia.current === vista) return;
    vistaPrevia.current = vista;
    (vista === "preferencias" ? volverRef : ajustesRef).current?.focus();
  }, [vista]);

  // An outside press closes it. The bell toggles by itself, so it does not count as outside.
  useEffect(() => {
    function alPresionar(evento: MouseEvent) {
      const objetivo = evento.target as Node;
      if (panelRef.current?.contains(objetivo)) return;
      if (triggerRef.current?.contains(objetivo)) return;
      // Un clic en otro diálogo apilado encima (p. ej. "¿Salir de esta página?") no es "afuera".
      if (!esCapaSuperior(panelRef.current)) return;
      cerrarRefFn.current();
    }
    document.addEventListener("mousedown", alPresionar);
    return () => document.removeEventListener("mousedown", alPresionar);
  }, [triggerRef]);

  function abrir(notificacion: Notificacion) {
    if (notificacion.leidaEn === null) marcarLeida.mutate(notificacion);
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
          {vista === "preferencias" && (
            <button
              ref={volverRef}
              type="button"
              id="notif-prefs-volver"
              className="adoc-notif-icono"
              aria-label="Volver a las notificaciones"
              onClick={() => setVista("lista")}
            >
              <IconoVolver />
            </button>
          )}
          <h2 id={idTitulo}>
            {vista === "lista" ? "Notificaciones" : "Preferencias de notificaciones"}
          </h2>
          {vista === "lista" && (
            <button
              ref={ajustesRef}
              type="button"
              id="notif-ajustes"
              className="adoc-notif-icono"
              aria-label="Preferencias de notificaciones"
              onClick={() => setVista("preferencias")}
            >
              <IconoAjustes />
            </button>
          )}
          <button
            ref={cerrarRef}
            type="button"
            id="notif-cerrar"
            className="adoc-notif-icono"
            aria-label="Cerrar notificaciones"
            onClick={cerrar}
          >
            <IconoCerrar />
          </button>
        </div>
        {vista === "preferencias" ? (
          <div className="adoc-notif-cuerpo adoc-notif-cuerpo-prefs">
            <PreferenciasNotificaciones dispositivo={dispositivo} permisos={permisos} />
          </div>
        ) : (
          <div className="adoc-notif-cuerpo">
            {dispositivo.estado === "desactivado" && (
              <SugerenciaPush onActivar={() => setVista("preferencias")} />
            )}
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
        )}
        {vista === "lista" && seleccionActiva && (
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
