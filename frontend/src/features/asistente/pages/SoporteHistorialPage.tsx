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

/** Lectura de soporte del historial AJENO (asistente-acceso-de-soporte-al-historial). */
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
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  const detalle = useQuery({
    queryKey: ["asistente", "soporte-historial", "leer", sujetoId, hiloAbierto, razonConfirmada],
    queryFn: () => leerHistorialDeSoporte(sujetoId ?? "", hiloAbierto ?? "", razonConfirmada),
    enabled: Boolean(sujetoId) && Boolean(hiloAbierto) && razonValida,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  // La razón se confirma para UNA persona: cambiar de persona pide volver a
  // confirmarla, o se leería el historial de otra con una razón ajena.
  function elegirSujeto(id: string) {
    setSujetoId(id);
    setRazonConfirmada("");
    setHiloAbierto(null);
  }

  function cambiarRazon(valor: string) {
    setRazon(valor);
    setRazonConfirmada("");
    setHiloAbierto(null);
  }

  // Mientras una lectura está en curso no se acepta otra confirmación: el refetch reutilizaría esa
  // misma solicitud y la confirmación quedaría sin su lectura (y sin su fila de auditoría).
  const leyendo = conversaciones.isFetching || detalle.isFetching;

  function confirmarRazon(evento: FormEvent) {
    evento.preventDefault();
    if (leyendo) return;
    const confirmada = razon.trim();
    setRazonConfirmada(confirmada);
    // Misma razón, misma clave: sin pedirla de nuevo, volver a confirmar no
    // leería nada y esa confirmación quedaría sin su fila de auditoría.
    if (confirmada === razonConfirmada) {
      void conversaciones.refetch();
      if (hiloAbierto) void detalle.refetch();
    }
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
          <Button type="submit" variant="secondary" disabled={razon.trim().length === 0 || leyendo}>
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
