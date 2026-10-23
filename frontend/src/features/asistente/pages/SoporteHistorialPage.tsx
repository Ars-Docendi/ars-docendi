import { useState, type FormEvent } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, Input } from "@ars-docendi/ui";

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
 *
 * LA RAZÓN SE CONFIRMA, NO SE TIPEA: cada lectura queda auditada con su
 * razón, así que las consultas dependen de la razón confirmada con «Ver
 * historial» y no de cada tecla. Si no, escribir «porque falla» dejaba en la
 * auditoría una lectura con «p», otra con «po», y así hasta el final.
 */
export function SoporteHistorialPage() {
  const [sujetoId, setSujetoId] = useState<string | null>(null);
  const [razon, setRazon] = useState("");
  const [razonConfirmada, setRazonConfirmada] = useState("");
  const [hiloAbierto, setHiloAbierto] = useState<string | null>(null);
  const razonValida = razonConfirmada.length > 0;

  const personas = useQuery({
    queryKey: ["asistente", "soporte-historial", "personas"],
    queryFn: buscarPersonasParaSoporte,
  });

  const conversaciones = useQuery({
    queryKey: ["asistente", "soporte-historial", "listar", sujetoId, razonConfirmada],
    queryFn: () => listarHistorialDeSoporte(sujetoId ?? "", razonConfirmada),
    enabled: Boolean(sujetoId) && razonValida,
  });

  const detalle = useQuery({
    queryKey: ["asistente", "soporte-historial", "leer", sujetoId, hiloAbierto, razonConfirmada],
    queryFn: () => leerHistorialDeSoporte(sujetoId ?? "", hiloAbierto ?? "", razonConfirmada),
    enabled: Boolean(sujetoId) && Boolean(hiloAbierto) && razonValida,
  });

  function elegirSujeto(id: string) {
    setSujetoId(id);
    setHiloAbierto(null);
  }

  function cambiarRazon(valor: string) {
    setRazon(valor);
    setRazonConfirmada("");
    setHiloAbierto(null);
  }

  function confirmarRazon(evento: FormEvent) {
    evento.preventDefault();
    setRazonConfirmada(razon.trim());
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

        <form className="adoc-asistente-soporte-razon" onSubmit={confirmarRazon}>
          <Input
            value={razon}
            onChange={(e) => cambiarRazon(e.target.value)}
            placeholder="¿Por qué necesitás ver este historial?"
            aria-label="Razón del acceso"
          />
          <Button type="submit" variant="secondary" disabled={razon.trim().length === 0}>
            Ver historial
          </Button>
        </form>
      </div>

      {sujetoId && !razonValida && (
        <p className="adoc-asistente-soporte-sin-razon">
          Ingresá una razón y elegí «Ver historial» para listar o abrir el historial de esta
          persona.
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
                  {conversacion.archivada && (
                    <span className="adoc-asistente-soporte-marca">Archivada</span>
                  )}
                  {conversacion.pendienteDeBorrado && (
                    <span className="adoc-asistente-soporte-marca">Pendiente de borrado</span>
                  )}
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
