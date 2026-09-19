import { useCallback, useId, useRef, type KeyboardEvent, type Ref } from "react";
import { Button, Textarea } from "@ars-docendi/ui";

import { sendIcon, sparkIcon } from "../../../app/shell/icons";

import { useAltoAutomatico } from "../hooks/useAltoAutomatico";

interface EntradaDePreguntaProps {
  valor: string;
  onCambiar: (valor: string) => void;
  /** Se llama sin argumentos: el texto ya lo tiene quien sostiene `valor`. */
  onEnviar: () => void;
  enVuelo: boolean;
  /** El del backend: `ModelosAsistente.cs` rechaza mensajes más largos. */
  maxCaracteres?: number;
  /** Desde cuántos caracteres se muestra el contador. */
  umbralDelContador?: number;
  /** Al textarea, para que el dueño le devuelva el foco. */
  ref?: Ref<HTMLTextAreaElement>;
}

/**
 * El composer: destello, campo que crece, contador cerca del límite y «Enviar».
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
 */
export function EntradaDePregunta({
  valor,
  onCambiar,
  onEnviar,
  enVuelo,
  maxCaracteres = 2000,
  umbralDelContador = 1800,
  ref,
}: EntradaDePreguntaProps) {
  const campo = useRef<HTMLTextAreaElement>(null);
  const idDelContador = useId();
  const mostrarContador = valor.length >= umbralDelContador;

  useAltoAutomatico(campo, valor);

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

  function alPresionarTecla(evento: KeyboardEvent<HTMLTextAreaElement>) {
    if (evento.key !== "Enter" || evento.shiftKey) return;
    // Con puntero grueso Enter es un salto de línea y nada más.
    if (punteroGrueso()) return;

    // Enter no inserta un salto ni en vuelo: viajaría con la pregunta siguiente.
    evento.preventDefault();
    if (enVuelo) return;
    onEnviar();
  }

  return (
    <form
      className="adoc-asistente-entrada"
      onSubmit={(evento) => {
        evento.preventDefault();
        if (!enVuelo) onEnviar();
      }}
    >
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
        onChange={(evento) => onCambiar(evento.target.value)}
        onKeyDown={alPresionarTecla}
        placeholder="Escribí tu pregunta…"
        aria-label="Tu pregunta"
        aria-describedby={mostrarContador ? idDelContador : undefined}
      />

      <Button type="submit" leadingIcon={sendIcon} disabled={enVuelo || valor.trim().length === 0}>
        Enviar
      </Button>

      {mostrarContador && (
        <span id={idDelContador} className="adoc-asistente-contador">
          {conMiles(valor.length)} / {conMiles(maxCaracteres)}
        </span>
      )}
    </form>
  );
}

/** Enter hace salto en pantallas táctiles. Guardado: jsdom no trae `matchMedia`. */
function punteroGrueso(): boolean {
  return typeof window.matchMedia === "function" && window.matchMedia("(pointer: coarse)").matches;
}

/** «1 850», con un espacio que no se parte, como lo escribe el design spec. */
function conMiles(numero: number): string {
  return String(numero).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
}
