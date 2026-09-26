import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Input } from "@ars-docendi/ui";

import { DetalleDeSoporte } from "../components/DetalleDeSoporte";
import { SelectorDeSujeto } from "../components/SelectorDeSujeto";
import { PageHeader } from "../../../shared/ui/PageHeader";
import {
  buscarPersonasParaSoporte,
  leerHistorialDeSoporte,
  listarHistorialDeSoporte,
} from "../api/soporteHistorialApi";
import "../asistente.css";

/**
 * Lectura de soporte del historial AJENO
 * (asistente-acceso-de-soporte-al-historial).
 *
 * SÓLO LLEGA ACÁ QUIEN TIENE EL PERMISO: `routes.tsx` la envuelve con
 * `RequirePermission`, así que esta página no vuelve a chequearlo —el mismo
 * criterio que `PeriodosPage`/`RequirePermission` en `designaciones`—.
 *
 * LA RAZÓN ES OBLIGATORIA ANTES DE VER NADA (design.md D10): sin ella no se
 * pide ni el listado ni una conversación puntual — los dos endpoints del
 * backend la exigen igual, así que esto es la misma regla del lado del
 * cliente, no una regla nueva.
 */
export function SoporteHistorialPage() {
  const [sujetoId, setSujetoId] = useState<string | null>(null);
  const [razon, setRazon] = useState("");
  const [hiloAbierto, setHiloAbierto] = useState<string | null>(null);
  const razonValida = razon.trim().length > 0;

  const personas = useQuery({
    queryKey: ["asistente", "soporte-historial", "personas"],
    queryFn: buscarPersonasParaSoporte,
  });

  const conversaciones = useQuery({
    queryKey: ["asistente", "soporte-historial", "listar", sujetoId, razon],
    queryFn: () => listarHistorialDeSoporte(sujetoId ?? "", razon),
    enabled: Boolean(sujetoId) && razonValida,
  });

  const detalle = useQuery({
    queryKey: ["asistente", "soporte-historial", "leer", sujetoId, hiloAbierto, razon],
    queryFn: () => leerHistorialDeSoporte(sujetoId ?? "", hiloAbierto ?? "", razon),
    enabled: Boolean(sujetoId) && Boolean(hiloAbierto) && razonValida,
  });

  function elegirSujeto(id: string) {
    setSujetoId(id);
    setHiloAbierto(null);
  }

  function cambiarRazon(valor: string) {
    setRazon(valor);
    setHiloAbierto(null);
  }

  return (
    <div className="adoc-asistente-soporte">
      <PageHeader
        title="Historial del asistente (soporte)"
        meta="Lectura auditada del historial de otra persona: requiere una razón. Sin filas de resultado ni re-ejecución."
      />

      <div className="adoc-asistente-soporte-formulario">
        <SelectorDeSujeto
          personas={personas.data ?? []}
          cargando={personas.isLoading}
          seleccionado={sujetoId}
          onSeleccionar={elegirSujeto}
        />

        <Input
          value={razon}
          onChange={(e) => cambiarRazon(e.target.value)}
          placeholder="¿Por qué necesitás ver este historial?"
          aria-label="Razón del acceso"
        />
      </div>

      {sujetoId && !razonValida && (
        <p className="adoc-asistente-soporte-sin-razon">
          Ingresá una razón para listar o abrir el historial de esta persona.
        </p>
      )}

      {sujetoId && razonValida && (
        <div
          className="adoc-asistente-soporte-lista"
          aria-label="Conversaciones de la persona elegida"
        >
          {conversaciones.isLoading && <p>Buscando conversaciones…</p>}

          {conversaciones.data?.length === 0 && (
            <p className="adoc-asistente-historial-vacio">Esta persona no tiene conversaciones.</p>
          )}

          <ul>
            {conversaciones.data?.map((conversacion) => (
              <li key={conversacion.id}>
                <button
                  type="button"
                  className="adoc-asistente-soporte-abrir"
                  onClick={() => setHiloAbierto(conversacion.id)}
                >
                  {conversacion.titulo}
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {hiloAbierto && detalle.data && <DetalleDeSoporte conversacion={detalle.data} />}
    </div>
  );
}
