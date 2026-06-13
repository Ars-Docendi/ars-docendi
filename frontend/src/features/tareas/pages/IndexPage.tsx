import { Breadcrumbs } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";

export function IndexPage() {
  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Tareas" }]} />
      <PageHeader pretitle="Cuatrimestre 2026 · 1C" title="Tareas" />
      <p>Módulo en construcción — RF-04 Seguimiento de Tareas.</p>
    </>
  );
}
