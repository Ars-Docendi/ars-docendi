import { Breadcrumbs, Button } from "@ars-docendi/ui";
import { useNavigate } from "react-router-dom";

import { PageHeader } from "../../../shared/ui/PageHeader";

export function IndexPage() {
  const navigate = useNavigate();
  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[{ label: "Inicio", href: "/" }, { label: "Designaciones" }]}
      />
      <PageHeader
        pretitle="Cuatrimestre 2026 · 1C"
        title="Designaciones"
        actions={
          <Button variant="primary" onClick={() => navigate("pedidos/nuevo")}>
            Nuevo pedido
          </Button>
        }
      />
      <p>Módulo en construcción — RF-01 Gestión de Proyecto Docente.</p>
    </>
  );
}
