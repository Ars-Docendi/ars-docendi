import { useState } from "react";
import { Navigate, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { Button, InlineAlert } from "@ars-docendi/ui";

import { isAuthenticated, setToken } from "./auth";
import "./LoginPage.css";

/** Where to land after login: the route the guard bounced us from, else the default. */
function usePostLoginTarget(): string {
  const location = useLocation();
  const from = (location.state as { from?: { pathname?: string } } | null)?.from;
  return from?.pathname ?? "/designaciones";
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

/** Left institutional marquee — purely decorative. */
function Marquee() {
  return (
    <aside className="login-marquee" aria-hidden="true">
      <div className="crest">
        <div className="mark">UNLaM</div>
        <div>
          Departamento de Ingeniería
          <small>Universidad Nacional de La Matanza</small>
        </div>
      </div>
      <div className="title-area">
        <h1>
          Ars <span className="accent">Docendi</span>
        </h1>
        <p className="pitch">
          Designaciones, reservas de aula, tareas y perfil del docente — un solo lugar, una sola
          identidad institucional.
        </p>
      </div>
      <div className="foot">
        <span>v0.1</span>
      </div>
    </aside>
  );
}

/** Visible login state: identity not yet attempted, SSO failed, or no access granted. */
type LoginState = "default" | "redirecting" | "error" | "forbidden";

export function LoginPage() {
  const [searchParams] = useSearchParams();
  const [redirecting, setRedirecting] = useState(false);
  const navigate = useNavigate();
  const target = usePostLoginTarget();

  // Already signed in? Don't show the login screen — go straight to the app.
  if (isAuthenticated()) {
    return <Navigate to={target} replace />;
  }

  // error/forbidden are surfaced by the Azure AD callback via a search param.
  const param = searchParams.get("error");
  const baseState: LoginState =
    param === "forbidden" ? "forbidden" : param === "auth" ? "error" : "default";
  const state: LoginState = redirecting ? "redirecting" : baseState;

  function handleLogin() {
    setRedirecting(true);
    // TODO: real Azure AD redirect. When a real entry URL is configured, hand off to it.
    const loginUrl = import.meta.env.VITE_AUTH_LOGIN_URL;
    if (loginUrl) {
      window.location.assign(loginUrl);
      return;
    }
    // STUB (dev): no Azure AD wired yet — mint a placeholder token and enter the app.
    // Replace with the MSAL callback that stores the real token. The delay is purely
    // cosmetic so the "Redirigiendo a Microsoft…" loading state is visible in dev.
    setTimeout(() => {
      setToken("dev-stub-token");
      navigate(target, { replace: true });
    }, 1500);
  }

  return (
    <div className="login-page adoc-ui">
      <Marquee />

      <div className="login-form-side">
        <div className="login-card">
          <div className="head">
            <h2>Iniciá sesión</h2>
            <p>
              Usá tu cuenta institucional para acceder. No hay registro: tu acceso se gestiona desde
              UNLaM.
            </p>
          </div>

          {state === "error" && (
            <InlineAlert severity="danger" title="No pudimos confirmar tu identidad.">
              Microsoft devolvió un error de autenticación. Probá nuevamente; si persiste, contactá
              a Soporte.
              <div className="alert-code">AADSTS50058 · sesión no encontrada</div>
            </InlineAlert>
          )}

          {state === "forbidden" && (
            <InlineAlert
              severity="warning"
              title="Tu cuenta existe, pero todavía no tenés acceso a Ars Docendi."
            >
              Pedile al responsable de tu sector que solicite el alta a Secretaría Académica.
              <div style={{ marginTop: "10px" }}>
                <a href="#">Ver instrucciones para solicitar acceso →</a>
              </div>
            </InlineAlert>
          )}

          <div className="cta">
            {state === "redirecting" ? (
              <Button variant="primary" size="lg" loading disabled>
                Redirigiendo a Microsoft…
              </Button>
            ) : state === "forbidden" ? (
              <Button variant="secondary" size="lg" leadingIcon={<MsGlyph />} disabled>
                Cerrar sesión
              </Button>
            ) : (
              <Button variant="primary" size="lg" leadingIcon={<MsGlyph />} onClick={handleLogin}>
                Iniciar sesión con cuenta institucional
              </Button>
            )}
          </div>

          <div className="divider" />

          <div className="helper">
            <b style={{ color: "var(--color-text-primary)" }}>¿Problemas para ingresar?</b>
            <br />
            Escribí a{" "}
            <a href="mailto:soporte.adocendi@unlam.edu.ar">soporte.adocendi@unlam.edu.ar</a>{" "}
            indicando tu legajo o DNI.
          </div>

          <div className="footnote-tiny">
            Al ingresar aceptás el uso institucional del sistema. Las acciones quedan registradas en
            el log de auditoría.
          </div>
        </div>
      </div>
    </div>
  );
}
