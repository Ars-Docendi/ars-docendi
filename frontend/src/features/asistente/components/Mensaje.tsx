import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import { Button, InlineAlert, Textarea } from "@ars-docendi/ui";

import { ContenidoHistorico } from "./ContenidoHistorico";
import { CuerpoDeLaRespuesta } from "./CuerpoDeLaRespuesta";
import { HerramientasDePregunta } from "./HerramientasDePregunta";
import { UMBRAL_DE_APARICION_MS } from "./IndicadorDeProceso";
import { sendIcon } from "../../../app/shell/icons";
import { useVisibleTrasUmbral } from "../hooks/useVisibleTrasUmbral";
import { modoDebugAsistente } from "../utils/modoDebug";
import type { MencionEnPregunta, TurnoDeLaConversacion } from "../types";

interface MensajeProps {
  turno: TurnoDeLaConversacion;
  onElegir: (pregunta: string) => void;
  onReintentar: (id: string) => void;
  /** «Volver a consultar» sobre un turno histórico (asistente-historial-conversaciones). */
  onReejecutar?: (id: string) => void;
  /** Edita y reenvía la pregunta de este turno (asistente-edicion-de-la- ultima-pregunta). */
  onEditarYReenviar?: (texto: string) => void;
  enVuelo: boolean;
  /**
   * Cupo agotado, tope organizacional o mantenimiento: mientras está bloqueado tampoco se ofrece
   * «Editar y reenviar», igual que el composer no acepta preguntas nuevas.
   */
  bloqueado?: boolean;
  /**
   * El último turno del hilo: su barra de acciones queda siempre visible, nunca sólo detrás del
   * hover (design.md D6 de asistente-rediseno-v3).
   */
  esUltimo?: boolean;
  /**
   * Modo debug (`VITE_ASISTENTE_DEBUG=true`, ver `utils/modoDebug`): sólo con esto prendido se
   * muestra la disclosure «Cómo lo interpreté».
   */
  debug?: boolean;
  /**
   * El mismo umbral que `IndicadorDeProceso` (asistente-rediseno-v3, D14): los puntos inline no
   * aparecen antes de él, para que una respuesta determinista no los haga parpadear.
   */
  umbralMs?: number;
}

