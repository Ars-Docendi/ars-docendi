import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@ars-docendi/ui";

import { BannerDeMantenimiento } from "./BannerDeMantenimiento";
import { KpisDeUso } from "./KpisDeUso";
import { PanelDeUso } from "./PanelDeUso";
import { ServidorDelModeloCard } from "./ServidorDelModeloCard";
import { TopeOrganizacionalCard } from "./TopeOrganizacionalCard";
import {
  editarAccesoDeRol,
  editarAccesoDeUsuario,
  editarCupoDeRol,
  editarCupoDeUsuario,
  restablecerCupoDeUsuario,
  editarMantenimiento,
  editarTopeOrganizacional,
  obtenerPresupuestos,
  obtenerServidorLocal,
  obtenerUso,
} from "../api/administracionAsistenteApi";
import { obtenerCapacidades } from "../api/asistenteApi";
import { IconoDownload } from "../../../shared/ui/iconos";
import { construirCsvDeUso, nombreDeArchivoDeUso } from "../utils/exportarUsoCsv";
import { descargarArchivo } from "../utils/descargas";
import type { PeriodoDeUso } from "../types";
import "../asistente.css";

const PERIODOS: { valor: PeriodoDeUso; etiqueta: string }[] = [
  { valor: "dia", etiqueta: "Hoy" },
  { valor: "semana", etiqueta: "7 días" },
  { valor: "mes", etiqueta: "30 días" },
];

interface PanelAdministracionAsistenteProps {
  /**
   * Contador que sube cada vez que la pestaña Asistente de «Sistema» pide
   * «Actualizar» (design D8, sistema-seccion-unificada). El panel reacciona
   * invalidando sus propias queries (`["asistente","administracion"]` y
   * `["asistente","capacidades"]`) SIN perder el período elegido — nada más
   * se resetea.
   */
  actualizacion?: number;
}

/**
 * El panel administrativo del asistente (asistente-administracion-de-uso),
 * en su pasada de fidelidad 1:1 con el canvas «Uso del asistente» de Claude
 * Design: título propio + período relabeleado («Hoy»/«7 días»/«30 días»,
 * mismo criterio que ya usa `FiltrosAuditoria`) y «Exportar CSV» a la
 * derecha; el banner de mantenimiento compacto (`BannerDeMantenimiento`) en
 * vez de la tarjeta con toggle; los cinco KPIs arriba; y el detalle por
 * usuario/rol con el cupo diario editado directo en la fila (`PanelDeUso`).
 *
 * EL TÍTULO «Uso del asistente» ES UN `<h2>`, no un segundo `<h1>`: la
 * página «Sistema» ya tiene el suyo (`PageHeader`); este es el título de LA
 * SECCIÓN dentro de la pestaña, tal como lo muestra el canvas —antes este
 * panel no tenía ningún título propio, lo cual dejaba a la pestaña «Asistente»
 * sin la jerarquía de encabezados que sí tiene, por ejemplo, la pestaña
 * Auditoría.
 *
 * SÓLO LLEGA ACÁ QUIEN TIENE `asistente.administrar`: el gate lo aplica la
 * pestaña de `SistemaPage` (mismo criterio que antes aplicaba `routes.tsx`
 * con `RequirePermission`) — este componente no vuelve a chequear el permiso.
 */
