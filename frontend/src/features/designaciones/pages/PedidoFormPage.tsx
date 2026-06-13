import { useState } from "react";
import { useParams, useSearchParams, useNavigate } from "react-router-dom";
import { Breadcrumbs, Button, InlineAlert } from "@ars-docendi/ui";

import { PageHeader } from "../../../shared/ui/PageHeader";
import { FormToc } from "../components/FormToc";
import { SeccionTipo } from "../components/SeccionTipo";
import { SeccionDocente } from "../components/SeccionDocente";
import { SeccionDesignacion } from "../components/SeccionDesignacion";
import { SeccionJustificacion } from "../components/SeccionJustificacion";
import { SeccionDocumentacion } from "../components/SeccionDocumentacion";
import { FooterPedido } from "../components/FooterPedido";
import { useValidacionPedido } from "../hooks/useValidacionPedido";
import {
  exigeDocumentacion,
  pedidoAltaNueva,
  pedidoInicial,
  type ArchivoCargado,
  type DatosDocente,
  type DesignacionSolicitada,
  type EstadoPantalla,
  type PedidoMock,
  type TipoPedido,
} from "../mock/mockPedido";
import "../pedido-form.css";

type SlotObligatorio = "cv" | "dniFrente" | "dniDorso";

function PedidoFormEditando({ inicial }: { inicial: PedidoMock }) {
  const navigate = useNavigate();
  const [pedido, setPedido] = useState<PedidoMock>(inicial);
  const validacion = useValidacionPedido(pedido);

  function setDocente<K extends keyof DatosDocente>(campo: K, valor: DatosDocente[K]) {
    setPedido((p) => ({ ...p, docente: { ...p.docente, [campo]: valor } }));
  }
  function setDesignacion<K extends keyof DesignacionSolicitada>(
    campo: K,
    valor: DesignacionSolicitada[K],
  ) {
    setPedido((p) => ({ ...p, designacion: { ...p.designacion, [campo]: valor } }));
  }
  function setTipo(tipo: TipoPedido) {
    setPedido((p) => ({ ...p, tipo }));
  }
  function setDoc(slot: SlotObligatorio, archivo: ArchivoCargado | null) {
    setPedido((p) => ({ ...p, documentacion: { ...p.documentacion, [slot]: archivo } }));
  }
  function addOtros(archivos: ArchivoCargado[]) {
    setPedido((p) => ({
      ...p,
      documentacion: { ...p.documentacion, otros: [...p.documentacion.otros, ...archivos] },
    }));
  }
  function removeOtro(id: string) {
    setPedido((p) => ({
      ...p,
      documentacion: {
        ...p.documentacion,
        otros: p.documentacion.otros.filter((a) => a.id !== id),
      },
    }));
  }

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Pedidos", href: "/designaciones" },
          { label: "Crear / editar" },
        ]}
      />
      <PageHeader
        pretitle={`Pedido ${pedido.numero} · borrador`}
        title="Pedido de Designación"
        meta="Última edición: hoy · 14:02 · autoguardado activo"
        actions={
          <Button variant="ghost" onClick={() => navigate("/designaciones")}>
            Descartar borrador
          </Button>
        }
      />

      <div className="pedido-form-page">
        <FormToc items={validacion.itemsToc} active="tipo" />

        <div>
          <SeccionTipo tipo={pedido.tipo} onCambiarTipo={setTipo} />

          {exigeDocumentacion(pedido.tipo) && (
            <div className="pedido-banner">
              <b>Alta nueva</b> — vas a tener que adjuntar{" "}
              <b>CV en PDF + foto del DNI (frente y dorso)</b> en la sección de Documentación. El
              pedido no se puede enviar a revisión sin esos tres archivos.
            </div>
          )}

          <SeccionDocente tipo={pedido.tipo} docente={pedido.docente} onCambiar={setDocente} />
          <SeccionDesignacion
            tipo={pedido.tipo}
            designacion={pedido.designacion}
            onCambiar={setDesignacion}
          />
          <SeccionJustificacion
            justificacion={pedido.justificacion}
            onCambiar={(valor) => setPedido((p) => ({ ...p, justificacion: valor }))}
          />
          <SeccionDocumentacion
            tipo={pedido.tipo}
            documentacion={pedido.documentacion}
            onSetDoc={setDoc}
            onAddOtros={addOtros}
            onRemoveOtro={removeOtro}
          />
        </div>
      </div>

      <FooterPedido
        mensaje={validacion.mensajeFooter}
        tono={validacion.tonoFooter}
        puedeEnviar={validacion.puedeEnviar}
        onCancelar={() => navigate("/designaciones")}
        onGuardarBorrador={() => navigate("/designaciones")}
        onEnviar={() => navigate("/designaciones")}
      />
    </>
  );
}

