import {
  useCallback,
  useId,
  useLayoutEffect,
  useRef,
  type ChangeEvent,
  type KeyboardEvent,
  type Ref,
} from "react";
import { Button, Textarea } from "@ars-docendi/ui";

import { UMBRAL_DE_APARICION_MS } from "./IndicadorDeProceso";
import { PopoverDeMenciones } from "./PopoverDeMenciones";
import { closeIcon, sendIcon, sparkIcon, stopIcon } from "../../../app/shell/icons";

import { useAltoAutomatico } from "../hooks/useAltoAutomatico";
import { useMenciones } from "../hooks/useMenciones";
import { useVisibleTrasUmbral } from "../hooks/useVisibleTrasUmbral";
import type { ChipDeMencion, MencionEnPregunta } from "../types";

interface EntradaDePreguntaProps {
  valor: string;
  onCambiar: (valor: string) => void;
  /**
   * Se llama con las menciones que sobrevivieron hasta el envío —ya ubicadas
   * en `valor`, listas para guardar en el turno y para derivar la
   * `referencias` del pedido (asistente-menciones)—, o sin argumento si no
   * quedó ninguna. El texto ya lo tiene quien sostiene `valor`.
   */
  onEnviar: (menciones?: MencionEnPregunta[]) => void;
  /**
   * Deja de esperar el turno en vuelo (asistente-rediseno-v3, D14): reemplaza
   * a «Enviar» en el mismo lugar del botón, pasado el umbral del indicador.
   * Ausente mientras no hay ningún turno en vuelo.
   */
  onDetener: () => void;
  /**
   * Cuántos resultados trajo la búsqueda del popover de menciones, para la
   * región viva EXISTENTE del hilo (asistente-menciones/asistente-
   * accesibilidad: «no agregar una segunda»). Ausente en un test que no la
   * necesita.
   */
  onAnunciar?: (texto: string) => void;
  enVuelo: boolean;
  /**
   * Cupo agotado, tope organizacional o mantenimiento (asistente-cupo-visible /
   * asistente-modo-mantenimiento): a diferencia de `enVuelo` —que deja
   * seguir escribiendo mientras se espera un turno—, ESTO deshabilita el
   * campo de verdad, porque no hay ningún turno que enviar todavía va a
   * poder completarse.
   */
  deshabilitado?: boolean;
  /** El del backend: `ModelosAsistente.cs` rechaza mensajes más largos. */
  maxCaracteres?: number;
  /** Desde cuántos caracteres se muestra el contador. */
  umbralDelContador?: number;
  /**
   * El mismo umbral que `IndicadorDeProceso`: «Dejar de esperar» no
   * aparece antes de él, para que no parpadee en cada respuesta
   * determinista. Inyectable para que el test no dependa del reloj real.
   */
  umbralMs?: number;
  /** Al textarea, para que el dueño le devuelva el foco. */
  ref?: Ref<HTMLTextAreaElement>;
}

/**
 * El composer: destello, campo que crece, chips de mención, contador cerca
 * del límite y «Enviar».
 *
 * Es controlado y no sabe nada de la red: recibe el valor, avisa los cambios y
 * pide enviar. El guard de «no mandar en vuelo» vive en el hook y en el panel;
 * acá sólo se refleja en el botón y en Enter.
 *
 * ENTER ENVÍA Y SHIFT+ENTER HACE SALTO, salvo con puntero grueso: en un teléfono
 * Enter es la única forma de hacer un salto de línea, y el botón queda a un toque.
 * Es lo que hacen los asistentes que el usuario ya conoce en móvil.
 *
 * EL BOTÓN TIENE ETIQUETA VISIBLE. Un ícono solo obliga a descubrir qué hace, y
 * Enter es un atajo, no la única vía: quien navega con lector de pantalla o desde
 * un teléfono necesita el botón. Se llama «Enviar» y no «Preguntar» para que con
 * el modal abierto no haya dos botones con el mismo nombre en el DOM —el lanzador
 * y éste—. Sin spinner: parpadea en las respuestas deterministas, que es lo que el
 * umbral del indicador evita; el estado en vuelo lo dice el indicador.
 *
 * EL CONTADOR APARECE RECIÉN CERCA DEL LÍMITE y va ligado al campo con
 * `aria-describedby`, sin región viva: un «12 / 2 000» permanente es ruido, y uno
 * que se anuncia a cada tecla es insoportable.
 *
 * MENCIONES «@materia» / «#docente» (asistente-menciones, design.md D10/D11 de
 * asistente-rediseno-v3): el campo es un combobox ARIA que controla el popover
 * de `useMenciones`/`PopoverDeMenciones`. Elegir una inserta su nombre en el
 * texto y la deja además como chip removible en su propia fila, arriba del
 * campo; al enviar, sólo las que siguen presentes en el texto viajan como
 * referencia.
 */
