import { useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { InlineAlert } from "@ars-docendi/ui";
import { Migas } from "../../../shared/navegacion/Migas";
import { useGuardiaDeCambios } from "../../../shared/navegacion/guardias";
import { useDestinoVolver } from "../../../shared/navegacion/useDestinoVolver";
import { PedidoForm } from "../components/PedidoForm";
import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { useMisPedidos, usePedido } from "../hooks/usePedidos";
import {
  useCrearPedido,
  useEditarPedido,
  useEnviarPedido,
  useReenviarPedido,
} from "../hooks/useAccionesPedido";
import { useCatalogosDesignaciones } from "../hooks/useCatalogosDesignaciones";
import { docentesDesdeCatalogo } from "../api/catalogos";
import { erroresValidacionPedido } from "../api/erroresValidacionPedido";
import type { DatosEditablesPedido } from "../types";

const RUTA_MIS_PEDIDOS = "/designaciones/mis-pedidos";

/** Etiqueta corta del período a partir de su id. */
function etiquetaPeriodo(periodoId: string, periodos: { id: string; nombre: string }[]): string {
  const periodo = periodos.find((item) => item.id === periodoId);
  return periodo?.nombre ?? "Período sin definir";
}

export function PedidoFormPage() {
  // El Jefe de Cátedra tiene pocas materias: lista. El resto (Administrativo, Secretaría…)
  // ve todas las del Departamento: buscador.
  const { user } = useCurrentUser();
  const buscadorMaterias = user?.roleCode !== "jefe_catedra";
  const { id } = useParams();
  const esEdicion = Boolean(id);
  const navegar = useNavigate();
  const location = useLocation();

  const { data: pedidos } = useMisPedidos();
  const catalogos = useCatalogosDesignaciones(id);
  const { data: pedidoInicial, isLoading, isError } = usePedido(id);
  const crear = useCrearPedido();
  const editar = useEditarPedido();
  const enviar = useEnviarPedido();
  const reenviar = useReenviarPedido();
  // Un pedido nuevo nace en Mis pedidos: vuelve a esa lista con los filtros con los que se salió
  // (`volverA` validado); cualquier otro origen se ignora.
  const origen = useDestinoVolver("pedido");
  const rutaMisPedidos =
    origen.desdeEstado && origen.ruta.split("?")[0] === RUTA_MIS_PEDIDOS
      ? origen.ruta
      : RUTA_MIS_PEDIDOS;
  const rutaRetorno = esEdicion && id ? `/designaciones/pedidos/${id}` : rutaMisPedidos;

  // El formulario avisa si tiene cambios; tras guardar se sale sin preguntar.
  const [sucio, setSucio] = useState(false);
  const { permitirSalida } = useGuardiaDeCambios(sucio);

  function volver() {
    // Reemplaza la edición en el historial y conserva el origen del detalle.
    navegar(rutaRetorno, { replace: true, state: location.state });
  }

  function volverTrasGuardar() {
    permitirSalida();
    volver();
  }

  function handleGuardar(datos: DatosEditablesPedido, opciones?: { enviar?: boolean }) {
    if (esEdicion && id) {
      editar.mutate(
        {
          id,
          datos: {
            ...datos,
            version: pedidoInicial?.version,
            periodoId: pedidoInicial?.periodoId,
            personaId: pedidoInicial?.personaId,
          },
        },
        {
          onSuccess: () => {
            if (!opciones?.enviar) {
              volverTrasGuardar();
              return;
            }
            const mutacion = pedidoInicial?.estado === "devuelto" ? reenviar : enviar;
            mutacion.mutate(id, { onSuccess: volverTrasGuardar });
          },
        },
      );
    } else {
      crear.mutate(datos, {
        onSuccess: (creado) => {
          if (!opciones?.enviar) {
            volverTrasGuardar();
            return;
          }
          enviar.mutate(creado.id, { onSuccess: volverTrasGuardar });
        },
      });
    }
  }

  const guardando = crear.isPending || editar.isPending || enviar.isPending || reenviar.isPending;
  const hayErrorMutacion = crear.isError || editar.isError || enviar.isError || reenviar.isError;
  const errorMutacion = crear.isError
    ? crear.error
    : editar.isError
      ? editar.error
      : enviar.isError
        ? enviar.error
        : reenviar.isError
          ? reenviar.error
          : null;
  const erroresBackend = erroresValidacionPedido(errorMutacion);
  const periodoLabel = etiquetaPeriodo(
    pedidoInicial?.periodoId ?? catalogos.data?.periodoActivo?.id ?? "",
    catalogos.data?.periodos ?? [],
  );

  return (
    <>
      <Migas
        items={[
          { label: "Inicio", href: "/" },
          { label: esEdicion ? "Detalle del pedido" : "Mis pedidos", href: rutaRetorno },
          { label: esEdicion ? "Editar pedido" : "Nuevo pedido" },
        ]}
      />

      {esEdicion && isLoading && (
        <p style={{ color: "var(--color-text-secondary)" }}>Cargando el pedido…</p>
      )}

      {esEdicion && isError && (
        <InlineAlert severity="danger" title="No se encontró el pedido">
          No pudimos cargar el pedido solicitado.{" "}
          <Link to={rutaRetorno} state={location.state}>
            Volver
          </Link>
          .
        </InlineAlert>
      )}

      {esEdicion && pedidoInicial && !pedidoInicial.accionesPermitidas?.includes("editar") && (
        <InlineAlert severity="info" title="Este pedido no es editable">
          El pedido no admite edición para el actor actual.{" "}
          <Link to={rutaRetorno} state={location.state}>
            Volver
          </Link>
          .
        </InlineAlert>
      )}

      {hayErrorMutacion && (
        <InlineAlert severity="danger" title="No se pudo guardar el pedido">
          {erroresBackend.length > 0 ? (
            <ul>
              {erroresBackend.map(({ campo, mensaje }, indice) => (
                <li key={`${campo}-${indice}`}>
                  <strong>{campo}:</strong> {mensaje}
                </li>
              ))}
            </ul>
          ) : (
            "Ocurrió un error al guardar. Revisá los datos e intentá de nuevo."
          )}
        </InlineAlert>
      )}

      {!esEdicion && catalogos.data && (
        <PedidoForm
          materias={catalogos.data.materias}
          pedidosExistentes={pedidos ?? []}
          periodoLabel={periodoLabel}
          guardando={guardando}
          onGuardar={handleGuardar}
          onCancelar={volver}
          onSucioChange={setSucio}
          docentes={docentesDesdeCatalogo(catalogos.data)}
          cargos={catalogos.data.cargos.map((c) => c.nombre)}
          dedicaciones={catalogos.data.dedicaciones.map((d) => d.nombre)}
          sectoresExternos={catalogos.data.sectoresExternos}
          buscadorMaterias={buscadorMaterias}
          tiposBaja={catalogos.data.tiposBaja}
        />
      )}

      {esEdicion &&
        pedidoInicial &&
        pedidoInicial.accionesPermitidas?.includes("editar") &&
        catalogos.data && (
          <PedidoForm
            pedidoInicial={pedidoInicial}
            catedra={pedidoInicial.catedra}
            materias={catalogos.data.materias}
            pedidosExistentes={pedidos ?? []}
            esEdicion
            periodoLabel={periodoLabel}
            guardando={guardando}
            onGuardar={handleGuardar}
            onCancelar={volver}
            onSucioChange={setSucio}
            docentes={docentesDesdeCatalogo(catalogos.data)}
            cargos={catalogos.data.cargos.map((c) => c.nombre)}
            dedicaciones={catalogos.data.dedicaciones.map((d) => d.nombre)}
            sectoresExternos={catalogos.data.sectoresExternos}
            buscadorMaterias={buscadorMaterias}
            tiposBaja={catalogos.data.tiposBaja}
          />
        )}
    </>
  );
}
