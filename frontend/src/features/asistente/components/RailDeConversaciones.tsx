import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import { Button, Input } from "@ars-docendi/ui";

import { MenuAcciones } from "../../../shared/ui/MenuAcciones";
import {
  historyIcon,
  railColapsarIcon,
  railExpandirIcon,
  searchIcon,
} from "../../../app/shell/icons";
import { agruparPorFecha } from "../utils/agruparPorFecha";
import { NuevaConversacion } from "./NuevaConversacion";
import type { Asistente } from "../hooks/useAsistente";
import type { HistorialAsistente } from "../hooks/useHistorialAsistente";
import type { ConversacionResumen } from "../types";

interface RailDeConversacionesProps {
  asistente: Asistente;
  historial: HistorialAsistente;
  colapsado: boolean;
  onAlternar: () => void;
}

/**
 * El rail de conversaciones propias: siempre a la vista, a la izquierda de
 * la conversación (asistente-superficie-frontend, design.md D1 de
 * asistente-rediseno-v3).
 *
 * REEMPLAZA AL CAJÓN QUE SE ABRÍA Y CERRABA (`ListaDeConversaciones` +
 * `AbrirHistorial`, ahora borrados). La lista ya no tiene un estado propio
 * de «abierto»: lo único que colapsa es el ANCHO del rail —268 px
 * expandido, 60 px colapsado—, y el contenido (buscar, agrupar, renombrar,
 * borrar) es el mismo tanto si el rail lo mostró siempre como si lo acaba
 * de destapar.
 *
 * COLAPSADO MUESTRA TRES CONTROLES ÍCONO-SOLO: expandir, «Nueva
 * conversación» y «Historial» —éste último también expande, es la forma
 * reconocible de decir «tus conversaciones viven acá» aun angosto—.
 * `NuevaConversacion` es EL MISMO componente en los dos anchos: la hoja de
 * estilos le esconde la etiqueta visualmente cuando el rail está colapsado,
 * en vez de duplicar el botón.
 *
 * §1 (ARS-142) sólo trae Renombrar y Eliminar al «⋮» —las acciones que ya
 * existen en el backend hoy—: Archivar y el aviso de deshacer llegan con
 * §2/§3 (ARS-143/144), y agregarlos acá sería la UI muerta que el
 * invariante #7 prohíbe.
 */