export function PanelAdministracionAsistente({ actualizacion }: PanelAdministracionAsistenteProps) {
  const [periodo, setPeriodo] = useState<PeriodoDeUso>("dia");
  const [pestana, setPestana] = useState<"usuarios" | "roles">("usuarios");
  const [anuncio, setAnuncio] = useState("");
  const queryClient = useQueryClient();

  const uso = useQuery({
    queryKey: ["asistente", "administracion", "uso", periodo],
    queryFn: () => obtenerUso(periodo),
  });

  // El tope, el gasto del mes y los cupos persistidos (tarea 12.8 de
  // sistema-seccion-unificada) — reemplaza el "guardado en esta sesión" que
  // `TopeOrganizacionalCard`/`EditorDeCupoEnFila` usaban porque no había
  // ningún `GET` todavía.
  const presupuestos = useQuery({
    queryKey: ["asistente", "administracion", "presupuestos"],
    queryFn: obtenerPresupuestos,
  });

  // La carga del servidor del modelo propio (asistente-optimizaciones-modelo-local,
  // D8). Se refresca sola cada 15 s porque es lo que el admin mira cuando
  // alguien avisa que «el asistente anda lento»; con proveedor en la nube el
  // endpoint contesta `configurado: false` y la tarjeta no aparece.
  const servidorLocal = useQuery({
    queryKey: ["asistente", "administracion", "servidor-local"],
    queryFn: obtenerServidorLocal,
    refetchInterval: 15_000,
  });

  const capacidades = useQuery({
    queryKey: ["asistente", "capacidades"],
    queryFn: obtenerCapacidades,
  });

  async function refrescarCapacidades() {
    await queryClient.invalidateQueries({ queryKey: ["asistente", "capacidades"] });
  }

  async function refrescarPresupuestos() {
    await queryClient.invalidateQueries({
      queryKey: ["asistente", "administracion", "presupuestos"],
    });
  }

  // «Actualizar» de la sección Sistema: invalida las queries propias del panel
  // sin tocar el período elegido (design D8). No corre en el montaje inicial.
  useEffect(() => {
    if (actualizacion === undefined) return;
    void queryClient.invalidateQueries({ queryKey: ["asistente", "administracion"] });
    void refrescarCapacidades();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [actualizacion]);

  async function guardarMantenimiento(activo: boolean, razon?: string) {
    await editarMantenimiento(activo, razon);
    await refrescarCapacidades();
    // El foco se queda en el botón que se apretó: el anuncio es la ÚNICA cosa
    // que se mueve (tasks.md 13.2), igual que ya hace el resto de la feature
    // con sus propias confirmaciones.
    setAnuncio(activo ? "Modo mantenimiento activado." : "Modo mantenimiento desactivado.");
  }

  async function guardarTope(topeMensualUsd: number) {
    await editarTopeOrganizacional(topeMensualUsd);
    await refrescarPresupuestos();
  }

  // El cupo y el acceso de una fila de USUARIO los resuelve el backend en
  // `GET …/uso` (`cupoEfectivo`, `accesoEfectivo`), no en `…/presupuestos`:
  // después de editar cualquiera de los dos hay que refrescar ambos, o la
  // fila sigue mostrando el valor viejo.
  async function refrescarUsoYPresupuestos() {
    await queryClient.invalidateQueries({ queryKey: ["asistente", "administracion"] });
  }

  async function guardarCupoDeRolYRefrescar(rol: string, cupo: number) {
    await editarCupoDeRol(rol, cupo);
    await refrescarUsoYPresupuestos();
  }

  async function guardarCupoDeUsuarioYRefrescar(actorId: string, cupo: number) {
    await editarCupoDeUsuario(actorId, cupo);
    await refrescarUsoYPresupuestos();
  }

  async function restablecerCupoDeUsuarioYRefrescar(actorId: string) {
    await restablecerCupoDeUsuario(actorId);
    await refrescarUsoYPresupuestos();
  }

  async function cambiarAccesoDeRolYRefrescar(rol: string, habilitado: boolean) {
    await editarAccesoDeRol(rol, habilitado);
    await refrescarUsoYPresupuestos();
  }

  async function cambiarAccesoDeUsuarioYRefrescar(actorId: string, habilitado: boolean) {
    await editarAccesoDeUsuario(actorId, habilitado);
    await refrescarUsoYPresupuestos();
  }

  // «Exportar CSV»: exporta EXACTAMENTE lo que ya está cargado para la vista
  // activa (Por usuario / Por rol) del período elegido — ningún pedido nuevo
  // al backend, ninguna columna que la tabla no muestre ya.
  function exportarCsv() {
    if (!uso.data) return;
    const filas = pestana === "usuarios" ? uso.data.porUsuario : uso.data.porRol;
    const csv = construirCsvDeUso(filas, pestana === "usuarios");
    descargarArchivo(nombreDeArchivoDeUso(periodo), csv, "text/csv;charset=utf-8");
  }

  const usuariosActivos = uso.data?.porUsuario.filter((f) => f.turnos > 0).length;

  return (
    <div className="adoc-asistente-administracion">
      {/* Única región viva de esta pantalla: anuncia guardados sin mover el foco
          (tasks.md 13.2), con el mismo mecanismo simple que ya usa
          `IndicadorDeProceso` —un solo texto, reemplazado entero—. */}
      <p role="status" aria-live="polite" className="adoc-asistente-admin-anuncio">
        {anuncio}
      </p>

      <div className="adoc-asistente-admin-encabezado">
        <div>
          <h2 className="adoc-asistente-admin-titulo">Uso del asistente</h2>
          <p className="adoc-asistente-admin-copete">
            Consumo, presupuestos, acceso y mantenimiento.
          </p>
        </div>
        <div className="adoc-asistente-admin-encabezado-acciones">
          <div className="adoc-asistente-admin-periodo" role="group" aria-label="Período">
            {PERIODOS.map((p) => (
              <button
                key={p.valor}
                type="button"
                aria-pressed={periodo === p.valor}
                className="adoc-asistente-admin-periodo-boton"
                onClick={() => setPeriodo(p.valor)}
              >
                {p.etiqueta}
              </button>
            ))}
          </div>
          <Button
            variant="secondary"
            type="button"
            className="adoc-asistente-admin-exportar"
            onClick={exportarCsv}
            disabled={!uso.data}
          >
            <IconoDownload />
            Exportar CSV
          </Button>
        </div>
      </div>

      <BannerDeMantenimiento
        mantenimiento={capacidades.data?.mantenimiento}
        onGuardar={guardarMantenimiento}
      />

      <section aria-label="Organización" className="adoc-asistente-admin-kpis-fila">
        <KpisDeUso organizacion={uso.data?.organizacion} usuariosActivos={usuariosActivos} />
        <TopeOrganizacionalCard
          topeConocido={presupuestos.data?.topeMensualUsd}
          gastoEstimadoDelMes={presupuestos.data?.gastoEstimadoDelMes}
          error={presupuestos.isError}
          onGuardar={guardarTope}
          onGuardado={setAnuncio}
        />
        <ServidorDelModeloCard servidor={servidorLocal.data} />
      </section>

      <section aria-label="Panel de uso">
        <PanelDeUso
          uso={uso.data}
          cargando={uso.isLoading}
          cuposPorRol={presupuestos.data?.cuposPorRol ?? []}
          onGuardarCupoDeRol={guardarCupoDeRolYRefrescar}
          onGuardarCupoDeUsuario={guardarCupoDeUsuarioYRefrescar}
          onRestablecerCupoDeUsuario={restablecerCupoDeUsuarioYRefrescar}
          onCambiarAccesoDeRol={cambiarAccesoDeRolYRefrescar}
          onCambiarAccesoDeUsuario={cambiarAccesoDeUsuarioYRefrescar}
          onGuardado={setAnuncio}
          pestana={pestana}
          onCambiarPestana={setPestana}
        />
      </section>
    </div>
  );
}