/** Un turno completo: lo que preguntó el usuario y lo que contestó el asistente. */
export function Mensaje({
  turno,
  onElegir,
  onReintentar,
  onReejecutar,
  onEditarYReenviar,
  enVuelo,
  bloqueado = false,
  esUltimo = false,
  debug = modoDebugAsistente,
  umbralMs = UMBRAL_DE_APARICION_MS,
}: MensajeProps) {
  const { respuesta } = turno;
  const mostrarPendiente = useVisibleTrasUmbral(enVuelo, umbralMs);

  // EDICIÓN DE LA PREGUNTA (asistente-edicion-de-la-ultima-pregunta, design.md D9 de
  // asistente-rediseno-v3).
  const [editando, setEditando] = useState(false);
  const [textoDeEdicion, setTextoDeEdicion] = useState(turno.pregunta);
  const editarBotonRef = useRef<HTMLButtonElement>(null);
  // El botón al que hay que devolverle el foco recién existe en el DOM DESPUÉS de que este
  // componente se vuelva a renderizar sin el campo de edición: llamar `.focus()` en el mismo evento
  // que pone `editando` en falso todavía lo encuentra desmontado.
  const volviendoDeCancelar = useRef(false);

  useEffect(() => {
    if (!editando && volviendoDeCancelar.current) {
      volviendoDeCancelar.current = false;
      editarBotonRef.current?.focus();
    }
  }, [editando]);

  function empezarEdicion() {
    setTextoDeEdicion(turno.pregunta);
    setEditando(true);
  }

  function cancelarEdicion() {
    volviendoDeCancelar.current = true;
    setEditando(false);
  }

  function confirmarEdicion() {
    const limpio = textoDeEdicion.trim();
    if (limpio.length === 0) return;

    setEditando(false);
    onEditarYReenviar?.(limpio);
  }

  function alTeclearEnLaEdicion(evento: KeyboardEvent<HTMLTextAreaElement>) {
    if (evento.key === "Enter" && !evento.shiftKey) {
      evento.preventDefault();
      confirmarEdicion();
    } else if (evento.key === "Escape") {
      evento.preventDefault();
      cancelarEdicion();
    }
  }

  // «Editar y reenviar» sólo en la última pregunta, nunca en vuelo ni
  // bloqueada — el mismo criterio que el composer.
  const puedeEditar = esUltimo && Boolean(onEditarYReenviar) && !enVuelo && !bloqueado;

  // «Repetir consulta» es «Editar y reenviar» sin editar (D5): la misma condición, más que el turno
  // haya terminado con una respuesta.
  const puedeRepetir = puedeEditar && Boolean(respuesta);

  // EL LUGAR DONDE VA A APARECER LA RESPUESTA, mientras no hay nada más que mostrar (ni respuesta,
  // ni error, ni «se dejó de esperar», ni contenido histórico): sólo el último turno puede estar en
  // vuelo (design.md D14 de asistente-rediseno-v3).
  const pendiente =
    esUltimo &&
    mostrarPendiente &&
    !respuesta &&
    !turno.error &&
    !turno.detenido &&
    !turno.historico;

  // LA REDACCIÓN MIENTRAS SE ESCRIBE (asistente-optimizaciones-modelo-local, D9): ocupa el lugar de
  // los puntos apenas llega el primer fragmento, sin esperar el umbral.
  const parcial =
    !respuesta && !turno.error && !turno.detenido ? turno.redaccionParcial : undefined;

  return (
    <li
      className={
        esUltimo ? "adoc-asistente-turno adoc-asistente-turno--ultimo" : "adoc-asistente-turno"
      }
    >
      {editando ? (
        <div className="adoc-asistente-pregunta-edicion">
          <Textarea
            className="adoc-asistente-pregunta-campo"
            value={textoDeEdicion}
            onChange={(evento) => setTextoDeEdicion(evento.target.value)}
            onKeyDown={alTeclearEnLaEdicion}
            aria-label="Editar tu pregunta"
            autoFocus
          />
          <div className="adoc-asistente-pregunta-edicion-acciones">
            <Button variant="ghost" size="sm" onClick={cancelarEdicion}>
              Cancelar
            </Button>
            <Button
              variant="primary"
              size="sm"
              leadingIcon={sendIcon}
              disabled={textoDeEdicion.trim().length === 0}
              onClick={confirmarEdicion}
            >
              Enviar
            </Button>
          </div>
        </div>
      ) : (
        <div className="adoc-asistente-pregunta-fila">
          <p className="adoc-asistente-pregunta">
            {/* Los dos puntos y el espacio NO son cosmética: sin separador, un lector
                de pantalla anuncia «Vosdame 3 materias…» de corrido. La clase la saca
                además de la selección, para que copiar la pregunta no arrastre la
                etiqueta al portapapeles — pasó, y el texto pegado volvió al modelo. */}
            <span className="adoc-asistente-quien">Vos:</span>{" "}
            {pintarPregunta(turno.pregunta, turno.menciones)}
          </p>

          <HerramientasDePregunta
            pregunta={turno.pregunta}
            onEditar={puedeEditar ? empezarEdicion : undefined}
            onRepetir={puedeRepetir ? () => onEditarYReenviar?.(turno.pregunta) : undefined}
            editarBotonRef={editarBotonRef}
          />
        </div>
      )}

      {turno.error && (
        <InlineAlert severity="danger" title="No se pudo consultar">
          {turno.error}
          {/* Reusa la clave y el texto del intento, y SÓLO acá, sobre un turno que
              terminó. En vuelo o tras dejar de esperar, la misma clave haría que el
              backend ejecutara el turno entero otra vez. */}
          <div className="adoc-asistente-reintento">
            <Button
              variant="secondary"
              size="sm"
              disabled={enVuelo}
              onClick={() => onReintentar(turno.id)}
            >
              Reintentar
            </Button>
          </div>
        </InlineAlert>
      )}

      {turno.detenido && (
        // No es un error: lo pidió el usuario, y por eso no va en una alerta.
        <p className="adoc-asistente-detenido">
          Dejaste de esperar la respuesta. La consulta ya salió y cuenta para tu cupo.
        </p>
      )}

      {parcial ? (
        <div
          className="adoc-asistente-respuesta adoc-asistente-respuesta--parcial"
          aria-hidden="true"
        >
          <p className="adoc-asistente-texto">{parcial}</p>
        </div>
      ) : (
        pendiente && (
          // «Consultando…» ocupa el lugar del bloque de la respuesta que viene.
          <div className="adoc-asistente-respuesta" aria-hidden="true">
            <div className="adoc-asistente-pendiente">
              <span className="adoc-asistente-puntos">
                <span />
                <span />
                <span />
              </span>
              <span>Consultando…</span>
            </div>
          </div>
        )
      )}

      {turno.historico && (
        // Se envuelve en vez de pasarle la clase a `ContenidoHistorico`: ese componente ya fija su
        // propia clase de raíz, y la opacidad cascadea igual sobre sus hijos.
        <div className={editando ? "adoc-asistente-respuesta--editando" : undefined}>
          <ContenidoHistorico turno={turno} onReejecutar={onReejecutar} />
        </div>
      )}

      {respuesta && (
        <CuerpoDeLaRespuesta
          respuesta={respuesta}
          pregunta={turno.pregunta}
          onElegir={onElegir}
          enVuelo={enVuelo}
          debug={debug}
          editando={editando}
        />
      )}
    </li>
  );
}

/**
 * La pregunta enviada, con cada mención elegida en el composer pintada como chip
 * (asistente-menciones, «In the sent question, each mention SHALL be displayed as a chip»).
 */
function pintarPregunta(pregunta: string, menciones: MencionEnPregunta[] | undefined) {
  if (!menciones || menciones.length === 0) return pregunta;

  const partes: React.ReactNode[] = [];
  let cursor = 0;

  menciones.forEach((mencion, indice) => {
    if (mencion.inicio > cursor) {
      partes.push(pregunta.slice(cursor, mencion.inicio));
    }
    partes.push(
      <span
        key={`${mencion.tipo}-${mencion.id}-${indice}`}
        className="adoc-asistente-mencion-chip adoc-asistente-mencion-chip--enviada"
      >
        {mencion.texto}
      </span>,
    );
    cursor = mencion.fin;
  });

  if (cursor < pregunta.length) partes.push(pregunta.slice(cursor));

  return partes;
}