export function RailDeConversaciones({
  asistente,
  historial,
  colapsado,
  onAlternar,
}: RailDeConversacionesProps) {
  const { conversaciones, busqueda, setBusqueda, conversacionActivaId } = historial;
  const lista = conversaciones.data ?? [];
  const grupos = agruparPorFecha(lista);

  // FOCO AL CONTROL VISIBLE EN EL NUEVO ESTADO, NUNCA AL DOCUMENTO
  // (asistente-accesibilidad, tasks.md 1.6): colapsar desmonta «Colapsar
  // conversaciones» y monta «Expandir conversaciones» —son dos botones
  // distintos, uno por estado—, y el que se fue no puede quedarse con el
  // foco. La MISMA ref se pasa a los dos: sólo uno está montado a la vez,
  // así que apunta siempre al que hay que enfocar.
  const toggleRef = useRef<HTMLButtonElement>(null);
  const esPrimerRender = useRef(true);
  useEffect(() => {
    if (esPrimerRender.current) {
      esPrimerRender.current = false;
      return;
    }
    toggleRef.current?.focus();
  }, [colapsado]);

  // RENOMBRAR Y ELIMINAR SE LLEVAN CONSIGO EL CONTROL QUE TENÍA EL FOCO:
  // guardar un título cierra el campo inline, y borrar saca la fila entera de
  // la lista. Sin esto, el navegador manda el foco a `<body>` —perdido, no
  // «donde estaba»—, que es justo lo que asistente-accesibilidad prohíbe. El
  // destino es la lista misma, que no se va a ningún lado con ninguna de las
  // dos acciones.
  const listaRef = useRef<HTMLDivElement>(null);
  const enfocarLista = () => listaRef.current?.focus();

  return (
    <nav
      className={colapsado ? "adoc-asistente-rail colapsado" : "adoc-asistente-rail"}
      aria-label="Conversaciones"
    >
      <div className="adoc-asistente-rail-encabezado">
        {colapsado ? (
          <button
            ref={toggleRef}
            type="button"
            className="adoc-asistente-rail-toggle"
            aria-label="Expandir conversaciones"
            aria-expanded={false}
            onClick={onAlternar}
          >
            <span className="ico">{railExpandirIcon}</span>
          </button>
        ) : (
          <button
            ref={toggleRef}
            type="button"
            className="adoc-asistente-rail-toggle"
            aria-label="Colapsar conversaciones"
            aria-expanded={true}
            onClick={onAlternar}
          >
            <span className="ico">{railColapsarIcon}</span>
          </button>
        )}

        <NuevaConversacion asistente={asistente} />

        {colapsado && (
          <button
            type="button"
            className="adoc-asistente-rail-historial-icono"
            aria-label="Historial"
            onClick={onAlternar}
          >
            <span className="ico">{historyIcon}</span>
          </button>
        )}
      </div>

      {!colapsado && (
        <>
          <div className="adoc-asistente-rail-busqueda">
            <span className="ico" aria-hidden="true">
              {searchIcon}
            </span>
            <Input
              type="search"
              value={busqueda}
              onChange={(e) => setBusqueda(e.target.value)}
              placeholder="Buscar en tus conversaciones…"
              aria-label="Buscar en tus conversaciones"
            />
          </div>

          <div
            ref={listaRef}
            className="adoc-asistente-rail-lista"
            aria-label="Tus conversaciones"
            tabIndex={-1}
          >
            {conversaciones.isLoading && <p>Buscando tus conversaciones…</p>}

            {!conversaciones.isLoading && lista.length === 0 && (
              <p className="adoc-asistente-rail-vacio">
                {busqueda
                  ? "Ninguna conversación coincide con esa búsqueda."
                  : "Todavía no tenés conversaciones guardadas."}
              </p>
            )}

            {grupos.map((grupo) => (
              <div key={grupo.etiqueta} className="adoc-asistente-rail-grupo">
                <p className="adoc-asistente-rail-grupo-etiqueta">{grupo.etiqueta}</p>
                <ul className="adoc-asistente-rail-grupo-lista">
                  {grupo.conversaciones.map((conversacion) => (
                    <ItemDeConversacion
                      key={conversacion.id}
                      conversacion={conversacion}
                      activa={conversacion.id === conversacionActivaId}
                      historial={historial}
                      enfocarLista={enfocarLista}
                    />
                  ))}
                </ul>
              </div>
            ))}

            <div className="adoc-asistente-rail-pie">
              <BorrarTodo
                historial={historial}
                deshabilitado={lista.length === 0}
                enfocarLista={enfocarLista}
              />
            </div>
          </div>
        </>
      )}
    </nav>
  );
}