export function EntradaDePregunta({
  valor,
  onCambiar,
  onEnviar,
  onDetener,
  onAnunciar,
  enVuelo,
  deshabilitado = false,
  maxCaracteres = 2000,
  umbralDelContador = 1800,
  umbralMs = UMBRAL_DE_APARICION_MS,
  ref,
}: EntradaDePreguntaProps) {
  const campo = useRef<HTMLTextAreaElement>(null);
  const idDelContador = useId();
  const mostrarContador = valor.length >= umbralDelContador;
  const noEnviaAhora = enVuelo || deshabilitado;
  // Aparece con el indicador y no antes: un botón que se ve un instante en
  // cada respuesta determinista es el mismo parpadeo que el umbral le evita
  // al texto (asistente-rediseno-v3, D14).
  const mostrarDetener = useVisibleTrasUmbral(enVuelo, umbralMs);

  const menciones = useMenciones({ valor, onCambiar, onAnunciar });

  useAltoAutomatico(campo, valor);

  // EL CURSOR VUELVE A DONDE CORRESPONDE TRAS INSERTAR UNA MENCIÓN, recién
  // DESPUÉS del render que trae el texto nuevo: moverlo en el mismo evento
  // que dispara `onCambiar` movería el cursor de un valor que el campo
  // todavía no tiene (`valor` sigue siendo el viejo hasta que el dueño del
  // estado —`PanelAsistente`— vuelva a renderizar con el nuevo).
  useLayoutEffect(() => {
    const posicion = menciones.tomarCursorPendiente();
    if (posicion === null) return;

    const elemento = campo.current;
    if (!elemento) return;
    elemento.focus();
    elemento.setSelectionRange(posicion, posicion);
  }, [valor, menciones]);

  // Un solo ref para dos lectores: el hook del alto y el dueño que devuelve el
  // foco. Con `useCallback` React no lo suelta y lo vuelve a asignar en cada render.
  const asignarCampo = useCallback(
    (elemento: HTMLTextAreaElement | null) => {
      campo.current = elemento;
      if (typeof ref === "function") ref(elemento);
      else if (ref) ref.current = elemento;
    },
    [ref],
  );

  function alCambiarValor(evento: ChangeEvent<HTMLTextAreaElement>) {
    const valorNuevo = evento.target.value;
    const cursor = evento.target.selectionStart ?? valorNuevo.length;
    onCambiar(valorNuevo);
    menciones.alCambiarTexto(valorNuevo, cursor);
  }

  function alPresionarTecla(evento: KeyboardEvent<HTMLTextAreaElement>) {
    // El popover se queda con la tecla primero: Escape/flechas/Enter navegan
    // o eligen mientras está abierto, y sólo si NO la usó sigue el envío de
    // siempre.
    if (menciones.alTeclear(evento)) return;

    if (evento.key !== "Enter" || evento.shiftKey) return;
    // Con puntero grueso Enter es un salto de línea y nada más.
    if (punteroGrueso()) return;

    // Enter no inserta un salto ni en vuelo: viajaría con la pregunta siguiente.
    evento.preventDefault();
    if (noEnviaAhora) return;
    enviar();
  }

  function enviar() {
    const enviadas = menciones.resolverEnvio(valor);
    // Se vacían los chips YA, sea que se haya mandado alguna referencia o
    // ninguna: el borrador siguiente arranca sin las menciones de éste.
    menciones.limpiar();
    onEnviar(enviadas.length > 0 ? enviadas : undefined);
  }

  const idPopoverAbierto = menciones.estado.clase === "abierto" ? menciones.idPopover : undefined;
  const idOpcionActiva =
    menciones.estado.clase === "abierto" && menciones.estado.resultados.length > 0
      ? menciones.idDeOpcion(menciones.activo)
      : undefined;

  return (
    <form
      className="adoc-asistente-entrada"
      onSubmit={(evento) => {
        evento.preventDefault();
        if (!noEnviaAhora) enviar();
      }}
    >
      {menciones.chips.length > 0 && (
        <div className="adoc-asistente-menciones-chips">
          {menciones.chips.map((chip, indice) => (
            <ChipEnElComposer
              key={`${chip.tipo}-${chip.id}-${indice}`}
              chip={chip}
              onQuitar={() => menciones.quitarChip(chip)}
            />
          ))}
        </div>
      )}

      {/* El mismo destello del lanzador: es la misma promesa —«acá le hablás al
          asistente»— y dos símbolos para lo mismo obligan a aprender dos. */}
      <span className="adoc-asistente-destello" aria-hidden="true">
        {sparkIcon}
      </span>

      <Textarea
        ref={asignarCampo}
        rows={1}
        value={valor}
        maxLength={maxCaracteres}
        onChange={alCambiarValor}
        onKeyDown={alPresionarTecla}
        placeholder="Preguntá algo · @ materia · # docente"
        aria-label="Tu pregunta"
        aria-describedby={mostrarContador ? idDelContador : undefined}
        disabled={deshabilitado}
        role="combobox"
        aria-autocomplete="list"
        aria-haspopup="listbox"
        aria-expanded={menciones.estado.clase === "abierto"}
        aria-controls={idPopoverAbierto}
        aria-activedescendant={idOpcionActiva}
      />

      {mostrarDetener ? (
        // «Dejar de esperar» y no «Detener» ni «Cancelar»: suelta el request de
        // este lado y libera el campo, y eso es todo lo que hace. El backend
        // sigue el turno hasta el final y lo cobra; ni el nombre ni ningún
        // tooltip insinúan otra cosa. `type="button"`: no tiene que pasar por
        // el submit del form, que llamaría a `onEnviar`.
        <Button type="button" variant="secondary" leadingIcon={stopIcon} onClick={onDetener}>
          Dejar de esperar
        </Button>
      ) : (
        <Button
          type="submit"
          leadingIcon={sendIcon}
          disabled={noEnviaAhora || valor.trim().length === 0}
        >
          Enviar
        </Button>
      )}

      {mostrarContador && (
        <span id={idDelContador} className="adoc-asistente-contador">
          {conMiles(valor.length)} / {conMiles(maxCaracteres)}
        </span>
      )}

      {menciones.estado.clase === "pista" && (
        <div className="adoc-asistente-menciones-pista">
          <span className="adoc-asistente-menciones-pista-disparador">
            {menciones.estado.disparador}
          </span>
          {menciones.estado.disparador === "@"
            ? "Escribí al menos 2 letras para buscar materias."
            : "Escribí al menos 2 letras para buscar docentes."}
        </div>
      )}

      {menciones.estado.clase === "abierto" && (
        <PopoverDeMenciones
          id={menciones.idPopover}
          tipo={menciones.estado.tipo}
          resultados={menciones.estado.resultados}
          hayMas={menciones.estado.hayMas}
          activo={menciones.activo}
          idDeOpcion={menciones.idDeOpcion}
          onSeleccionar={menciones.elegir}
        />
      )}
    </form>
  );
}

/** Un chip de mención, mientras se sigue escribiendo — con su botón «quitar». */
function ChipEnElComposer({ chip, onQuitar }: { chip: ChipDeMencion; onQuitar: () => void }) {
  return (
    <span className="adoc-asistente-mencion-chip">
      {chip.texto}
      <button
        type="button"
        className="adoc-asistente-mencion-chip-quitar"
        aria-label={`Quitar la mención ${chip.texto}`}
        onClick={onQuitar}
      >
        {closeIcon}
      </button>
    </span>
  );
}

/** Enter hace salto en pantallas táctiles. Guardado: jsdom no trae `matchMedia`. */
function punteroGrueso(): boolean {
  return typeof window.matchMedia === "function" && window.matchMedia("(pointer: coarse)").matches;
}

/** «1 850», con un espacio que no se parte, como lo escribe el design spec. */
function conMiles(numero: number): string {
  return String(numero).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
}
