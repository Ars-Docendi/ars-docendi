import { useState } from "react";
import { Button, Input } from "@ars-docendi/ui";

import { MenuAcciones } from "../../../shared/ui/MenuAcciones";
import { searchIcon } from "../../../app/shell/icons";
import { agruparPorFecha } from "../utils/agruparPorFecha";
import type { HistorialAsistente } from "../hooks/useHistorialAsistente";
import type { ConversacionResumen } from "../types";

interface ListaDeConversacionesProps {
  historial: HistorialAsistente;
}

/**
 * El cajón de conversaciones propias: buscar, abrir (reanuda), renombrar y
 * borrar una o todas.
 *
 * ES UN CAJÓN QUE SE SUPERPONE AL HILO, no una caja que lo empuja hacia
 * abajo. La versión anterior insertaba esta lista ARRIBA del hilo, dentro
 * del flujo normal del panel: con más de un par de conversaciones el campo
 * de entrada terminaba fuera de la vista sin haber escrito nada. Acá el
 * cajón es `position: absolute` sobre `.adoc-asistente` —que por eso es
 * `position: relative`—, con un fondo que cierra al clic a la izquierda a
 * ancho de escritorio; a ancho angosto ocupa todo (`asistente.css`).
 *
 * AGRUPADA POR FECHA RELATIVA (Hoy / Ayer / Últimos 7 días / Anteriores), UNA
 * LÍNEA POR TÍTULO Y ACCIONES DETRÁS DE UN «⋮»: es el patrón que comparten
 * ChatGPT, Claude.ai, Gemini y Copilot para esta misma lista (research previo
 * a este rediseño). El título completo queda en `title` —se lee al pasar el
 * mouse— y ya no repite la fecha en cada fila: la fecha la da el grupo.
 *
 * SE MONTA IGUAL EN LA RUTA Y EN EL MODAL: recibe el `historial` que le pasa
 * `PanelAsistente`, y ese hook es uno solo por conversación —no hay una copia
 * para cada montaje— (tasks.md 10.3).
 */
