import { useEffect, useRef, useState } from "react";
import { useLocation, useSearchParams } from "react-router-dom";
import { Modal } from "@ars-docendi/ui";

import { PanelAsistente } from "./PanelAsistente";
import { VolverALaConversacion } from "./VolverALaConversacion";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";
import { useAsistente } from "../hooks/useAsistente";
import { useHistorialAsistente } from "../hooks/useHistorialAsistente";
import { sparkIcon } from "../../../app/shell/icons";
import "../asistente.css";

/**
 * El asistente desde cualquier pantalla. Una ruta a la que hay que navegar NO resuelve el
 * descubrimiento: si el usuario tiene que acordarse de que el asistente existe y buscar dónde
 * está, no lo usa.
 */
export function LanzadorAsistente() {
  const { tieneAcceso, capacidades } = useAccesoAlAsistente();
  const [abierto, setAbierto] = useState(false);
  const asistente = useAsistente({ redaccionEnFlujo: capacidades?.redaccionEnFlujo === true });
  // Sólo mientras el modal está abierto: cerrado, no hay rail que mostrar y no
  // vale la pena pedir la lista (tasks.md 1.3).
  const historial = useHistorialAsistente(asistente, abierto);
  const lanzador = useRef<HTMLButtonElement>(null);
  const { pathname } = useLocation();
  const [rutaVista, setRutaVista] = useState(pathname);
  // Hubo un clic en un vínculo de una respuesta con el modal abierto, y la ruta
  // todavía no cambió. Es estado y no una ref porque se lee al renderizar.
  const [siguioVinculo, setSiguioVinculo] = useState(false);
  const [volverVisible, setVolverVisible] = useState(false);

  // AJUSTE DE ESTADO EN RENDER, no un efecto. Cerrar en un `useEffect` pinta el modal una vez
  // encima de la pantalla nueva y lo saca en el commit siguiente; acá React descarta ese render y
  // vuelve a correr el componente antes de pintar nada.
  if (rutaVista !== pathname) {
    setRutaVista(pathname);
    setAbierto(false);
    setSiguioVinculo(false);
    // Cada cambio de ruta lo recalcula: navegar de nuevo, sin vínculo, lo quita.
    setVolverVisible(siguioVinculo);
  }

  // Reabrir el asistente, por el medio que sea, lo quita.
  if (abierto && volverVisible) setVolverVisible(false);

  // EL MODAL DE LA LIBRERÍA NO GESTIONA EL FOCO: ni lo contiene ni lo devuelve.
  useEffect(() => {
    if (!abierto) return;

    const raiz = document.getElementById("root");
    const boton = lanzador.current;
    raiz?.setAttribute("inert", "");

    return () => {
      raiz?.removeAttribute("inert");
      boton?.focus();
    };
  }, [abierto]);

  function alCambiarApertura(valor: boolean) {
    if (!valor) setSiguioVinculo(false);
    setAbierto(valor);
  }

  const [parametros, fijarParametros] = useSearchParams();
  const marcaPedida = parametros.get("asistente") === "abrir";

  // NO SE PUEDE CAPTURAR LA MARCA UNA SOLA VEZ AL MONTAR (inicializador de `useState` o ref): en la
  // app real este componente vive en la barra, fuera de la ruta, así que YA ESTÁ MONTADO cuando
  // `/asistente` todavía no redirigió —el primer render ve la URL sin la marca todavía—.
  const [marcaVista, setMarcaVista] = useState(marcaPedida);
  const [pedidoEnEspera, setPedidoEnEspera] = useState(marcaPedida);

  // AJUSTE DE ESTADO EN RENDER: registra la aparición de la marca como un pedido pendiente.
  if (marcaVista !== marcaPedida) {
    setMarcaVista(marcaPedida);
    if (marcaPedida) setPedidoEnEspera(true);
  }

  // AJUSTE DE ESTADO EN RENDER: consume el pedido pendiente en cuanto `tieneAcceso` deja de ser `undefined`.
  if (pedidoEnEspera && tieneAcceso !== undefined) {
    setPedidoEnEspera(false);
    if (tieneAcceso) setAbierto(true);
  }

  // BORRAR LA MARCA DE LA URL SÍ ES UN EFECTO: es una escritura sobre el router, un sistema externo
  // al componente.
  useEffect(() => {
    if (!marcaPedida || tieneAcceso === undefined) return;

    fijarParametros(
      (previos) => {
        const siguientes = new URLSearchParams(previos);
        siguientes.delete("asistente");
        return siguientes;
      },
      { replace: true },
    );
  }, [marcaPedida, tieneAcceso, fijarParametros]);

  if (tieneAcceso !== true) return null;

  return (
    <>
      <button
        ref={lanzador}
        type="button"
        className="adoc-asistente-lanzador"
        onClick={() => setAbierto(true)}
      >
        <span className="ico">{sparkIcon}</span>
        Preguntar
      </button>

      {/* EL TÍTULO SIGUE SIENDO «Asistente», Y EL DIÁLOGO SIGUE NOMBRÁNDOSE POR ÉL:
          la librería lo pone en un `h4` referenciado por `aria-labelledby` del
          diálogo. Lo nuevo en v3 es que ese `h4` queda visualmente oculto
          (`asistente.css`, mismo recorte que ya usa `.adoc-asistente-quien`) y
          `hideCloseButton` apaga la «×» de la librería: `PanelAsistente` pinta su
          propio encabezado de 56 px —título de la conversación activa, ayuda y
          cierre— DENTRO del cuerpo, porque la librería sigue sin un slot de
          acciones en su fila de encabezado (TD-020) y superponer controles ahí,
          como hacía v2, ya no alcanza para el rail + encabezado de v3. */}
      <Modal
        open={abierto}
        onOpenChange={alCambiarApertura}
        title="Asistente"
        hideCloseButton
        className="adoc-asistente-modal"
      >
        {/* `display: contents`: sólo escucha, no participa del layout. El
            atributo lo llevan el chip, la celda con vínculo y el botón de la
            ficha; no hace falta pasar nada por props. */}
        <div
          style={{ display: "contents" }}
          onClickCapture={(evento) => {
            if (
              evento.target instanceof Element &&
              evento.target.closest("a[data-vinculo-del-asistente]")
            ) {
              setSiguioVinculo(true);
            }
          }}
        >
          <PanelAsistente
            asistente={asistente}
            historial={historial}
            onCerrar={() => alCambiarApertura(false)}
          />
        </div>
      </Modal>

      {volverVisible && !abierto && <VolverALaConversacion onVolver={() => setAbierto(true)} />}
    </>
  );
}
