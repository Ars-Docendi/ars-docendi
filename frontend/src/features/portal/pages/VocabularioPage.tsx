import { useState } from "react";
import { Button, Field, InlineAlert, Input, Select, Table } from "@ars-docendi/ui";
import { mensajeProblema } from "../../../shared/api/problemDetails";
import { Migas } from "../../../shared/navegacion/Migas";
import { ModalDeFormulario } from "../../../shared/ui/ModalDeFormulario";
import { PageHeader } from "../../../shared/ui/PageHeader";
import type { TerminoVocabulario } from "../api/vocabularioApi";
import { useVocabularioAdmin } from "../hooks/useVocabularioAdmin";

type Accion = { tipo: "renombrar" | "fusionar"; termino: TerminoVocabulario } | null;

function estado(t: TerminoVocabulario): string {
  if (t.canonico) return `Fusionado en ${t.canonico}`;
  return t.sugerido ? "Sugerido" : "Incorporado";
}

/**
 * Vocabulario de habilidades e intereses (`portal.vocabulario.administrar`). Un término sugerido
 * se ofrece recién al incorporarlo; fusionar pasa los usos de una variante a otro término.
 */
export function VocabularioPage() {
  const remoto = useVocabularioAdmin();
  const terminos = remoto.consulta.data ?? [];
  const [soloSugeridos, setSoloSugeridos] = useState(false);
  const [accion, setAccion] = useState<Accion>(null);
  const visibles = soloSugeridos ? terminos.filter((t) => t.sugerido && !t.canonico) : terminos;

  function abrir(tipo: "renombrar" | "fusionar", termino: TerminoVocabulario) {
    remoto.renombrar.reset();
    remoto.fusionar.reset();
    setAccion({ tipo, termino });
  }

  return (
    <>
      <Migas items={[{ label: "Inicio", href: "/" }, { label: "Vocabulario de experticia" }]} />
      <PageHeader
        title="Vocabulario de experticia"
        meta={`${terminos.filter((t) => t.sugerido && !t.canonico).length} sugeridos`}
      />
      <label className="adoc-filtro-encabezado-opcion">
        <input
          type="checkbox"
          checked={soloSugeridos}
          onChange={(e) => setSoloSugeridos(e.target.checked)}
        />
        Sólo sugeridos
      </label>
      {remoto.consulta.isLoading && <p role="status">Cargando vocabulario…</p>}
      {remoto.consulta.isError && (
        <InlineAlert severity="danger" title="No se pudo cargar el vocabulario">
          <Button variant="secondary" size="sm" onClick={() => remoto.consulta.refetch()}>
            Reintentar
          </Button>
        </InlineAlert>
      )}
      {remoto.incorporar.isError && (
        <InlineAlert severity="danger" title="No se pudo incorporar el término">
          {mensajeProblema(remoto.incorporar.error, "Intentá de nuevo.")}
        </InlineAlert>
      )}

      {remoto.consulta.isSuccess && (
        <Table>
          <Table.Root>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Término</Table.HeaderCell>
                <Table.HeaderCell>Estado</Table.HeaderCell>
                <Table.HeaderCell>Docentes</Table.HeaderCell>
                <Table.HeaderCell>Acciones</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {visibles.length === 0 ? (
                <Table.Row>
                  <Table.Cell
                    colSpan={4}
                    style={{ textAlign: "center", color: "var(--color-text-tertiary)" }}
                  >
                    No hay términos para mostrar.
                  </Table.Cell>
                </Table.Row>
              ) : (
                visibles.map((t) => (
                  <Table.Row key={t.id}>
                    <Table.Cell>{t.termino}</Table.Cell>
                    <Table.Cell>{estado(t)}</Table.Cell>
                    <Table.Cell>{t.docentes}</Table.Cell>
                    <Table.Cell>
                      {!t.canonico && (
                        <div className="adoc-acciones-fila">
                          {t.sugerido && (
                            <Button
                              variant="ghost"
                              size="sm"
                              disabled={remoto.incorporar.isPending}
                              onClick={() => remoto.incorporar.mutate(t.id)}
                            >
                              Incorporar
                            </Button>
                          )}
                          <Button variant="ghost" size="sm" onClick={() => abrir("renombrar", t)}>
                            Renombrar
                          </Button>
                          <Button variant="ghost" size="sm" onClick={() => abrir("fusionar", t)}>
                            Fusionar
                          </Button>
                        </div>
                      )}
                    </Table.Cell>
                  </Table.Row>
                ))
              )}
            </Table.Body>
          </Table.Root>
        </Table>
      )}

      {accion?.tipo === "renombrar" && (
        <ModalRenombrar
          termino={accion.termino}
          pendiente={remoto.renombrar.isPending}
          error={
            remoto.renombrar.isError
              ? mensajeProblema(remoto.renombrar.error, "Intentá de nuevo.")
              : undefined
          }
          onCerrar={() => setAccion(null)}
          onConfirmar={(nuevo) =>
            remoto.renombrar.mutate(
              { id: accion.termino.id, termino: nuevo },
              { onSuccess: () => setAccion(null) },
            )
          }
        />
      )}
      {accion?.tipo === "fusionar" && (
        <ModalFusionar
          termino={accion.termino}
          // Destinos válidos: términos vigentes distintos del origen (el backend también lo valida).
          destinos={terminos.filter((t) => !t.canonico && t.id !== accion.termino.id)}
          pendiente={remoto.fusionar.isPending}
          error={
            remoto.fusionar.isError
              ? mensajeProblema(remoto.fusionar.error, "Intentá de nuevo.")
              : undefined
          }
          onCerrar={() => setAccion(null)}
          onConfirmar={(destinoId) =>
            remoto.fusionar.mutate(
              { id: accion.termino.id, destinoId },
              { onSuccess: () => setAccion(null) },
            )
          }
        />
      )}
    </>
  );
}

