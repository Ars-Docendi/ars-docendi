import { useId } from "react";
import { Link } from "react-router-dom";

import type { DispositivoPush, EstadoDispositivo } from "../push/useDispositivoPush";
import { modulosVisibles, RUTA_AYUDA_PUSH } from "../push/modulosPush";
import { usePreferenciasPush } from "../push/usePreferenciasPush";
import { Interruptor } from "./Interruptor";

const TEXTO_ESTADO: Record<EstadoDispositivo, string> = {
  cargando: "Comprobando si este dispositivo puede recibir notificaciones…",
  activo: "Activas. Te avisamos en este dispositivo aunque no tengas la app abierta.",
  desactivado: "Desactivadas. Activalas para recibir los avisos en este dispositivo.",
  bloqueado:
    "Bloqueadas en el navegador. Para activarlas, permití las notificaciones de este sitio desde la configuración del navegador (el candado o el ícono de ajustes junto a la dirección) y volvé a abrir esta pantalla.",
  "ios-sin-instalar":
    "En iPhone y iPad las notificaciones sólo funcionan con la app agregada a la pantalla de inicio (iOS 16.4 o más nuevo). Tocá Compartir, elegí «Agregar a pantalla de inicio», abrí Ars Docendi desde su ícono y activalas desde acá.",
  "sin-soporte":
    "No disponibles en este navegador. Probá con una versión actualizada de Chrome, Edge, Firefox o Safari.",
  "servidor-apagado":
    "No disponibles por ahora: el sistema todavía no tiene habilitadas las notificaciones al celular.",
  error: "No pudimos comprobar el estado de las notificaciones en este dispositivo.",
};

interface PreferenciasNotificacionesProps {
  dispositivo: DispositivoPush;
  /** Permissions of the active role; unknown (`undefined`) shows every module. */
  permisos?: readonly string[];
}

/** "Preferencias de notificaciones": this device's push switch and one switch per module. */
export function PreferenciasNotificaciones({
  dispositivo,
  permisos,
}: PreferenciasNotificacionesProps) {
  const idEtiqueta = useId();
  const idEstado = useId();
  const activo = dispositivo.estado === "activo";
  const puedeCambiar = activo || dispositivo.estado === "desactivado";
  const mostrarModulos = dispositivo.estado !== "servidor-apagado";
  const preferencias = usePreferenciasPush(mostrarModulos);
  const modulos = modulosVisibles(permisos);

  return (
    <div className="adoc-notif-prefs">
      <section className="adoc-notif-prefs-bloque" aria-labelledby={idEtiqueta}>
        <div className="adoc-notif-prefs-fila">
          <div className="adoc-notif-prefs-texto">
            <h3 id={idEtiqueta}>Notificaciones en este dispositivo</h3>
            <p id={idEstado} className="adoc-notif-prefs-ayuda">
              {TEXTO_ESTADO[dispositivo.estado]}
            </p>
          </div>
          <Interruptor
            id="notif-push-dispositivo"
            etiquetadoPor={idEtiqueta}
            descritoPor={idEstado}
            activo={activo}
            disabled={!puedeCambiar || dispositivo.trabajando}
            onCambiar={(quiere) => void (quiere ? dispositivo.activar() : dispositivo.desactivar())}
          />
        </div>
        {dispositivo.error && (
          <p role="alert" className="adoc-notif-prefs-error">
            {dispositivo.error}
          </p>
        )}
        <p className="adoc-notif-prefs-ayuda">
          <Link to={RUTA_AYUDA_PUSH} id="notif-push-ayuda">
            ¿Cómo activarlas en el celular?
          </Link>
        </p>
      </section>

      {mostrarModulos && (
        <section
          className="adoc-notif-prefs-bloque"
          aria-label="Qué módulos te avisan en el dispositivo"
        >
          <h3>De qué querés recibir avisos</h3>
          <p className="adoc-notif-prefs-ayuda">
            {activo
              ? "Elegí de qué módulos te avisamos al dispositivo. En el panel vas a ver todos los avisos igual."
              : "Activá las notificaciones en este dispositivo para elegir. En el panel vas a ver todos los avisos igual."}
          </p>
          {preferencias.fallo && (
            <p role="alert" className="adoc-notif-prefs-error">
              No pudimos cargar tus preferencias.{" "}
              <button type="button" className="adoc-notif-enlace" onClick={preferencias.reintentar}>
                Reintentar
              </button>
            </p>
          )}
          <ul className="adoc-notif-ul">
            {modulos.map((modulo) => (
              <li key={modulo.id} className="adoc-notif-prefs-fila">
                <div className="adoc-notif-prefs-texto">
                  <span id={`notif-prefs-${modulo.id}`} className="adoc-notif-prefs-nombre">
                    {modulo.titulo}
                  </span>
                  <span id={`notif-prefs-${modulo.id}-d`} className="adoc-notif-prefs-ayuda">
                    {modulo.descripcion}
                  </span>
                </div>
                <Interruptor
                  id={`notif-prefs-modulo-${modulo.id}`}
                  etiquetadoPor={`notif-prefs-${modulo.id}`}
                  descritoPor={`notif-prefs-${modulo.id}-d`}
                  activo={preferencias.estaHabilitado(modulo.id)}
                  disabled={!activo || preferencias.cargando || preferencias.fallo}
                  onCambiar={(quiere) => preferencias.cambiar(modulo.id, quiere)}
                />
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
