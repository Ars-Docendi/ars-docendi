import { useEffect, useRef, useState } from "react";
import { InlineAlert } from "@ars-docendi/ui";

import { Conversacion } from "./Conversacion";
import { EncabezadoDeConversacion } from "./EncabezadoDeConversacion";
import { EntradaDePregunta } from "./EntradaDePregunta";
import { EstadoInicial } from "./EstadoInicial";
import { FranjaDeEstado } from "./FranjaDeEstado";
import { IrAlFinal } from "./IrAlFinal";
import { RailDeConversaciones } from "./RailDeConversaciones";
import { MENSAJE_SIN_ACCESO } from "../errores";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";
import { useAnclaAlFinal } from "../hooks/useAnclaAlFinal";
import { usePreferenciaDelRail } from "../hooks/usePreferenciaDelRail";
import type { Asistente } from "../hooks/useAsistente";
import type { HistorialAsistente } from "../hooks/useHistorialAsistente";
import type { MencionEnPregunta } from "../types";
import { developmentAuthEnabled } from "../../../shared/auth/developmentAuth";
import { obtenerSesionDesarrollo } from "../../../shared/auth/dev/session";

interface PanelAsistenteProps {
  /**
   * La conversación. La crea el dueño del montaje —el lanzador para el modal, la
   * página para la ruta— y no el panel, para que sobreviva a cerrar el modal.
   */
  asistente: Asistente;
  /**
   * El historial de conversaciones propias, para el rail. LO CREA EL MISMO
   * DUEÑO que crea `asistente`, con el mismo criterio: es un solo hook, no
   * una copia por montaje.
   */
  historial: HistorialAsistente;
  /**
   * Cierra el asistente. Sólo el modal del lanzador lo tiene: la ruta
   * `/asistente` —mientras siga existiendo, tasks.md §10— no tiene noción de
   * «cerrar», y el encabezado no pinta ese control sin esta prop.
   */
  onCerrar?: () => void;
  /** Para el test del umbral, que no puede esperar el tiempo real. */
  umbralDelIndicadorMs?: number;
}

/**
 * La vista del asistente: el rail de conversaciones y la conversación
 * activa, en la grilla de dos columnas de v3 (asistente-superficie-frontend,
 * design.md D1 de asistente-rediseno-v3).
 *
 * ES UNA SOLA, MONTADA DOS VECES: el lanzador de la barra la muestra en un
 * modal y, mientras la ruta `/asistente` siga existiendo (tasks.md §10), la
 * muestra también a página completa. Dos implementaciones se
 * desincronizarían —una recibiría una mejora y la otra no—, y nadie lo
 * notaría hasta que alguien reportara que «desde el botón anda distinto».
 *
 * NO TIENE CONVERSACIÓN PROPIA: la recibe. En el modal se monta al abrir y se
 * desmonta al cerrar, y si el hilo viviera acá se iría con él —Esc y un clic
 * afuera, también sin querer, cierran—. Lo que sí es suyo es lo que se está
 * escribiendo, el foco y la preferencia de ancho del rail.
 */