function ItemDeConversacion({
  conversacion,
  activa,
  historial,
  enfocarLista,
}: {
  conversacion: ConversacionResumen;
  activa: boolean;
  historial: HistorialAsistente;
  /** A dónde va el foco cuando renombrar o eliminar se llevan el control que lo tenía. */
  enfocarLista: () => void;
}) {
  const [renombrando, setRenombrando] = useState(false);
  const [titulo, setTitulo] = useState(conversacion.titulo);
  const [confirmandoBorrado, setConfirmandoBorrado] = useState(false);
  // Escape cancela sin guardar; sin esta marca, quitarle el foco al campo al
  // desmontarlo (React ya sacó el `renombrando` del estado) dispara TAMBIÉN
  // el `onBlur` que guarda, y el título cancelado se guardaría igual.
  const canceladoPorEscape = useRef(false);

  function empezarRenombre() {
    setTitulo(conversacion.titulo);
    setRenombrando(true);
  }

  async function guardarTitulo() {
    const nuevo = titulo.trim();
    setRenombrando(false);
    enfocarLista();
    // Vacío conserva el título anterior (asistente-superficie-frontend): no
    // hay pedido que mandar ni anuncio que dar.
    if (nuevo.length === 0 || nuevo === conversacion.titulo) return;
    await historial.renombrar(conversacion.id, nuevo);
  }

  function cancelarRenombre() {
    canceladoPorEscape.current = true;
    setTitulo(conversacion.titulo);
    setRenombrando(false);
    enfocarLista();
  }

  function alPerderElFoco() {
    if (canceladoPorEscape.current) {
      canceladoPorEscape.current = false;
      return;
    }
    void guardarTitulo();
  }

  function alTeclearEnElCampo(evento: KeyboardEvent<HTMLInputElement>) {
    if (evento.key === "Enter") {
      evento.preventDefault();
      void guardarTitulo();
    } else if (evento.key === "Escape") {
      evento.preventDefault();
      cancelarRenombre();
    }
  }

  async function confirmarBorrado() {
    await historial.eliminar(conversacion.id);
    enfocarLista();
  }

  if (renombrando) {
    return (
      <li className="adoc-asistente-rail-item">
        <Input
          aria-label={`Nuevo título para «${conversacion.titulo}»`}
          className="adoc-asistente-rail-item-campo"
          value={titulo}
          onChange={(e) => setTitulo(e.target.value)}
          onKeyDown={alTeclearEnElCampo}
          onBlur={alPerderElFoco}
          autoFocus
        />
      </li>
    );
  }

  if (confirmandoBorrado) {
    return (
      <li className="adoc-asistente-rail-item">
        <span className="adoc-asistente-rail-confirmar">
          <span>¿Borrar «{conversacion.titulo}»?</span>
          <Button variant="secondary" size="sm" onClick={() => void confirmarBorrado()}>
            Confirmar borrado
          </Button>
          <Button variant="ghost" size="sm" onClick={() => setConfirmandoBorrado(false)}>
            Cancelar
          </Button>
        </span>
      </li>
    );
  }

  return (
    <li className={activa ? "adoc-asistente-rail-item activa" : "adoc-asistente-rail-item"}>
      <Button
        variant="ghost"
        className="adoc-asistente-rail-abrir"
        title={conversacion.titulo}
        aria-current={activa || undefined}
        onClick={() => void historial.abrirConversacion(conversacion.id)}
        onDoubleClick={empezarRenombre}
      >
        {conversacion.titulo}
      </Button>

      <MenuAcciones
        etiquetaAria={`Acciones de «${conversacion.titulo}»`}
        acciones={[
          { etiqueta: "Renombrar", onSelect: empezarRenombre },
          { etiqueta: "Eliminar", peligro: true, onSelect: () => setConfirmandoBorrado(true) },
        ]}
      />
    </li>
  );
}

function BorrarTodo({
  historial,
  deshabilitado,
  enfocarLista,
}: {
  historial: HistorialAsistente;
  deshabilitado: boolean;
  enfocarLista: () => void;
}) {
  const [confirmando, setConfirmando] = useState(false);

  async function confirmarBorradoDeTodo() {
    await historial.eliminarTodo();
    setConfirmando(false);
    enfocarLista();
  }

  if (confirmando) {
    return (
      <div className="adoc-asistente-rail-borrar-todo">
        <span>¿Borrar TODAS tus conversaciones? No se puede deshacer.</span>
        <Button variant="secondary" size="sm" onClick={() => void confirmarBorradoDeTodo()}>
          Confirmar borrado de todo
        </Button>
        <Button variant="ghost" size="sm" onClick={() => setConfirmando(false)}>
          Cancelar
        </Button>
      </div>
    );
  }

  return (
    <Button
      variant="destructive"
      size="sm"
      disabled={deshabilitado}
      onClick={() => setConfirmando(true)}
    >
      Borrar todas
    </Button>
  );
}
