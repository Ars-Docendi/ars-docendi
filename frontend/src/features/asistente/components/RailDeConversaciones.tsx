import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import { Button, Input } from "@ars-docendi/ui";

import { MenuAcciones } from "../../../shared/ui/MenuAcciones";
import {
  archiveIcon,
  archiveRestoreIcon,
  chevronIcon,
  historyIcon,
  pencilIcon,
  railColapsarIcon,
  railExpandirIcon,
  searchIcon,
  trashIcon,
} from "../../../app/shell/icons";
import { AvisoDeDeshacer } from "./AvisoDeDeshacer";
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
 * El rail de conversaciones propias: siempre a la vista, a la izquierda de la conversación
 * (asistente-superficie-frontend, design.md D1 de asistente-rediseno-v3).
 */
export function RailDeConversaciones({
  asistente,
  historial,
  colapsado,
  onAlternar,
}: RailDeConversacionesProps) {
  const { conversaciones, busqueda, setBusqueda, conversacionActivaId, aviso } = historial;
  const lista = conversaciones.data ?? [];
  const conBusqueda = busqueda.trim().length > 0;
  const activas = conBusqueda ? lista : lista.filter((conversacion) => !conversacion.archivada);
  const archivadas = conBusqueda ? [] : lista.filter((conversacion) => conversacion.archivada);
  const grupos = agruparPorFecha(activas);

  // FOCO AL CONTROL VISIBLE EN EL NUEVO ESTADO, NUNCA AL DOCUMENTO (asistente-accesibilidad,
  // tasks.md 1.6): colapsar desmonta «Colapsar conversaciones» y monta «Expandir conversaciones»
  // —son dos botones distintos, uno por estado—, y el que se fue no puede quedarse con el foco.
  const toggleRef = useRef<HTMLButtonElement>(null);
  const esPrimerRender = useRef(true);
  useEffect(() => {
    if (esPrimerRender.current) {
      esPrimerRender.current = false;
      return;
    }
    toggleRef.current?.focus();
  }, [colapsado]);

  // RENOMBRAR, ARCHIVAR Y ELIMINAR SE LLEVAN CONSIGO EL CONTROL QUE TENÍA EL FOCO: guardar un
  // título cierra el campo inline, y las otras dos sacan la fila entera de la lista.
  const listaRef = useRef<HTMLDivElement>(null);
  const enfocarLista = () => listaRef.current?.focus();

  return (
    // FRAGMENTO Y NO UN SOLO `<nav>`: el aviso de deshacer tiene que seguir visible con el rail
    // colapsado, superpuesto al hilo (design spec § v3), y el `<nav>` recorta su contenido con
    // `overflow: hidden` durante la transición de ancho.
    <>
      <nav
        className={colapsado ? "adoc-asistente-rail colapsado" : "adoc-asistente-rail"}
        aria-label="Conversaciones"
      >
        <div className="adoc-asistente-rail-encabezado">
          {colapsado ? (
            <>
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

              <NuevaConversacion asistente={asistente} />

              <button
                type="button"
                className="adoc-asistente-rail-historial-icono"
                aria-label="Historial"
                onClick={onAlternar}
              >
                <span className="ico">{historyIcon}</span>
              </button>
            </>
          ) : (
            <>
              {/* Ancho completo, con el toggle a su derecha — design spec § v3. */}
              <NuevaConversacion asistente={asistente} />

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
            </>
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

              {conBusqueda
                ? activas.length > 0 && (
                    <ul className="adoc-asistente-rail-grupo-lista">
                      {activas.map((conversacion) => (
                        <ItemDeConversacion
                          key={conversacion.id}
                          conversacion={conversacion}
                          activa={conversacion.id === conversacionActivaId}
                          historial={historial}
                          enfocarLista={enfocarLista}
                          mostrarMarcaArchivada
                        />
                      ))}
                    </ul>
                  )
                : grupos.map((grupo) => (
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

              {archivadas.length > 0 && (
                <SeccionDeArchivadas
                  archivadas={archivadas}
                  conversacionActivaId={conversacionActivaId}
                  historial={historial}
                  enfocarLista={enfocarLista}
                />
              )}

              <div className="adoc-asistente-rail-pie">
                <BorrarTodo historial={historial} deshabilitado={lista.length === 0} />
              </div>
            </div>
          </>
        )}
      </nav>

      {aviso && <AvisoDeDeshacer aviso={aviso} enfocarLista={enfocarLista} />}
    </>
  );
}

function SeccionDeArchivadas({
  archivadas,
  conversacionActivaId,
  historial,
  enfocarLista,
}: {
  archivadas: ConversacionResumen[];
  conversacionActivaId: string | null;
  historial: HistorialAsistente;
  enfocarLista: () => void;
}) {
  // COLAPSADA POR DEFAULT, al revés que los grupos colapsables del nav (design spec § v3): archivar
  // es justamente sacar algo del primer plano, así que la sección arranca cerrada.
  const [abierta, setAbierta] = useState(false);

  return (
    <div className="adoc-asistente-rail-archivadas">
      <button
        type="button"
        className="adoc-asistente-rail-archivadas-toggle"
        aria-expanded={abierta}
        onClick={() => setAbierta((a) => !a)}
      >
        <span className={abierta ? "chev" : "chev collapsed"} aria-hidden="true">
          {chevronIcon}
        </span>
        <span className="adoc-asistente-rail-archivadas-etiqueta">Archivadas</span>
        <span className="adoc-asistente-rail-archivadas-contador">{archivadas.length}</span>
      </button>

      {abierta && (
        <ul className="adoc-asistente-rail-grupo-lista">
          {archivadas.map((conversacion) => (
            <ItemDeConversacion
              key={conversacion.id}
              conversacion={conversacion}
              activa={conversacion.id === conversacionActivaId}
              historial={historial}
              enfocarLista={enfocarLista}
            />
          ))}
        </ul>
      )}
    </div>
  );
}

function ItemDeConversacion({
  conversacion,
  activa,
  historial,
  enfocarLista,
  mostrarMarcaArchivada = false,
}: {
  conversacion: ConversacionResumen;
  activa: boolean;
  historial: HistorialAsistente;
  /** A dónde va el foco cuando renombrar, archivar o eliminar se llevan el control que lo tenía. */
  enfocarLista: () => void;
  /**
   * La marca «Archivada» junto al título. SÓLO en los resultados de búsqueda —donde activas y
   * archivadas se mezclan en una sola lista y hace falta distinguirlas—: dentro de su propia
   * sección «Archivadas» la marca es redundante (asistente-rediseno-v3, hallazgo de verificación
   * visual; el mock no la dibuja ahí).
   */
  mostrarMarcaArchivada?: boolean;
}) {
  const [renombrando, setRenombrando] = useState(false);
  const [titulo, setTitulo] = useState(conversacion.titulo);
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

  // SIN CONFIRMACIÓN (asistente-superficie-frontend, design.md D4 de asistente-rediseno-v3): la
  // fila desaparece de inmediato y el aviso de deshacer —con foco en «Deshacer»— es la red de seguridad.
  async function eliminar() {
    await historial.eliminar(conversacion.id);
    // NO se llama `enfocarLista()` acá: el aviso de deshacer que aparece a continuación se lleva el
    // foco a su propio «Deshacer» (asistente-accesibilidad) — enfocar la lista primero lo movería
    // ahí sólo para que el aviso lo vuelva a mover un instante después.
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

  const acciones = conversacion.archivada
    ? [
        {
          etiqueta: "Desarchivar",
          icono: archiveRestoreIcon,
          onSelect: () => void historial.desarchivar(conversacion.id),
        },
        { etiqueta: "Eliminar", icono: trashIcon, peligro: true, onSelect: () => void eliminar() },
      ]
    : [
        { etiqueta: "Renombrar", icono: pencilIcon, onSelect: empezarRenombre },
        {
          etiqueta: "Archivar",
          icono: archiveIcon,
          onSelect: () => void historial.archivar(conversacion.id),
        },
        { etiqueta: "Eliminar", icono: trashIcon, peligro: true, onSelect: () => void eliminar() },
      ];

  return (
    <li className={activa ? "adoc-asistente-rail-item activa" : "adoc-asistente-rail-item"}>
      <Button
        variant="ghost"
        className="adoc-asistente-rail-abrir"
        title={conversacion.titulo}
        aria-current={activa || undefined}
        onClick={() => void historial.abrirConversacion(conversacion.id)}
        onDoubleClick={conversacion.archivada ? undefined : empezarRenombre}
      >
        {conversacion.titulo}
      </Button>

      {mostrarMarcaArchivada && conversacion.archivada && (
        <span className="adoc-asistente-rail-marca-archivada">Archivada</span>
      )}

      <MenuAcciones etiquetaAria={`Acciones de «${conversacion.titulo}»`} acciones={acciones} />
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

  async function confirmarBorradoDeTodo() {
    await historial.eliminarTodo();
    setConfirmando(false);
    // NO se llama `enfocarLista()` acá, mismo motivo que en `eliminar()` de `ItemDeConversacion`:
    // el aviso de deshacer se lleva el foco a «Deshacer» apenas aparece.
  }

  if (confirmando) {
    return (
      <div className="adoc-asistente-rail-borrar-todo">
        <span>
          ¿Borrar TODAS tus conversaciones, incluidas las archivadas? Vas a poder deshacerlo durante
          10 segundos.
        </span>
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
