import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";

import { buscarMenciones } from "../api/mencionesApi";
import type { ChipDeMencion, MencionEnPregunta, ResultadoDeMencion, TipoDeMencion } from "../types";
import {
  detectarToken,
  textoDeLaMencion,
  tipoDelDisparador,
  ubicarMenciones,
  type TokenDeMencion,
} from "../utils/menciones";

/** Igual al debounce de la búsqueda del rail (`useHistorialAsistente`). */
export const ESPERA_DE_BUSQUEDA_DE_MENCIONES_MS = 300;

/** El backend rechaza una sexta referencia (`ConsultaDelAsistente.Referencias`, `[MaxLength(5)]`). */
export const LIMITE_DE_MENCIONES = 5;

/** La pista bajo 2 letras, o el popover ya con búsqueda: nunca los dos a la vez. */
type EstadoDelPopover =
  | { clase: "cerrado" }
  | { clase: "pista"; disparador: "@" | "#" }
  | { clase: "abierto"; tipo: TipoDeMencion; resultados: ResultadoDeMencion[]; hayMas: boolean };

interface OpcionesDeMenciones {
  valor: string;
  onCambiar: (valor: string) => void;
  /**
   * Anuncia por la región viva EXISTENTE del hilo (asistente-accesibilidad:
   * «no agregar una segunda»). Ausente en un test que no la necesita.
   */
  onAnunciar?: (texto: string) => void;
}

export interface Menciones {
  chips: ChipDeMencion[];
  estado: EstadoDelPopover;
  activo: number;
  /** Para `aria-controls`/`id` del listbox. */
  idPopover: string;
  /** Para `aria-activedescendant`: el id de la opción en la posición dada. */
  idDeOpcion: (indice: number) => string;
  /** Se llama en cada cambio del campo, con el cursor YA actualizado. */
  alCambiarTexto: (valorNuevo: string, cursor: number) => void;
  /** Se llama en cada tecla del campo. `true` si la mención se quedó con ella. */
  alTeclear: (evento: KeyboardEvent<HTMLTextAreaElement>) => boolean;
  elegir: (resultado: ResultadoDeMencion) => void;
  quitarChip: (chip: ChipDeMencion) => void;
  /** El cursor a donde volver tras insertar una mención, una vez sola. */
  tomarCursorPendiente: () => number | null;
  /** Los chips vigentes, ya ubicados en `texto` — para mandar y para pintar. */
  resolverEnvio: (texto: string) => MencionEnPregunta[];
  /** Vacía los chips: después de enviar, sean o no las referencias que viajaron. */
  limpiar: () => void;
}

/**
 * El estado y la mecánica del popover de menciones «@materia» / «#docente»
 * del composer (asistente-menciones, design.md D10/D11 de
 * asistente-rediseno-v3).
 *
 * NO TOCA EL DOM DEL CAMPO. Inserta texto y pide el cursor de vuelta a través
 * de `onCambiar`/`tomarCursorPendiente`: quien tiene el `ref` del textarea
 * —`EntradaDePregunta`— es quien puede fijarlo, y quien sabe cuándo el campo
 * cambió por esta vía y no por otra.
 */
