import { Link, isRouteErrorResponse, useRouteError } from "react-router-dom";
import { Button } from "@ars-docendi/ui";

import { PageHeader } from "../ui/PageHeader";

function IrAlInicio() {
  return (
    <Link to="/" className="adoc-btn v-primary">
      Ir al inicio
    </Link>
  );
}

export function PaginaNoEncontrada() {
  return (
    <>
      <PageHeader title="Página no encontrada" />
      <p style={{ color: "var(--color-text-secondary)" }}>
        La dirección que abriste no existe o fue movida.
      </p>
      <IrAlInicio />
    </>
  );
}

export function PaginaSinAcceso({ rol }: { rol?: string }) {
  return (
    <>
      <PageHeader title="No tenés acceso a esta página" />
      <p style={{ color: "var(--color-text-secondary)" }}>
        El acceso a esta página depende del perfil activo.
        {rol
          ? ` Con tu perfil actual (${rol}) no tenés permiso para abrirla;`
          : " Con tu perfil actual no tenés permiso para abrirla;"}{" "}
        si tenés otro perfil, cambiá a ese perfil e intentá de nuevo.
      </p>
      <IrAlInicio />
    </>
  );
}

/** `errorElement` del shell: un error de render o de carga deja el menú y muestra esta página. */
export function PaginaDeError() {
  const error = useRouteError();
  const noEncontrada = isRouteErrorResponse(error) && error.status === 404;
  if (noEncontrada) return <PaginaNoEncontrada />;
  return (
    <>
      <PageHeader title="Algo salió mal" />
      <p style={{ color: "var(--color-text-secondary)" }}>
        No pudimos mostrar esta página. Recargala o volvé al inicio; si el problema sigue, contactá
        a Soporte.
      </p>
      <div style={{ display: "flex", gap: "var(--space-3)" }}>
        <Button variant="secondary" onClick={() => window.location.reload()}>
          Recargar
        </Button>
        <IrAlInicio />
      </div>
    </>
  );
}
