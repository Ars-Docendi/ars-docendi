import {
  useCallback,
  useEffect,
  useId,
  useRef,
  useState,
  type MouseEvent as EventoMouse,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import { Button } from "@ars-docendi/ui";
import { registrarCapa } from "./capas";
import "./ModalDeFormulario.css";

/** Campos que reciben el foco inicial. El input nativo del calendario se excluye con `data-no-foco`. */
const SELECTOR_CAMPOS = 'input:not([type="hidden"]):not([data-no-foco]), select, textarea';

export interface ModalDeFormularioProps {
  titulo?: ReactNode;
  /** Nombre accesible cuando no hay `titulo` (p. ej. un visor de imagen). */
  etiqueta?: string;
  /** `confirmacion` mide 480 px; `formulario` mide min(600px, 100vw - 32px), con el zoom de la app;
   *  `libre` deja el ancho a la clase del consumidor (modales estirables). */
  tamano?: "confirmacion" | "formulario" | "libre";
  /** Clase extra del cuadro de diálogo. */
  className?: string;
  /** Se monta sólo mientras está abierto: el padre decide cuándo mostrarlo. */
  onCerrar: () => void;
  /** Hay una mutación en curso: Esc, la X y el fondo no cierran. */
  pendiente?: boolean;
  /** Hay cambios sin guardar: cerrar pide confirmar dentro del mismo modal. */
  sucio?: boolean;
  /** Si se pasa, el cuerpo y el pie van dentro de un `<form>` y el botón `submit` lo dispara. */
  alEnviar?: () => void;
  pie?: ReactNode;
  /** Cuando cambia y nada tiene el foco (p. ej. terminó de cargar el primer campo), se reenfoca. */
  claveFoco?: unknown;
  children: ReactNode;
}

function IconoCerrar() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M18 6 6 18" />
      <path d="m6 6 12 12" />
    </svg>
  );
}

/** Foco de vuelta al elemento que abrió el modal; si ya no existe, al h1 de la página. */
function devolverFoco(abridor: Element | null) {
  window.setTimeout(() => {
    if (abridor instanceof HTMLElement && document.contains(abridor) && !isDisabled(abridor)) {
      abridor.focus();
      return;
    }
    const titulo = document.querySelector<HTMLElement>("h1");
    if (titulo) {
      if (!titulo.hasAttribute("tabindex")) titulo.setAttribute("tabindex", "-1");
      titulo.focus();
    }
  }, 0);
}

function isDisabled(elemento: HTMLElement) {
  return (elemento as HTMLButtonElement).disabled === true;
}

/**
 * Carcasa única de los diálogos del módulo Aulas (y de los que se sumen). Nace en la app, no en la
 * ui-lib: subirla a `@ars-docendi/ui` queda como seguimiento. Usa las clases `adoc-modal*` de la
 * librería y las refina con el modificador `--app`, sin tocar los modales de la librería.
 */
