import { useEffect, useRef, useState } from "react";
import { useLocation } from "react-router-dom";
import { Modal } from "@ars-docendi/ui";

import { PanelAsistente } from "./PanelAsistente";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";
import { useAsistente } from "../hooks/useAsistente";
import { useHistorialAsistente } from "../hooks/useHistorialAsistente";
import { sparkIcon } from "../../../app/shell/icons";
import "../asistente.css";

/**
 * El asistente desde cualquier pantalla.
 *
 * Una ruta a la que hay que navegar NO resuelve el descubrimiento: si el usuario
 * tiene que acordarse de que el asistente existe y buscar dónde está, no lo usa. Por
 * eso hay dos montajes, y éste es el que está siempre a mano.
 *
 * Ocupa el lugar del botón «Ayuda» que estaba `disabled` con `title="Próximamente"`.
 * Activarlo ELIMINA un fake UI existente en vez de agregar superficie nueva, que es
 * lo que el invariante #7 pide.
 *
 * Quien no tiene el permiso no ve nada: ni el botón deshabilitado ni una ruta
 * muerta. El acceso lo decide el backend, no una lista de roles.
 *
 * LLEVA ETIQUETA Y NO SOLO UN ÍCONO. Un ícono solo obliga a descubrir qué hace
 * pasando el mouse por encima, y en la barra ya conviven otros dos; «Preguntar»
 * dice qué pasa al apretarlo sin que haya que averiguarlo.
 *
 * VA EN UN MODAL CENTRADO Y NO EN UN CAJÓN LATERAL. La conversación es la tarea
 * mientras dura: un cajón compite por el ancho con la pantalla que quedó atrás, y a
 * esa pantalla no se la está mirando. El modal también centra el foco del teclado,
 * que es lo que corresponde cuando lo que se abre es donde hay que escribir.
 *
 * NAVEGAR CIERRA EL MODAL. La única forma de navegar con el modal abierto es seguir
 * un vínculo de una respuesta, y quedarse tapando la pantalla a la que se acaba de
 * llegar no tendría sentido. Se resuelve mirando la ubicación desde acá —que es el
 * dueño del estado de apertura— y no pasando un callback por tres componentes hasta
 * la celda de la tabla.
 *
 * LA CONVERSACIÓN VIVE ACÁ, NO EN EL PANEL. El panel se monta al abrir y se
 * desmonta al cerrar, y Esc o un clic afuera —también sin querer— cierran: con el
 * hilo en el panel, un clic fuera lo tiraba. El lanzador vive con la barra, así que
 * al reabrir la conversación sigue donde estaba, y un turno en vuelo al cerrar
 * llega igual y espera. No hay ningún pedido al backend hasta la primera pregunta y
 * nada se guarda en el navegador. La ruta tiene la suya: son dos hilos.
 */
export function LanzadorAsistente() {
  const { tieneAcceso } = useAccesoAlAsistente();
  const [abierto, setAbierto] = useState(false);
  const asistente = useAsistente();
  // Sólo mientras el modal está abierto: cerrado, no hay rail que mostrar y no
  // vale la pena pedir la lista (tasks.md 1.3).
  const historial = useHistorialAsistente(asistente, abierto);
  const lanzador = useRef<HTMLButtonElement>(null);
  const { pathname } = useLocation();
  const [rutaVista, setRutaVista] = useState(pathname);

  // AJUSTE DE ESTADO EN RENDER, no un efecto. Cerrar en un `useEffect` pinta el
  // modal una vez encima de la pantalla nueva y lo saca en el commit siguiente;
  // acá React descarta ese render y vuelve a correr el componente antes de pintar
  // nada. Es el patrón que la documentación de React llama «ajustar estado cuando
  // cambia una prop», y el mismo que usa el grupo colapsable del nav.
  //
  // La conversación no se pierde: vive en este componente, que sigue montado en la
  // barra mientras la aplicación navega por debajo.
  if (rutaVista !== pathname) {
    setRutaVista(pathname);
    setAbierto(false);
  }

  // EL MODAL DE LA LIBRERÍA NO GESTIONA EL FOCO: ni lo contiene ni lo devuelve. Se
  // portalea a `body`, hermano de `#root`, así que hacer inerte la raíz mientras
  // está abierto deja Tab contenido en el diálogo sin implementar un trap a mano.
  // Al cerrar, primero se restaura la raíz y después vuelve el foco al lanzador: un
  // elemento dentro de un subárbol inerte no se puede enfocar, así que el orden
  // importa. Cuando el Modal traiga lo suyo, esto sobra (está en tech-debt).
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
        onOpenChange={setAbierto}
        title="Asistente"
        hideCloseButton
        className="adoc-asistente-modal"
      >
        <PanelAsistente
          asistente={asistente}
          historial={historial}
          onCerrar={() => setAbierto(false)}
        />
      </Modal>
    </>
  );
}
