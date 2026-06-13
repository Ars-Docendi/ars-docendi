import { Breadcrumbs } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";

export function IndexPage() {
  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[{ label: "Inicio", href: "/" }, { label: "Designaciones" }]}
      />
      <PageHeader pretitle="Cuatrimestre 2026 · 1C" title="Designaciones" />
      <p>Módulo en construcción — RF-01 Gestión de Proyecto Docente.</p>
    </>
  );
}
