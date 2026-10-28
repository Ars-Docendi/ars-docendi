import { useMemo, useState, type FormEvent } from "react";
import { Button, Input, Modal, Table } from "@ars-docendi/ui";
import {
  FILTROS_MATERIA_VACIOS,
  buscarMaterias,
  esSeleccionActual,
  hayFiltros,
  totalOpciones,
  type FiltrosMateria,
  type ModoBuscadorMaterias,
  type OpcionMateriaCarrera,
  type ResultadoMateria,
  type SeleccionMateria,
} from "./busquedaMaterias";
import { propsFilaClickeable } from "./filaClickeable";
import "./BuscadorMaterias.css";
import "./modalEstirable.css";

interface BuscadorMateriasProps {
  open: boolean;
  modo: ModoBuscadorMaterias;
  /** Pares materia–carrera del catálogo. */
  opciones: OpcionMateriaCarrera[];
  seleccion?: SeleccionMateria | null;
  /** Desde dónde se abrió, debajo del título (p. ej. "Asignación 2 · elegí materia y carrera"). */
  contexto?: string;
  /** Motivo por el que una fila no se puede elegir; `undefined` si es elegible. */
  motivoNoElegible?: (resultado: ResultadoMateria) => string | undefined;
  onSeleccionar: (resultado: ResultadoMateria) => void;
  onCerrar: () => void;
}

const CAMPOS: { campo: keyof FiltrosMateria; etiqueta: string; placeholder: string }[] = [
  { campo: "materiaCodigo", etiqueta: "Código materia", placeholder: "01023" },
  { campo: "materiaNombre", etiqueta: "Nombre materia", placeholder: "Análisis, física…" },
  { campo: "carreraCodigo", etiqueta: "Código carrera", placeholder: "201" },
  { campo: "carreraNombre", etiqueta: "Nombre carrera", placeholder: "Informática, civil…" },
];

/**
 * Buscador emergente de materias, con el formato del buscador de Responsable de Tareas. El
 * catálogo ya está en el cliente, así que filtra localmente; los filtros se aplican con
 * "Buscar" o Enter. Las opciones no elegibles se ven con su motivo en vez de ocultarse.
 */
export function BuscadorMaterias({
  open,
  modo,
  opciones,
  seleccion,
  contexto,
  motivoNoElegible,
  onSeleccionar,
  onCerrar,
}: BuscadorMateriasProps) {
  const [borrador, setBorrador] = useState<FiltrosMateria>(FILTROS_MATERIA_VACIOS);
  const [filtros, setFiltros] = useState<FiltrosMateria>(FILTROS_MATERIA_VACIOS);

  const resultados = useMemo(
    () => buscarMaterias(opciones, filtros, modo),
    [opciones, filtros, modo],
  );
  const total = useMemo(() => totalOpciones(opciones, modo), [opciones, modo]);
  const columnas = modo === "par" ? 4 : 3;

  function cerrar() {
    setBorrador(FILTROS_MATERIA_VACIOS);
    setFiltros(FILTROS_MATERIA_VACIOS);
    onCerrar();
  }

  function buscar(evento: FormEvent) {
    evento.preventDefault();
    setFiltros(borrador);
  }

  function limpiar() {
    setBorrador(FILTROS_MATERIA_VACIOS);
    setFiltros(FILTROS_MATERIA_VACIOS);
  }

  return (
    <Modal
      open={open}
      onOpenChange={(siguiente) => {
        if (!siguiente) cerrar();
      }}
      className="adoc-modal--estirable adoc-buscador-materias"
      title={
        <span className="adoc-buscador-materias-titulo">
          Buscar materia
          {contexto && <span className="adoc-buscador-materias-contexto">{contexto}</span>}
        </span>
      }
      footer={
        <Button variant="secondary" onClick={cerrar}>
          Cancelar
        </Button>
      }
    >
      <form className="adoc-buscador-materias-form" onSubmit={buscar}>
        {CAMPOS.map(({ campo, etiqueta, placeholder }, i) => (
          <label key={campo} className={`adoc-buscador-materias-filtro is-${campo}`}>
            <span>{etiqueta}</span>
            <Input
              value={borrador[campo]}
              onChange={(e) => setBorrador({ ...borrador, [campo]: e.target.value })}
              placeholder={placeholder}
              className={campo.endsWith("Codigo") ? "adoc-mono" : undefined}
              autoFocus={i === 0}
            />
          </label>
        ))}
        <Button type="submit" variant="primary">
          Buscar
        </Button>
      </form>

      <div className="adoc-buscador-materias-estado">
        <span aria-live="polite">
          {resultados.length === total
            ? `${total} materias`
            : `${resultados.length} de ${total} materias`}
        </span>
        {(hayFiltros(borrador) || hayFiltros(filtros)) && (
          <Button variant="ghost" size="sm" onClick={limpiar}>
            Limpiar filtros
          </Button>
        )}
      </div>

      <div className="adoc-buscador-materias-resultados">
        <Table>
          <Table.Root>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell className="is-codigo">Código</Table.HeaderCell>
                <Table.HeaderCell>Materia</Table.HeaderCell>
                {modo === "par" ? (
                  <>
                    <Table.HeaderCell className="is-codigo">Carrera</Table.HeaderCell>
                    <Table.HeaderCell>Nombre carrera</Table.HeaderCell>
                  </>
                ) : (
                  <Table.HeaderCell>Se dicta en</Table.HeaderCell>
                )}
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {resultados.length === 0 ? (
                <Table.Row>
                  <Table.Cell colSpan={columnas} className="empty">
                    No hay materias que coincidan con la búsqueda.
                  </Table.Cell>
                </Table.Row>
              ) : (
                resultados.map((resultado) => {
                  const motivo = motivoNoElegible?.(resultado);
                  const elegir = motivo
                    ? undefined
                    : () => {
                        onSeleccionar(resultado);
                        cerrar();
                      };
                  return (
                    <Table.Row
                      key={resultado.clave}
                      selected={esSeleccionActual(resultado, seleccion)}
                      aria-disabled={motivo ? true : undefined}
                      title={motivo}
                      {...propsFilaClickeable(elegir)}
                      className={
                        motivo ? "adoc-buscador-materias-no-elegible" : "adoc-fila-clickeable"
                      }
                    >
                      <Table.Cell className="adoc-mono">{resultado.materiaCodigo}</Table.Cell>
                      <Table.Cell>
                        {resultado.materiaNombre}
                        {motivo && <span className="adoc-buscador-materias-motivo">{motivo}</span>}
                      </Table.Cell>
                      {resultado.carrera ? (
                        <>
                          <Table.Cell className="adoc-mono">{resultado.carrera.codigo}</Table.Cell>
                          <Table.Cell>{resultado.carrera.nombre}</Table.Cell>
                        </>
                      ) : (
                        <Table.Cell className="adoc-buscador-materias-carreras">
                          {resultado.carreras
                            .map((carrera) => `${carrera.codigo} · ${carrera.nombre}`)
                            .join("; ")}
                        </Table.Cell>
                      )}
                    </Table.Row>
                  );
                })
              )}
            </Table.Body>
          </Table.Root>
        </Table>
      </div>
    </Modal>
  );
}