export function ListaDeConversaciones({ historial }: ListaDeConversacionesProps) {
  const { conversaciones, busqueda, setBusqueda, conversacionActivaId } = historial;
  const lista = conversaciones.data ?? [];
  const grupos = agruparPorFecha(lista);

  function cerrarYDevolverElFoco() {
    historial.cerrar();
    historial.enfocarDisparador();
  }

  return (
    <div className="adoc-asistente-historial-cajon">
      <div
        className="adoc-asistente-historial"
        aria-label="Tus conversaciones"
        id={historial.idDelPanel}
        tabIndex={-1}
      >
        <div className="adoc-asistente-historial-encabezado">
          <h2 className="adoc-asistente-historial-titulo-panel">Tus conversaciones</h2>
          <button
            type="button"
            className="adoc-asistente-historial-cerrar"
            aria-label="Cerrar el historial"
            onClick={cerrarYDevolverElFoco}
          >
            ×
          </button>
        </div>

        <div className="adoc-asistente-historial-busqueda">
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

        {conversaciones.isLoading && <p>Buscando tus conversaciones…</p>}

        {!conversaciones.isLoading && lista.length === 0 && (
          <p className="adoc-asistente-historial-vacio">
            {busqueda
              ? "Ninguna conversación coincide con esa búsqueda."
              : "Todavía no tenés conversaciones guardadas."}
          </p>
        )}

        <div className="adoc-asistente-historial-grupos">
          {grupos.map((grupo) => (
            <div key={grupo.etiqueta} className="adoc-asistente-historial-grupo">
              <p className="adoc-asistente-historial-grupo-etiqueta">{grupo.etiqueta}</p>
              <ul className="adoc-asistente-historial-lista">
                {grupo.conversaciones.map((conversacion) => (
                  <ItemDeConversacion
                    key={conversacion.id}
                    conversacion={conversacion}
                    activa={conversacion.id === conversacionActivaId}
                    historial={historial}
                  />
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className="adoc-asistente-historial-pie">
          <BorrarTodo historial={historial} deshabilitado={lista.length === 0} />
        </div>
      </div>

      {/* El fondo: a ancho de escritorio el cajón no cubre todo el ancho del
          asistente y queda hilo visible al lado —clicar ahí es la forma obvia
          de cerrar sin ir a buscar la «×»—. A ancho angosto `asistente.css` lo
          oculta, porque ahí el cajón ya ocupa todo. */}
      <button
        type="button"
        className="adoc-asistente-historial-fondo"
        aria-label="Cerrar el historial"
        onClick={cerrarYDevolverElFoco}
      />
    </div>
  );
}

function ItemDeConversacion({
  conversacion,
  activa,
  historial,
}: {
  conversacion: ConversacionResumen;
  activa: boolean;
  historial: HistorialAsistente;
}) {
  const [renombrando, setRenombrando] = useState(false);
  const [titulo, setTitulo] = useState(conversacion.titulo);
  const [confirmandoBorrado, setConfirmandoBorrado] = useState(false);

  // RENOMBRAR Y BORRAR SE LLEVAN CONSIGO EL CONTROL QUE TENÍA EL FOCO: guardar
  // un título cierra el formulario inline, y borrar saca la fila entera de la
  // lista. Sin esto, el navegador manda el foco a `<body>` —perdido, no
  // «donde estaba»—, que es justo lo que asistente-accesibilidad prohíbe
  // (tasks.md 14.2). El destino es el título del cajón, que no se va a
  // ningún lado con ninguna de las dos acciones.
  function elFocoAlTituloDelCajon() {
    document.getElementById(historial.idDelPanel)?.focus();
  }

  async function guardarTitulo() {
    const nuevo = titulo.trim();
    if (nuevo.length === 0) return;
    await historial.renombrar(conversacion.id, nuevo);
    setRenombrando(false);
    elFocoAlTituloDelCajon();
  }

  async function confirmarBorrado() {
    await historial.eliminar(conversacion.id);
    elFocoAlTituloDelCajon();
  }

  if (renombrando) {
    return (
      <li className="adoc-asistente-historial-item">
        <form
          className="adoc-asistente-historial-form"
          onSubmit={(e) => {
            e.preventDefault();
            void guardarTitulo();
          }}
        >
          <Input
            aria-label={`Nuevo título para «${conversacion.titulo}»`}
            value={titulo}
            onChange={(e) => setTitulo(e.target.value)}
            autoFocus
          />
          <Button type="submit" variant="secondary" size="sm">
            Guardar
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => {
              setTitulo(conversacion.titulo);
              setRenombrando(false);
            }}
          >
            Cancelar
          </Button>
        </form>
      </li>
    );
  }

  if (confirmandoBorrado) {
    return (
      <li className="adoc-asistente-historial-item">
        <span className="adoc-asistente-historial-confirmar">
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
    <li
      className={activa ? "adoc-asistente-historial-item activa" : "adoc-asistente-historial-item"}
    >
      <Button
        variant="ghost"
        className="adoc-asistente-historial-abrir"
        title={conversacion.titulo}
        aria-current={activa || undefined}
        onClick={() => void historial.abrirConversacion(conversacion.id)}
      >
        {conversacion.titulo}
      </Button>

      <MenuAcciones
        etiquetaAria={`Acciones de «${conversacion.titulo}»`}
        acciones={[
          { etiqueta: "Renombrar", onSelect: () => setRenombrando(true) },
          { etiqueta: "Borrar", peligro: true, onSelect: () => setConfirmandoBorrado(true) },
        ]}
      />
    </li>
  );
}

function BorrarTodo({
  historial,
  deshabilitado,
}: {
  historial: HistorialAsistente;
  deshabilitado: boolean;
}) {
  const [confirmando, setConfirmando] = useState(false);

  function elFocoAlTituloDelCajon() {
    document.getElementById(historial.idDelPanel)?.focus();
  }

  async function confirmarBorradoDeTodo() {
    await historial.eliminarTodo();
    setConfirmando(false);
    elFocoAlTituloDelCajon();
  }

  if (confirmando) {
    return (
      <div className="adoc-asistente-historial-borrar-todo">
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
    // `destructive` y no `ghost`: al pie, ya demotida de la cabecera, todavía
    // tiene que leerse como lo que es —borra TODO— y no como una acción más
    // de la fila. Es el mismo tono con borde que ya usa la librería para
    // «peligroso pero no en curso», sin ser tan vistosa como el botón sólido
    // de la confirmación.
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