export function useMenciones({ valor, onCambiar, onAnunciar }: OpcionesDeMenciones): Menciones {
  const [chips, setChips] = useState<ChipDeMencion[]>([]);
  const [token, setToken] = useState<TokenDeMencion | null>(null);
  // SÓLO lo que trae la búsqueda asíncrona — nunca «cerrado»/«pista», que se
  // derivan de `token` sin estado propio, más abajo: son inmediatos y
  // sincrónicos, así que un efecto no tiene nada que sincronizar ahí (`no
  // llamar a setState sincrónicamente en el cuerpo de un efecto»).
  const [resultado, setResultado] = useState<{
    disparador: "@" | "#";
    termino: string;
    tipo: TipoDeMencion;
    resultados: ResultadoDeMencion[];
    hayMas: boolean;
  } | null>(null);
  const [activo, setActivo] = useState(0);
  const idPopover = useId();

  // El cursor pendiente SÍ es un ref: `EntradaDePregunta` lo consume en un
  // `useLayoutEffect` propio, después del render que trajo el texto nuevo, y
  // no antes — un cambio de estado ahí volvería a disparar este mismo efecto.
  const cursorPendiente = useRef<number | null>(null);

  const disparadorActivo = token?.disparador ?? null;
  const terminoActivo = token?.termino ?? "";

  useEffect(() => {
    if (disparadorActivo === null || terminoActivo.length < 2) {
      // Nada que buscar: el render de abajo ya lee «cerrado»/«pista» del
      // propio `token`, sin necesitar que este efecto sincronice nada.
      return;
    }

    const tipo = tipoDelDisparador(disparadorActivo);
    const controlador = new AbortController();
    const temporizador = window.setTimeout(async () => {
      try {
        const busqueda = await buscarMenciones(tipo, terminoActivo, {
          signal: controlador.signal,
        });
        if (controlador.signal.aborted) return;

        setResultado({
          disparador: disparadorActivo,
          termino: terminoActivo,
          tipo,
          resultados: busqueda.resultados,
          hayMas: busqueda.hayMas,
        });
        setActivo(0);
        onAnunciar?.(textoDelAnuncio(tipo, busqueda.resultados.length, busqueda.hayMas));
      } catch {
        if (controlador.signal.aborted) return;
        // Un fallo de transporte no es «sin coincidencias»: no se afirma nada
        // que no se sabe. Se cierra en silencio, como si no hubiera token.
        setResultado(null);
      }
    }, ESPERA_DE_BUSQUEDA_DE_MENCIONES_MS);

    return () => {
      window.clearTimeout(temporizador);
      controlador.abort();
    };
  }, [disparadorActivo, terminoActivo, onAnunciar]);

  // El popover sólo muestra un resultado que corresponda AL TÉRMINO ACTUAL:
  // mientras el debounce de arriba sigue corriendo para lo que se acaba de
  // teclear, esto es «cerrado» y no el resultado —quizás vacío, quizás de
  // otras materias— de la búsqueda anterior, que parpadearía como si ya
  // fuera la respuesta a lo nuevo.
  const estado: EstadoDelPopover =
    disparadorActivo === null
      ? { clase: "cerrado" }
      : terminoActivo.length < 2
        ? { clase: "pista", disparador: disparadorActivo }
        : resultado &&
            resultado.disparador === disparadorActivo &&
            resultado.termino === terminoActivo
          ? {
              clase: "abierto",
              tipo: resultado.tipo,
              resultados: resultado.resultados,
              hayMas: resultado.hayMas,
            }
          : { clase: "cerrado" };

  function alCambiarTexto(valorNuevo: string, cursor: number) {
    setToken(detectarToken(valorNuevo, cursor));
  }

  function idDeOpcion(indice: number): string {
    return `${idPopover}-opcion-${indice}`;
  }

  function cerrar() {
    setToken(null);
  }

  function elegir(resultado: ResultadoDeMencion) {
    if (!token || chips.length >= LIMITE_DE_MENCIONES) {
      cerrar();
      return;
    }

    const tipo = tipoDelDisparador(token.disparador);
    const texto = textoDeLaMencion(token.disparador, resultado.nombre);
    const nuevoValor = `${valor.slice(0, token.inicio)}${texto} ${valor.slice(token.fin)}`;

    cursorPendiente.current = token.inicio + texto.length + 1;
    setChips((previos) => [...previos, { tipo, id: resultado.id, texto }]);
    cerrar();
    onCambiar(nuevoValor);
  }

  function quitarChip(chip: ChipDeMencion) {
    setChips((previos) => previos.filter((c) => c !== chip));

    const inicio = valor.indexOf(chip.texto);
    if (inicio === -1) return;

    // Se lleva puesto un espacio contiguo —el que se agregó al insertarla—
    // para no dejar dos espacios donde había uno.
    let fin = inicio + chip.texto.length;
    if (valor[fin] === " ") fin += 1;

    onCambiar(valor.slice(0, inicio) + valor.slice(fin));
  }

  function alTeclear(evento: KeyboardEvent<HTMLTextAreaElement>): boolean {
    if (estado.clase === "pista" && evento.key === "Escape") {
      evento.preventDefault();
      evento.stopPropagation();
      cerrar();
      return true;
    }

    if (estado.clase !== "abierto") return false;

    if (evento.key === "Escape") {
      // Cierra SÓLO el popover: ni el modal (asistente-accesibilidad, orden de
      // Esc) ni el texto ya tecleado.
      evento.preventDefault();
      evento.stopPropagation();
      cerrar();
      return true;
    }

    if (evento.key === "ArrowDown") {
      evento.preventDefault();
      setActivo((a) => Math.min(a + 1, Math.max(estado.resultados.length - 1, 0)));
      return true;
    }

    if (evento.key === "ArrowUp") {
      evento.preventDefault();
      setActivo((a) => Math.max(a - 1, 0));
      return true;
    }

    if (evento.key === "Enter" && estado.resultados.length > 0) {
      evento.preventDefault();
      elegir(estado.resultados[activo]);
      return true;
    }

    return false;
  }

  function tomarCursorPendiente(): number | null {
    const posicion = cursorPendiente.current;
    cursorPendiente.current = null;
    return posicion;
  }

  function resolverEnvio(texto: string): MencionEnPregunta[] {
    return ubicarMenciones(chips, texto);
  }

  function limpiar() {
    setChips([]);
    cerrar();
  }

  return {
    chips,
    estado,
    activo,
    idPopover,
    idDeOpcion,
    alCambiarTexto,
    alTeclear,
    elegir,
    quitarChip,
    tomarCursorPendiente,
    resolverEnvio,
    limpiar,
  };
}

/** «6 materias encontradas. Hay más…» — lo que se anuncia por la región viva existente. */
function textoDelAnuncio(tipo: TipoDeMencion, cantidad: number, hayMas: boolean): string {
  if (cantidad === 0) {
    return tipo === "materia"
      ? "Sin materias que coincidan en las carreras a las que tenés acceso."
      : "Sin docentes que coincidan en las carreras a las que tenés acceso.";
  }

  const sustantivo =
    tipo === "materia"
      ? cantidad === 1
        ? "materia"
        : "materias"
      : cantidad === 1
        ? "docente"
        : "docentes";
  const participio = tipo === "materia" ? "encontrada" : "encontrado";
  const verbo = cantidad === 1 ? participio : `${participio}s`;
  const base = `${cantidad} ${sustantivo} ${verbo}.`;

  return hayMas ? `${base} Hay más coincidencias. Seguí escribiendo para acotar.` : base;
}
