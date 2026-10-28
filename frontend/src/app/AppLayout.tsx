import { useState } from "react";
import { Outlet, useLocation } from "react-router-dom";

import { useCurrentUser } from "../shared/auth/useCurrentUser";
import { Sidebar } from "./shell/Sidebar";
import { TopBar } from "./shell/TopBar";
import "./shell/shell.css";

/** Preferencia de barra lateral colapsada, por navegador. */
const CLAVE_COLAPSADA = "ars-docendi:barra-lateral-colapsada";

function leerColapsada(): boolean {
  try {
    return localStorage.getItem(CLAVE_COLAPSADA) === "true";
  } catch {
    return false;
  }
}

function guardarColapsada(colapsada: boolean) {
  try {
    localStorage.setItem(CLAVE_COLAPSADA, String(colapsada));
  } catch {
    // Sin almacenamiento (modo privado, bloqueado): la preferencia dura la sesión.
  }
}

/**
 * App shell (design 04a): barra lateral por permisos + main con scroll; la barra
 * superior fina sólo aparece en pantallas angostas.
 * Wraps every authenticated route; the active page renders into <Outlet>.
 *
 */
export default function AppLayout() {
  const usuarioActual = useCurrentUser();
  const [collapsed, setCollapsed] = useState(leerColapsada);
  const { pathname } = useLocation();
  // Bajo /ayuda la sidebar arranca contraída a íconos para dar lugar a la de
  // documentación, pero el botón "Menú" la puede desplegar mientras se está ahí.
  // Al salir vuelve la preferencia guardada, y cada entrada a /ayuda arranca
  // contraída de nuevo (ajuste de estado en render, sin efectos).
  const enAyuda = pathname === "/ayuda" || pathname.startsWith("/ayuda/");
  const [desplegadaEnAyuda, setDesplegadaEnAyuda] = useState(false);
  const [enAyudaPrevio, setEnAyudaPrevio] = useState(enAyuda);
  if (enAyuda !== enAyudaPrevio) {
    setEnAyudaPrevio(enAyuda);
    setDesplegadaEnAyuda(false);
  }
  const colapsado = enAyuda ? !desplegadaEnAyuda : collapsed;

  function alternarColapsada() {
    if (enAyuda) {
      // Sólo para esta visita a /ayuda: no toca la preferencia guardada.
      setDesplegadaEnAyuda((previa) => !previa);
      return;
    }
    const nueva = !collapsed;
    setCollapsed(nueva);
    guardarColapsada(nueva);
  }

  if (usuarioActual.isLoading) return <main role="status">Cargando sesión…</main>;
  if (usuarioActual.error || !usuarioActual.user) {
    return (
      <main role="alert">
        No se pudo validar la sesión.{" "}
        <button type="button" onClick={usuarioActual.retry}>
          Reintentar
        </button>
      </main>
    );
  }
  const user = usuarioActual.user;

  return (
    <div className={`adoc-ui adoc-app${colapsado ? " collapsed" : ""}`}>
      <TopBar user={user} />
      <Sidebar collapsed={colapsado} onToggleCollapse={alternarColapsada} user={user} />
      <main className="adoc-main">
        <Outlet />
      </main>
    </div>
  );
}