function PedidoFormCargando() {
  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Pedidos", href: "/designaciones" },
          { label: "Crear / editar" },
        ]}
      />
      <div className="adoc-page-head">
        <div className="title-area">
          <span className="adoc-skel" style={{ width: 180, height: 11, marginBottom: 10 }} />
          <span className="adoc-skel" style={{ width: 280, height: 24 }} />
        </div>
      </div>
      <div className="pedido-form-page">
        <div>
          <span
            className="adoc-skel"
            style={{ width: 100, height: 10, marginBottom: 12, display: "block" }}
          />
          {[1, 2, 3, 4, 5].map((i) => (
            <span
              key={i}
              className="adoc-skel"
              style={{ width: "85%", height: 13, marginBottom: 14, display: "block" }}
            />
          ))}
        </div>
        <div>
          {[1, 2, 3].map((s) => (
            <div
              key={s}
              className="adoc-form-section"
              style={{ marginBottom: 16, padding: 18 }}
              aria-busy="true"
            >
              <span className="adoc-skel" style={{ width: 200, height: 14, marginBottom: 18 }} />
              <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 14 }}>
                {[1, 2, 3, 4, 5, 6].map((f) => (
                  <div key={f}>
                    <span
                      className="adoc-skel"
                      style={{ width: "50%", height: 10, marginBottom: 8 }}
                    />
                    <span className="adoc-skel" style={{ width: "100%", height: 36 }} />
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </>
  );
}

function PedidoFormError({ inicial }: { inicial: PedidoMock }) {
  const navigate = useNavigate();

  function descargarCopiaLocal() {
    const blob = new Blob([JSON.stringify(inicial, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `pedido-${inicial.id}-borrador.json`;
    a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <>
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Pedidos", href: "/designaciones" },
          { label: "Crear / editar" },
        ]}
      />
      <PageHeader pretitle={`Pedido ${inicial.numero} · borrador`} title="Pedido de Designación" />

      <div style={{ marginBottom: 18 }}>
        <InlineAlert severity="danger" title="No pudimos guardar los últimos cambios.">
          El borrador se conserva en tu navegador, pero no llegó al servidor. Verificá tu conexión y
          volvé a intentar.
          <div style={{ marginTop: 8, display: "flex", gap: 8 }}>
            <Button variant="secondary" size="sm" onClick={() => navigate(0)}>
              Reintentar autoguardado
            </Button>
            <Button variant="ghost" size="sm" onClick={descargarCopiaLocal}>
              Descargar copia local
            </Button>
          </div>
        </InlineAlert>
      </div>

      <FooterPedido
        mensaje="! Cambios no guardados · sin conexión al servidor"
        tono="warn"
        puedeEnviar={false}
        acciones={false}
        onCancelar={() => navigate("/designaciones")}
        onGuardarBorrador={() => {}}
        onEnviar={() => {}}
      />
    </>
  );
}

/**
 * Página crear/editar Pedido de Designación. La presencia de `:id` distingue
 * edición de creación. Los estados loading/error son alcanzables vía `?estado=`
 * para revisión de diseño (no hay backend todavía).
 */
export function PedidoFormPage() {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const estado = (searchParams.get("estado") as EstadoPantalla | null) ?? "edit";

  // Semilla: al editar, un pedido existente; al crear, según el tipo pedido por query.
  const inicial =
    !id && searchParams.get("tipo") === "alta-nueva" ? pedidoAltaNueva() : pedidoInicial();

  if (estado === "loading") return <PedidoFormCargando />;
  if (estado === "error") return <PedidoFormError inicial={inicial} />;
  return <PedidoFormEditando inicial={inicial} />;
}
