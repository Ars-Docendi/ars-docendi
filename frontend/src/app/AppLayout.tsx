import { useRef, useState } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";

import {
  CampanaSidebar,
  CampanaTopBar,
} from "../features/notificaciones/components/CampanaNotificaciones";
import { PanelNotificaciones } from "../features/notificaciones/components/PanelNotificaciones";
import { useCantidadNoLeidas } from "../features/notificaciones/hooks/useNotificaciones";
import { rutaDeEntidad } from "../features/notificaciones/presentacion";
import type { Notificacion } from "../features/notificaciones/types";
import { useCurrentUser, type CurrentUser } from "../shared/auth/useCurrentUser";
import { resaltar } from "../shared/ui/resaltado";
import { Toaster } from "../shared/ui/Toaster";
import { ModalCambiarRol } from "./shell/MenuUsuario";
import { Sidebar } from "./shell/Sidebar";
import { TopBar } from "./shell/TopBar";
import { usePantallaAngosta } from "./shell/usePantallaAngosta";
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
  return (
    <ShellAutenticado
      user={usuarioActual.user}
      colapsado={colapsado}
      alternarColapsada={alternarColapsada}
    />
  );
}

/** Ancho de la barra lateral (px) según su estado: el panel de notificaciones se abre a su lado. */
const ANCHO_BARRA = { expandida: 200, colapsada: 64 };

interface ShellAutenticadoProps {
  user: CurrentUser;
  colapsado: boolean;
  alternarColapsada: () => void;
}

/** Shell de una sesión válida: barras, contenido, campana y panel de notificaciones. */
function ShellAutenticado({ user, colapsado, alternarColapsada }: ShellAutenticadoProps) {
  const navigate = useNavigate();
  const angosta = usePantallaAngosta();
  const [notifAbierta, setNotifAbierta] = useState(false);
  const campanaRef = useRef<HTMLButtonElement>(null);
  // Las notificaciones se ven por perfil: el rol activo es parte de toda clave.
  const rol = user.roleCode;
  const { data: cantidad } = useCantidadNoLeidas(rol);

  const alternarNotificaciones = () => setNotifAbierta((abierta) => !abierta);
  function irANotificacion(notificacion: Notificacion) {
    setNotifAbierta(false);
    navigate(rutaDeEntidad(notificacion.tipoEntidad, notificacion.entidadId));
    resaltar(notificacion.entidadId);
  }
  const campana = angosta ? (
    <CampanaTopBar
      ref={campanaRef}
      cantidad={cantidad}
      abierta={notifAbierta}
      onClick={alternarNotificaciones}
    />
  ) : (
    <CampanaSidebar
      ref={campanaRef}
      cantidad={cantidad}
      abierta={notifAbierta}
      colapsada={colapsado}
      onClick={alternarNotificaciones}
    />
  );

  return (
    <div className={`adoc-ui adoc-app${colapsado ? " collapsed" : ""}`}>
      <TopBar user={user} campana={angosta ? campana : undefined} />
      <Sidebar
        collapsed={colapsado}
        onToggleCollapse={alternarColapsada}
        user={user}
        campana={angosta ? undefined : campana}
      />
      <main className="adoc-main">
        <Outlet />
      </main>
      <PanelNotificaciones
        abierto={notifAbierta}
        onCerrar={() => setNotifAbierta(false)}
        triggerRef={campanaRef}
        desplazamiento={angosta ? 0 : colapsado ? ANCHO_BARRA.colapsada : ANCHO_BARRA.expandida}
        rol={rol}
        onNavegar={irANotificacion}
      />
      {/* Con varias asignaciones, al ingresar se ofrece elegir; ya opera con la de por defecto. */}
      {user.requiereElegirRol && user.asignaciones.length > 1 && (
        <ModalCambiarRol user={user} alIngresar onCerrar={() => undefined} />
      )}
      <Toaster />
    </div>
  );
}