export function ModalDeFormulario({
  titulo,
  etiqueta,
  className,
  tamano = "confirmacion",
  onCerrar,
  pendiente = false,
  sucio = false,
  alEnviar,
  pie,
  claveFoco,
  children,
}: ModalDeFormularioProps) {
  const id = useId();
  const [confirmando, setConfirmando] = useState(false);
  const [abridor] = useState(() => document.activeElement);
  const dialogoRef = useRef<HTMLDivElement>(null);
  const cuerpoRef = useRef<HTMLDivElement>(null);
  const ultimoFoco = useRef<Element | null>(null);
  const mouseAbajoEnFondo = useRef(false);

  const enfocarInicial = useCallback(() => {
    const dialogo = dialogoRef.current;
    if (!dialogo) return;
    const campo = cuerpoRef.current?.querySelector<HTMLElement>(SELECTOR_CAMPOS);
    if (campo) {
      if (!isDisabled(campo)) campo.focus();
      else dialogo.focus();
      return;
    }
    // Confirmación sin campos: el botón menos destructivo.
    const botones = Array.from(
      dialogo.querySelectorAll<HTMLButtonElement>("footer button:not(:disabled)"),
    );
    const menosDestructivo =
      botones.find((boton) => /v-(secondary|ghost)/.test(boton.className)) ?? botones[0];
    if (menosDestructivo) menosDestructivo.focus();
    else dialogo.focus();
  }, []);

  function seguirEditando() {
    setConfirmando(false);
    const anterior = ultimoFoco.current;
    window.setTimeout(() => {
      if (anterior instanceof HTMLElement && document.contains(anterior)) anterior.focus();
      else enfocarInicial();
    }, 0);
  }

  // Esc, la X y el fondo terminan acá: nada mientras hay una mutación pendiente.
  function solicitarCierre() {
    if (pendiente) return;
    if (confirmando) {
      seguirEditando();
      return;
    }
    if (sucio) {
      ultimoFoco.current = document.activeElement;
      setConfirmando(true);
      return;
    }
    onCerrar();
  }
  const cierreRef = useRef(solicitarCierre);
  useEffect(() => {
    cierreRef.current = solicitarCierre;
  });

  useEffect(
    () =>
      registrarCapa({
        contenedor: () => dialogoRef.current,
        cerrar: () => cierreRef.current(),
      }),
    [],
  );

  useEffect(() => {
    enfocarInicial();
    return () => devolverFoco(abridor);
  }, [enfocarInicial, abridor]);

  // Un primer campo deshabilitado al abrir (p. ej. materias cargando) toma el foco al habilitarse.
  useEffect(() => {
    if (claveFoco === undefined) return;
    const activo = document.activeElement;
    if (!activo || activo === document.body || activo === dialogoRef.current) enfocarInicial();
  }, [claveFoco, enfocarInicial]);

  useEffect(() => {
    if (!confirmando) return;
    dialogoRef.current?.querySelector<HTMLElement>("#modal-seguir-editando")?.focus();
  }, [confirmando]);

  useEffect(() => {
    const previo = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = previo;
    };
  }, []);

  const pieVisible = confirmando ? (
    <footer className="adoc-modal-sucio">
      <p id={`${id}-sucio`} role="alert" className="adoc-modal-sucio-texto">
        Tenés cambios sin guardar.
      </p>
      <div className="adoc-modal-sucio-acciones">
        <Button
          id="modal-seguir-editando"
          variant="secondary"
          onClick={seguirEditando}
          aria-describedby={`${id}-sucio`}
        >
          Seguir editando
        </Button>
        <Button id="modal-descartar-cambios" variant="destructive" onClick={onCerrar}>
          Descartar cambios
        </Button>
      </div>
    </footer>
  ) : (
    pie && <footer>{pie}</footer>
  );

  const cuerpo = (
    <div className="body" ref={cuerpoRef}>
      {children}
    </div>
  );

  // Cerrar por el fondo exige que el mouse se haya presionado Y soltado sobre el fondo: arrastrar
  // una selección de texto desde el modal hasta afuera no lo cierra.
  function alPresionarFondo(evento: EventoMouse<HTMLDivElement>) {
    mouseAbajoEnFondo.current = evento.target === evento.currentTarget;
  }
  function alSoltarFondo(evento: EventoMouse<HTMLDivElement>) {
    const cierra = mouseAbajoEnFondo.current && evento.target === evento.currentTarget;
    mouseAbajoEnFondo.current = false;
    if (cierra) solicitarCierre();
  }

  return createPortal(
    <div
      className="adoc-modal-stage adoc-modal-stage--app adoc-ui"
      onMouseDown={alPresionarFondo}
      onMouseUp={alSoltarFondo}
    >
      <div
        ref={dialogoRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titulo ? `${id}-titulo` : undefined}
        aria-label={titulo ? undefined : etiqueta}
        tabIndex={-1}
        data-confirmando={confirmando ? "true" : undefined}
        className={`adoc-modal adoc-modal--app adoc-modal--${tamano}${className ? ` ${className}` : ""}`}
      >
        <header>
          {titulo ? (
            <h4 id={`${id}-titulo`}>{titulo}</h4>
          ) : (
            <span className="adoc-modal-sin-titulo" />
          )}
          <button
            type="button"
            className="close"
            aria-label="Cerrar"
            disabled={pendiente}
            onClick={solicitarCierre}
          >
            <IconoCerrar />
          </button>
        </header>
        {alEnviar ? (
          <form
            className="adoc-modal-form"
            noValidate
            onSubmit={(evento) => {
              evento.preventDefault();
              if (!pendiente && !confirmando) alEnviar();
            }}
          >
            {cuerpo}
            {pieVisible}
          </form>
        ) : (
          <>
            {cuerpo}
            {pieVisible}
          </>
        )}
      </div>
    </div>,
    document.body,
  );
}
