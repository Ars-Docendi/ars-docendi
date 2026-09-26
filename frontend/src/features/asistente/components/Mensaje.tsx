import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import { Button, InlineAlert, Textarea } from "@ars-docendi/ui";

import { BarraDeAcciones } from "./BarraDeAcciones";
import { ContenidoHistorico } from "./ContenidoHistorico";
import { HerramientasDePregunta } from "./HerramientasDePregunta";
import { Opciones } from "./Opciones";
import { Razonamiento } from "./Razonamiento";
import { TablaDeResultado } from "./TablaDeResultado";
import { UMBRAL_DE_APARICION_MS } from "./IndicadorDeProceso";
import { sendIcon } from "../../../app/shell/icons";
import { useVisibleTrasUmbral } from "../hooks/useVisibleTrasUmbral";
import { modoDebugAsistente } from "../utils/modoDebug";
import type { EstadoDelTurno, TurnoDeLaConversacion } from "../types";

interface MensajeProps {
  turno: TurnoDeLaConversacion;
  onElegir: (pregunta: string) => void;
  onReintentar: (id: string) => void;
  /** «Volver a consultar» sobre un turno histórico (asistente-historial-conversaciones). */
  onReejecutar?: (id: string) => void;
  /**
   * Edita y reenvía la pregunta de este turno (asistente-edicion-de-la-
   * ultima-pregunta). Ausente cuando este turno no es el último — `Mensaje`
   * no ofrece «Editar y reenviar» sin esta prop, sea cual sea `esUltimo`.
   */
  onEditarYReenviar?: (texto: string) => void;
  enVuelo: boolean;
  /**
   * Cupo agotado, tope organizacional o mantenimiento: mientras está
   * bloqueado tampoco se ofrece «Editar y reenviar», igual que el composer
   * no acepta preguntas nuevas.
   */
  bloqueado?: boolean;
  /**
   * El último turno del hilo: su barra de acciones queda siempre visible,
   * nunca sólo detrás del hover (design.md D6 de asistente-rediseno-v3). Los
   * turnos votados se resuelven aparte, por CSS (`:has([aria-pressed="true"])`
   * en `asistente.css`), sin necesitar este mismo mecanismo de props.
   */
  esUltimo?: boolean;
  /**
   * Modo debug (`VITE_ASISTENTE_DEBUG=true`, ver `utils/modoDebug`): sólo con
   * esto prendido se muestra la disclosure «Cómo lo interpreté». Parámetro,
   * no lectura directa del env, para que los tests no dependan de
   * `import.meta.env`; en producción usa el valor real por defecto.
   */
  debug?: boolean;
  /**
   * El mismo umbral que `IndicadorDeProceso` (asistente-rediseno-v3, D14):
   * los puntos inline no aparecen antes de él, para que una respuesta
   * determinista no los haga parpadear. Inyectable para que el test no
   * dependa del reloj real.
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

  // La vista ampliada de la tabla es controlada desde ACÁ, no desde
  // `TablaDeResultado` (design.md D6 de asistente-rediseno-v3, ARS-146 §5): el
  // disparador vive en `BarraDeAcciones`, así que el estado tiene que vivir en
  // el ancestro común de las dos. `onExportar` es la función YA resuelta que
  // `TablaDeResultado` expone por el mismo motivo — exportar sin duplicar el
  // orden mostrado (asistente-exportacion-csv).
  const [ampliado, setAmpliado] = useState(false);
  const [exportar, setExportar] = useState<(() => void) | null>(null);
  // A dónde vuelve el foco cuando la vista ampliada se contrae desde adentro
  // (Escape o «Contraer»): el disparador es un ícono de `BarraDeAcciones`, no
  // el botón propio que `TablaDeResultado` deja de dibujar en modo controlado.
  const ampliarBotonRef = useRef<HTMLButtonElement>(null);

  // EDICIÓN DE LA PREGUNTA (asistente-edicion-de-la-ultima-pregunta,
  // design.md D9 de asistente-rediseno-v3). Vive ACÁ y no en un componente
  // aparte: la respuesta de más abajo se atenúa mientras se edita, y las dos
  // cosas necesitan el mismo booleano.
  const [editando, setEditando] = useState(false);
  const [textoDeEdicion, setTextoDeEdicion] = useState(turno.pregunta);
  const editarBotonRef = useRef<HTMLButtonElement>(null);
  // El botón al que hay que devolverle el foco recién existe en el DOM
  // DESPUÉS de que este componente se vuelva a renderizar sin el campo de
  // edición: llamar `.focus()` en el mismo evento que pone `editando` en
  // falso todavía lo encuentra desmontado. Este flag hace que el efecto de
  // abajo lo enfoque una vez que React ya lo montó.
  const volviendoDeCancelar = useRef(false);

  useEffect(() => {
    if (!editando && volviendoDeCancelar.current) {
      volviendoDeCancelar.current = false;
      editarBotonRef.current?.focus();
    }
  }, [editando]);

  function alCambiarAmpliado(valor: boolean) {
    setAmpliado(valor);
    if (!valor) ampliarBotonRef.current?.focus();
  }

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

  // EL LUGAR DONDE VA A APARECER LA RESPUESTA, mientras no hay nada más que
  // mostrar (ni respuesta, ni error, ni «se dejó de esperar», ni contenido
  // histórico): sólo el último turno puede estar en vuelo (design.md D14 de
  // asistente-rediseno-v3). Es puramente visual y `aria-hidden`: el anuncio
  // sigue siendo el `role="status"` de `IndicadorDeProceso`, fuera de esta
  // región viva — dos anuncios del mismo «Consultando…» serían el doble aviso
  // que la spec de accesibilidad prohíbe.
  const pendiente =
    esUltimo &&
    mostrarPendiente &&
    !respuesta &&
    !turno.error &&
    !turno.detenido &&
    !turno.historico;

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
            <span className="adoc-asistente-quien">Vos:</span> {turno.pregunta}
          </p>

          <HerramientasDePregunta
            pregunta={turno.pregunta}
            onEditar={puedeEditar ? empezarEdicion : undefined}
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
        // No es un error: lo pidió el usuario, y por eso no va en una alerta. Se
        // dice lo que pasó de verdad —la consulta ya salió y el backend la sigue
        // hasta el final, cupo incluido— y no se ofrece «Reintentar»: ese turno
        // sigue corriendo allá, y la misma clave lo ejecutaría dos veces.
        <p className="adoc-asistente-detenido">
          Dejaste de esperar la respuesta. La consulta ya salió y cuenta para tu cupo.
        </p>
      )}

      {pendiente && (
        <div className="adoc-asistente-pendiente" aria-hidden="true">
          <span className="adoc-asistente-puntos">
            <span />
            <span />
            <span />
          </span>
          <span>Consultando…</span>
        </div>
      )}

      {turno.historico && (
        // Se envuelve en vez de pasarle la clase a `ContenidoHistorico`: ese
        // componente ya fija su propia clase de raíz, y la opacidad cascadea
        // igual sobre sus hijos.
        <div className={editando ? "adoc-asistente-respuesta--editando" : undefined}>
          <ContenidoHistorico turno={turno} onReejecutar={onReejecutar} />
        </div>
      )}

      {respuesta && (
        <div
          className={
            editando
              ? "adoc-asistente-respuesta adoc-asistente-respuesta--editando"
              : "adoc-asistente-respuesta"
          }
        >
          <span className="adoc-asistente-quien">Asistente:</span>

          {respuesta.preguntaInterpretada && (
            // Solo llega cuando difiere de lo que se escribió, así que mostrarla
            // nunca es ruido: es el asistente diciendo cómo entendió la pregunta.
            // Queda VISIBLE y fuera de la disclosure del razonamiento: esconder el
            // aviso de que se reinterpretó la pregunta derrota su razón de ser.
            <p className="adoc-asistente-interpretada">
              Entendí: <em>{respuesta.preguntaInterpretada}</em>
            </p>
          )}

          <MarcoDelEstado estado={respuesta.estado}>
            <p className="adoc-asistente-texto">{respuesta.respuesta}</p>
          </MarcoDelEstado>

          <TablaDeResultado
            columnas={respuesta.columnas}
            filas={respuesta.filas}
            truncado={respuesta.truncado}
            vinculos={respuesta.vinculos}
            hilo={respuesta.hilo}
            pregunta={turno.pregunta}
            ampliado={ampliado}
            onAmpliarChange={alCambiarAmpliado}
            // `setExportar(fn)` A SECAS, NO: `useState` interpreta un valor
            // función como la forma "actualizador" (`(prev) => next`) y la
            // LLAMARÍA en vez de guardarla, dejando `exportar` en lo que sea
            // que esa llamada devuelva (nada: `exportar` no devuelve nada).
            // Envolverla en otra función es lo que hace que se guarde la
            // función en sí.
            onExportarDisponible={(fn) => setExportar(() => fn)}
          />

          <Opciones opciones={respuesta.opciones} onElegir={onElegir} deshabilitado={enVuelo} />

          {((debug && respuesta.razonamiento) || respuesta.sql) && (
            // El pie: lo que se puede desplegar a pedido, después de todo lo que
            // hay que leer. Sólo existe cuando hay algo que poner: un pie vacío
            // dejaría un hueco. Con el modo debug apagado, el razonamiento no
            // cuenta como «algo que poner» aunque el backend lo haya mandado.
            // La barra de acciones YA NO vive acá (design.md D6 de
            // asistente-rediseno-v3): sus propias condiciones —portapapeles,
            // tabla, token— no tienen nada que ver con el razonamiento o la
            // consulta, así que depender de este mismo `if` la escondía sin
            // portapapeles aunque hubiera tabla o voto para ofrecer.
            <div className="adoc-asistente-pie">
              <Razonamiento razonamiento={respuesta.razonamiento} debug={debug} />

              {respuesta.sql && (
                // Solo llega con `asistente.ver_consulta`. Que esté acá no es
                // transparencia gratuita: el WHERE de una consulta generada puede
                // llevar un documento, y por eso quien la ve pasó por un permiso.
                <details className="adoc-asistente-sql">
                  <summary>Ver la consulta</summary>
                  <pre>{respuesta.sql}</pre>
                </details>
              )}
            </div>
          )}

          <BarraDeAcciones
            texto={respuesta.respuesta}
            onExportar={exportar}
            onAmpliar={() => alCambiarAmpliado(true)}
            ampliarBotonRef={ampliarBotonRef}
            claveDeRetroalimentacion={respuesta.claveDeRetroalimentacion}
          />
        </div>
      )}
    </li>
  );
}

/**
 * Envuelve el texto según el estado.
 *
 * EL DEGRADADO SE MUESTRA COMO ESTADO Y NO COMO ERROR. Un banner rojo le dice al
 * usuario que hizo algo mal; el servicio degradado no es culpa suya y su pregunta no
 * tiene nada de malo. La aclaración tampoco es un rechazo: es «puedo, en cuanto
 * elijas».
 */
function MarcoDelEstado({
  estado,
  children,
}: {
  estado: EstadoDelTurno;
  children: React.ReactNode;
}) {
  if (estado === "servicio_degradado") {
    return (
      <InlineAlert severity="warning" title="El asistente no está disponible ahora">
        {children}
      </InlineAlert>
    );
  }

  if (estado === "necesita_aclaracion") {
    return (
      <InlineAlert severity="info" title="Necesito que precises algo">
        {children}
      </InlineAlert>
    );
  }

  return <>{children}</>;
}