interface ModalTerminoProps {
  termino: TerminoVocabulario;
  pendiente: boolean;
  error?: string;
  onCerrar: () => void;
}

function ModalRenombrar({
  termino,
  pendiente,
  error,
  onCerrar,
  onConfirmar,
}: ModalTerminoProps & { onConfirmar: (nuevo: string) => void }) {
  const [nuevo, setNuevo] = useState(termino.termino);
  const [intentado, setIntentado] = useState(false);
  return (
    <ModalDeFormulario
      titulo="Renombrar término"
      tamano="confirmacion"
      onCerrar={onCerrar}
      pendiente={pendiente}
      sucio={nuevo !== termino.termino}
      alEnviar={() => {
        setIntentado(true);
        if (nuevo.trim()) onConfirmar(nuevo.trim());
      }}
      pie={
        <>
          <Button variant="secondary" onClick={onCerrar} disabled={pendiente}>
            Volver
          </Button>
          <Button type="submit" variant="primary" loading={pendiente}>
            Renombrar
          </Button>
        </>
      }
    >
      {error && (
        <InlineAlert severity="danger" title="No se pudo renombrar">
          {error}
        </InlineAlert>
      )}
      <Field
        label="Término"
        required
        error={intentado && !nuevo.trim() ? "Indicá el término." : undefined}
      >
        <Input
          id="vocabulario-renombrar"
          value={nuevo}
          onChange={(e) => setNuevo(e.target.value)}
        />
      </Field>
    </ModalDeFormulario>
  );
}

function ModalFusionar({
  termino,
  destinos,
  pendiente,
  error,
  onCerrar,
  onConfirmar,
}: ModalTerminoProps & {
  destinos: TerminoVocabulario[];
  onConfirmar: (destinoId: string) => void;
}) {
  const [destinoId, setDestinoId] = useState("");
  const [intentado, setIntentado] = useState(false);
  return (
    <ModalDeFormulario
      titulo={`Fusionar "${termino.termino}"`}
      tamano="confirmacion"
      onCerrar={onCerrar}
      pendiente={pendiente}
      sucio={destinoId !== ""}
      alEnviar={() => {
        setIntentado(true);
        if (destinoId) onConfirmar(destinoId);
      }}
      pie={
        <>
          <Button variant="secondary" onClick={onCerrar} disabled={pendiente}>
            Volver
          </Button>
          <Button type="submit" variant="primary" loading={pendiente}>
            Fusionar
          </Button>
        </>
      }
    >
      <p>
        {`Los ${termino.docentes} docentes que usan "${termino.termino}" pasan a tener el término elegido. "${termino.termino}" deja de ofrecerse. No se puede deshacer.`}
      </p>
      {error && (
        <InlineAlert severity="danger" title="No se pudo fusionar">
          {error}
        </InlineAlert>
      )}
      <Field
        label="Fusionar en"
        required
        error={intentado && !destinoId ? "Elegí el término destino." : undefined}
      >
        <Select
          id="vocabulario-destino"
          value={destinoId}
          onChange={(e) => setDestinoId(e.target.value)}
        >
          <option value="">Seleccioná un término…</option>
          {destinos.map((d) => (
            <option key={d.id} value={d.id}>
              {d.termino}
            </option>
          ))}
        </Select>
      </Field>
    </ModalDeFormulario>
  );
}
