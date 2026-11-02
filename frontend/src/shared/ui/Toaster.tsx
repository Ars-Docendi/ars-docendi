import { Toast } from "@ars-docendi/ui";

import { cerrarToast, useToasts } from "./toasts";
import "./Toaster.css";

/**
 * App-level toast host, mounted once in the shell. The ui-lib `Toast` is only a card (with its own
 * icon and live-region role per severity): the fixed position, the stacking and the dismissal live here.
 */
export function Toaster() {
  const toasts = useToasts();
  return (
    <div className="adoc-toaster adoc-ui" aria-label="Avisos">
      {toasts.map((toast) => (
        <Toast
          key={toast.id}
          severity={toast.tipo}
          title={toast.titulo}
          closeLabel="Cerrar aviso"
          onClose={() => cerrarToast(toast.id)}
        >
          {toast.descripcion}
        </Toast>
      ))}
    </div>
  );
}
