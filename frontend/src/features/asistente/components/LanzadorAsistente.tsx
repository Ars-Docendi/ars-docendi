import { useState } from "react";
import { Drawer } from "@ars-docendi/ui";

import { PanelAsistente } from "./PanelAsistente";
import { useAccesoAlAsistente } from "../hooks/useAccesoAlAsistente";
import { helpIcon } from "../../../app/shell/icons";

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
 */
export function LanzadorAsistente() {
  const { tieneAcceso } = useAccesoAlAsistente();
  const [abierto, setAbierto] = useState(false);

  if (tieneAcceso !== true) return null;

  return (
    <>
      <button
        type="button"
        className="adoc-icon-btn"
        aria-label="Abrir el asistente"
        title="Asistente"
        onClick={() => setAbierto(true)}
      >
        <span className="ico">{helpIcon}</span>
      </button>

      <Drawer
        open={abierto}
        onOpenChange={setAbierto}
        title="Asistente"
        aria-label="Asistente conversacional"
      >
        {/* Se monta recién al abrir: el panel arranca una conversación, y montarlo
            siempre dejaría un hilo abierto en cada pantalla de la aplicación. */}
        {abierto && <PanelAsistente />}
      </Drawer>
    </>
  );
}
