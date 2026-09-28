import { Breadcrumbs } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";

export function IndexPage() {
  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[{ label: "Inicio", href: "/" }, { label: "Reserva de aulas" }]}
      />
      <PageHeader title="Reserva de aulas" />
      <p>Módulo en construcción — RF-02 Reserva de Aulas / Laboratorios.</p>
    </>
  );
}
