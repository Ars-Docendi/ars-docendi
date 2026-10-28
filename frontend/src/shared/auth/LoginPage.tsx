import { lazy, Suspense, useState } from "react";
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { Button, InlineAlert } from "@ars-docendi/ui";

import { developmentAuthEnabled } from "./developmentAuth";
import { IsotipoArsDocendi } from "../marca/IsotipoArsDocendi";
import { seleccionarSesionDesarrollo } from "./dev/session";
import { microsoftLoginEnabled, urlIngresoMicrosoft } from "./sesionMicrosoft";
import { useCurrentUser } from "./useCurrentUser";
import "./LoginPage.css";

const DevLoginModal = developmentAuthEnabled
  ? lazy(() => import("./dev/DevLoginModal").then((module) => ({ default: module.DevLoginModal })))
  : null;

/** Where to land after login: the route the guard bounced us from, else the default. */
function usePostLoginTarget(): string {
  const location = useLocation();
  const from = (location.state as { from?: { pathname?: string } } | null)?.from;
  return from?.pathname ?? "/portal";
}

/** Microsoft four-square brand glyph (decorative, page-local). */
function MsGlyph() {
  return (
    <span className="ms-glyph" aria-hidden="true">
      <i />
      <i />
      <i />
      <i />
    </span>
  );
}

/** Panel de bienvenida a la izquierda — puramente decorativo. */
function Marquee() {
  return (
    <aside className="login-marquee" aria-hidden="true">
      <IsotipoArsDocendi className="mark" />
      <div className="title-area">
        <h1>
          Te damos la bienvenida a <span className="accent">Ars Docendi</span>
        </h1>
        <p className="pitch">
          Acá vas a poder gestionar designaciones, reservar aulas, seguir tus tareas, mantener tu
          perfil docente y consultar al asistente conversacional, todo en un solo lugar.
        </p>
      </div>
      <p className="foot">
        Departamento de Ingeniería e Investigaciones Tecnológicas
        <br />
        Universidad Nacional de La Matanza
      </p>
    </aside>
  );
}

/** Visible login state: identity not yet attempted, SSO failed, or no access granted. */
type LoginState = "default" | "redirecting" | "error" | "forbidden";

export function LoginPage() {
  const [searchParams] = useSearchParams();
  const [redirecting, setRedirecting] = useState(false);
  const [devModalOpen, setDevModalOpen] = useState(false);
  const navigate = useNavigate();
  const target = usePostLoginTarget();
  const { user } = useCurrentUser();

  // Already signed in? Don't show the login screen — go straight to the app.
  if (user) {
    return <Navigate to={target} replace />;
  }

  // error/forbidden are surfaced by the Azure AD callback via a search param.
  const param = searchParams.get("error");
  const baseState: LoginState =
    param === "forbidden" ? "forbidden" : param === "auth" ? "error" : "default";
  const state: LoginState = redirecting ? "redirecting" : baseState;

  function handleLogin() {
    // El backend hace el ingreso con Microsoft y vuelve a `target` con la cookie de sesión.
    if (microsoftLoginEnabled) {
      setRedirecting(true);
      window.location.assign(urlIngresoMicrosoft(target));
      return;
    }
    // Sin ingreso con Microsoft, el botón abre el selector sembrado (sólo no productivo).
    if (DevLoginModal) setDevModalOpen(true);
  }

  function handleDevSelect(userId: string, roleCode: string) {
    seleccionarSesionDesarrollo(userId, roleCode);
    // Close the picker and show the loading button briefly before entering the app.
    setDevModalOpen(false);
    setRedirecting(true);
    setTimeout(() => {
      navigate(target, { replace: true });
    }, 1000);
  }

  return (
    <div className="login-page adoc-ui">
      <Marquee />

      <div className="login-form-side">
        <div className="login-brand">
          <IsotipoArsDocendi className="login-brand-mark" />
          <span>
            <span className="accent">Ars</span> Docendi
          </span>
        </div>

        <div className="login-card">
          <div className="head">
            <h2>Ingresá a tu cuenta</h2>
            <p>Usá tu cuenta institucional @docentes.unlam.edu.ar.</p>
          </div>

          {state === "error" && (
            <InlineAlert severity="danger" title="No pudimos confirmar tu identidad.">
              Microsoft devolvió un error de autenticación. Probá nuevamente; si persiste,
              comunicate con Secretaría Académica.
            </InlineAlert>
          )}

          {/* No revela si la cuenta existe, está desactivada o no tiene rol. */}
          {state === "forbidden" && (
            <InlineAlert severity="warning" title="No pudimos iniciar tu sesión.">
              Comunicate con Secretaría Académica.
            </InlineAlert>
          )}

          <div className="cta">
            {state === "redirecting" ? (
              <Button variant="primary" size="lg" loading disabled>
                Redirigiendo a Microsoft…
              </Button>
            ) : (
              <Button variant="primary" size="lg" leadingIcon={<MsGlyph />} onClick={handleLogin}>
                Ingresar con Microsoft
              </Button>
            )}
            {microsoftLoginEnabled && DevLoginModal && state !== "redirecting" && (
              <Button variant="ghost" size="sm" onClick={() => setDevModalOpen(true)}>
                Ingresar con una identidad de desarrollo
              </Button>
            )}
          </div>
        </div>

        <footer className="login-legal">
          Al ingresar aceptás los{" "}
          <Link to="/terminos">Términos de uso y la Política de privacidad</Link>.
        </footer>
      </div>

      {DevLoginModal && (
        <Suspense fallback={null}>
          <DevLoginModal
            open={devModalOpen}
            onClose={() => setDevModalOpen(false)}
            onSelect={handleDevSelect}
          />
        </Suspense>
      )}
    </div>
  );
}