export function PanelAsistente({
  asistente,
  historial,
  onCerrar,
  umbralDelIndicadorMs,
}: PanelAsistenteProps) {
  const { capacidades, tieneAcceso } = useAccesoAlAsistente();
  const { turnos, enVuelo, preguntar, reintentar, reenviarUltima, detener } = asistente;
  const [borrador, setBorrador] = useState("");
  // El anuncio de cuántas materias/docentes coincidieron en el popover de
  // menciones (asistente-menciones) sale por la MISMA región viva que ya
  // usan renombrar/archivar/reanudar del rail (asistente-accesibilidad: «no
  // agregar una segunda»); por eso vive acá, junto a `historial.anuncio`, y
  // no adentro del composer, que no tiene ninguna región viva propia.
  const [anuncioDeMenciones, setAnuncioDeMenciones] = useState<string | null>(null);
  // El bypass del admin ya lo aplicó el backend en `cupo.bloqueado` (a diferencia de
  // `mantenimiento`, que es global y sin bypass): un actor con
  // `asistente.administrar` no se ve bloqueado por su propio mantenimiento y puede
  // seguir escribiendo para verificar la recuperación.
  const bloqueado = capacidades?.cupo.bloqueado ?? false;
  const entrada = useRef<HTMLTextAreaElement>(null);
  const sinTurnos = turnos.length === 0;

  // NO ES UN HOOK A PROPÓSITO: la sesión de desarrollo es una lectura
  // síncrona de `localStorage`, y pasar por `useCurrentUser` traería su
  // propia consulta de React Query a CADA test que monta este panel —que
  // son casi todos— sin que la preferencia del rail necesite nada de eso.
  // En producción, sin la integración de identidad todavía armada, no hay
  // id que leer y el rail usa el default (expandido) para todo el mundo.
  const usuarioId = developmentAuthEnabled ? obtenerSesionDesarrollo()?.usuarioId : undefined;
  const rail = usePreferenciaDelRail(usuarioId);

  // El foco vuelve al campo cuando el turno termina —también cuando se lo dejó de
  // esperar— y cuando la conversación se vacía: quien está usando un lector de
  // pantalla o el teclado no tiene que volver a buscarlo para seguir preguntando.
  useEffect(() => {
    if (!enVuelo && !bloqueado) entrada.current?.focus();
  }, [enVuelo, bloqueado, sinTurnos]);

  // El hilo sigue a quien está abajo y no arrastra a quien subió: al enviar va al
  // fondo, la respuesta se muestra desde su inicio, y si el usuario subió a releer
  // se queda donde está con «Ir al final» a mano.
  const { hilo, anclado, irAlFinal, onScroll } = useAnclaAlFinal(turnos);

  async function enviar(mensaje: string, menciones?: MencionEnPregunta[]) {
    // Mientras hay un turno en vuelo no se envía nada —ni por Enter, ni por el
    // botón, ni por un chip—, pero se puede seguir escribiendo: el borrador no se
    // toca. El hook tiene su propio guard; éste es el que cuida lo escrito.
    if (enVuelo || bloqueado) return;
    setBorrador("");
    await preguntar(mensaje, menciones);
  }

  // SIN ACCESO NO HAY FORMULARIO. Con 403 el campo y el botón quedaban activos y
  // el rechazo recién aparecía al enviar: un formulario que aparenta funcionar es
  // el fake UI que el invariante #7 prohíbe. El lanzador de la barra no aparece
  // sin acceso, así que la ruta `/asistente` es la única forma de llegar acá.
  if (tieneAcceso === false) {
    return (
      <section className="adoc-asistente" aria-label="Asistente conversacional">
        <InlineAlert severity="info">{MENSAJE_SIN_ACCESO}</InlineAlert>
      </section>
    );
  }

  // «Asistente» en la bienvenida; si no, el título de la conversación activa
  // —la reanudada, o la que este mismo hilo en vivo acaba de persistir—
  // (asistente-superficie-frontend). Mientras el rail todavía no trajo la
  // lista —o la conversación nueva todavía no aparece en ella—, se sigue
  // mostrando «Asistente»: nunca un hueco.
  const conversacionActiva = historial.conversaciones.data?.find(
    (c) => c.id === historial.conversacionActivaId,
  );
  const tituloDelEncabezado = sinTurnos ? "Asistente" : (conversacionActiva?.titulo ?? "Asistente");

  return (
    <section className="adoc-asistente" aria-label="Asistente conversacional">
      <div className="adoc-asistente-grilla">
        <RailDeConversaciones
          asistente={asistente}
          historial={historial}
          colapsado={rail.colapsado}
          onAlternar={rail.alternar}
        />

        <div className="adoc-asistente-columna">
          <EncabezadoDeConversacion titulo={tituloDelEncabezado} onCerrar={onCerrar} />

          {/* EL ENCABEZADO LLEGA AL BORDE DEL MODAL SIN RELLENO PROPIO; ESTO NO. El
              relleno alrededor del banner, el hilo, la franja y la entrada vive
              acá y no en `.adoc-asistente-columna` justamente para que el
              encabezado —arriba, afuera de este `div`— pueda ser edge-to-edge
              mientras el resto no. */}
          <div className="adoc-asistente-columna-cuerpo">
            {/* Arriba del hilo (asistente-conversacion): global y SIN bypass
                (asistente-modo-mantenimiento), se ve igual para todo el mundo,
                admin incluido, aunque el campo de abajo sólo se deshabilite
                para quien el backend efectivamente bloquea. */}
            {capacidades?.mantenimiento.activo && (
              <InlineAlert severity="warning" className="adoc-asistente-mantenimiento">
                {mensajeDeMantenimiento(capacidades.mantenimiento.razon)}
              </InlineAlert>
            )}

            {/* LO QUE SCROLLEA ES ESTO, y no el modal entero. Con el modal scrolleando,
                el campo de entrada se va hacia abajo con cada respuesta y hay que
                perseguirlo; acá se queda quieto y lo que se mueve es la conversación,
                que es lo que uno espera de un chat. */}
            <div className="adoc-asistente-hilo-marco">
              <div className="adoc-asistente-hilo" ref={hilo} onScroll={onScroll}>
                {/* Columna centrada de 720 px (mock v3): el ancho de scroll es
                    el de todo el cuerpo —así el thumb queda contra el borde del
                    modal, no pegado al texto—, y este envoltorio es el que
                    angosta y centra el contenido adentro de él. */}
                <div className="adoc-asistente-hilo-contenido">
                  {sinTurnos && capacidades && (
                    <EstadoInicial
                      capacidades={capacidades}
                      onElegir={enviar}
                      deshabilitado={enVuelo}
                    />
                  )}

                  <Conversacion
                    turnos={turnos}
                    onElegir={enviar}
                    onReintentar={(id) => void reintentar(id)}
                    onReejecutar={(id) => void asistente.reejecutar(id)}
                    onEditarYReenviar={(texto) => void reenviarUltima(texto)}
                    enVuelo={enVuelo}
                    bloqueado={bloqueado}
                    anuncio={anuncioDeMenciones ?? historial.anuncio}
                    umbralDelIndicadorMs={umbralDelIndicadorMs}
                  />
                </div>
              </div>

              {/* Flota sobre el hilo, fuera de la región viva. Al pulsarlo desaparece, y
                  el foco que tenía se iría a ninguna parte: pasa al campo, que es lo que
                  hay en el final al que se acaba de ir. */}
              <IrAlFinal
                visible={!anclado}
                onClick={() => {
                  irAlFinal();
                  entrada.current?.focus();
                }}
              />
            </div>

            {/* El composer y, debajo, la franja con el cupo y las métricas
                (mock v3, design.md D14): las dos centradas en su propia
                columna angosta de 684 px, más chica que la del hilo. */}
            <div className="adoc-asistente-compositor">
              <div className="adoc-asistente-compositor-marco">
                <EntradaDePregunta
                  ref={entrada}
                  valor={borrador}
                  onCambiar={setBorrador}
                  onEnviar={(menciones) => void enviar(borrador, menciones)}
                  onAnunciar={setAnuncioDeMenciones}
                  onDetener={detener}
                  enVuelo={enVuelo}
                  deshabilitado={bloqueado}
                  umbralMs={umbralDelIndicadorMs}
                />

                {/* FUERA de la región viva a propósito. */}
                <FranjaDeEstado
                  enVuelo={enVuelo}
                  turnos={turnos}
                  cupo={capacidades?.cupo}
                  umbralMs={umbralDelIndicadorMs}
                />
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

/**
 * «El asistente está en mantenimiento: {razón}.» — texto exacto del design
 * spec (docs/product/designs/asistente-conversacional-design-spec.md
 * §Administración de uso), para que el banner diga lo mismo en cualquier
 * lugar donde se lo muestre.
 */
function mensajeDeMantenimiento(razon: string | null | undefined): string {
  return razon
    ? `El asistente está en mantenimiento: ${razon}.`
    : "El asistente está en mantenimiento.";
}
