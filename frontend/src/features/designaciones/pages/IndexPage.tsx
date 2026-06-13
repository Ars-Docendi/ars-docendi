import { useNavigate } from "react-router-dom";
import { Breadcrumbs, Button } from "@ars-docendi/ui";
import { PageHeader } from "../../../shared/ui/PageHeader";

export function IndexPage() {
  const navegar = useNavigate();

  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Pedidos" }]} />
      <PageHeader pretitle="Cuatrimestre 2026 · 1C" title="Designaciones" />
      <p>Módulo en construcción — RF-01 Gestión de Proyecto Docente.</p>
      <Button variant="secondary" onClick={() => navegar("/designaciones/periodos")}>
        Configurar períodos de designación
      </Button>
    </>
  );
}
