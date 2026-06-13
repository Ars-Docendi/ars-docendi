import { Breadcrumbs } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";

export function IndexPage() {
  return (
    <>
      <Breadcrumbs separator="›" items={[{ label: "Inicio", href: "/" }, { label: "Mi Portal" }]} />
      <PageHeader title="Mi Portal Docente" />
      <p>Módulo en construcción — RF-03 Portal del Docente.</p>
    </>
  );
}
