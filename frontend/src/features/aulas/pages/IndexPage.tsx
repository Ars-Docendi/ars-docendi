import { Breadcrumbs } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";

export function IndexPage() {
  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Aulas" }]} />
      <PageHeader pretitle="Cuatrimestre 2026 · 1C" title="Reserva de Aulas" />
      <p>Módulo en construcción — RF-02 Reserva de Aulas / Laboratorios.</p>
    </>
  );
}
