import { HerramientasLista, type OpcionOrden } from "../../../shared/ui/HerramientasLista";
import type { GrupoFiltro } from "../../../shared/ui/HojaFiltros";
import { ListaTarjetas, type Tarjeta } from "../../../shared/ui/ListaTarjetas";
import type { OrdenURL } from "../../../shared/navegacion/estadoURL";
import type { Persona } from "../api/personasApi";
import {
  FILTROS_VACIOS,
  ETIQUETAS_ESTADO,
  SIN_ROL,
  filtrarPersonas,
  nombreCompleto,
  type ColumnaOrden,
  type EstadoPersona,
  type FiltrosPersonas,
} from "../filtrosPersonas";
import { InsigniaEstado, InsigniasAsignaciones, InsigniasRoles } from "./insigniasPersona";

const ORDENES: OpcionOrden<ColumnaOrden>[] = [
  { columna: "nombre", direccion: "asc", etiqueta: "Nombre (A–Z)" },
  { columna: "nombre", direccion: "desc", etiqueta: "Nombre (Z–A)" },
  { columna: "documento", direccion: "asc", etiqueta: "Documento" },
  { columna: "legajo", direccion: "asc", etiqueta: "Legajo" },
  { columna: "estado", direccion: "asc", etiqueta: "Estado" },
];

interface ListaPersonasMovilProps {
  /** Every person, unfiltered: the sheet counts results over this list. */
  todas: Persona[];
  /** Filtered and sorted, as the table shows them. */
  personas: Persona[];
  opcionesRol: string[];
  filtros: FiltrosPersonas;
  orden: OrdenURL<ColumnaOrden> | null;
  onFiltrosChange: (filtros: FiltrosPersonas) => void;
  onOrdenChange: (orden: OrdenURL<ColumnaOrden>) => void;
  onAbrir: (persona: Persona) => void;
  onCambiarEstado?: (persona: Persona) => void;
  textoAbrir: string;
}

/** Phone rendering of the Personas table: same rows, filters, sort and row actions. */
export function ListaPersonasMovil({
  todas,
  personas,
  opcionesRol,
  filtros,
  orden,
  onFiltrosChange,
  onOrdenChange,
  onAbrir,
  onCambiarEstado,
  textoAbrir,
}: ListaPersonasMovilProps) {
  const grupos: GrupoFiltro<FiltrosPersonas>[] = [
    {
      clave: "rol",
      titulo: "Rol",
      opciones: opcionesRol.map((o) => ({ valor: o, etiqueta: o === SIN_ROL ? "Sin rol" : o })),
    },
    {
      clave: "estado",
      titulo: "Estado",
      opciones: (Object.keys(ETIQUETAS_ESTADO) as EstadoPersona[]).map((e) => ({
        valor: e,
        etiqueta: ETIQUETAS_ESTADO[e],
      })),
    },
    { clave: "documento", titulo: "Documento", tipo: "texto", placeholder: "Buscar documento…" },
    { clave: "legajo", titulo: "Legajo", tipo: "texto", placeholder: "Buscar legajo…" },
    {
      clave: "asignaciones",
      titulo: "Asignaciones",
      tipo: "texto",
      placeholder: "Buscar materia, código o cargo…",
    },
  ];

  const tarjetas: Tarjeta[] = personas.map((persona) => ({
    id: persona.personaId,
    nombre: nombreCompleto(persona),
    titulo: nombreCompleto(persona),
    ruta: `/personas/${persona.personaId}`,
    etiquetaEnlace: `Ver a ${nombreCompleto(persona)}`,
    meta: `${persona.documento} · Legajo ${persona.legajo ?? "—"}`,
    insignias: (
      <>
        <InsigniasRoles persona={persona} />
        <InsigniasAsignaciones persona={persona} />
        <InsigniaEstado persona={persona} />
      </>
    ),
    acciones: [
      { etiqueta: textoAbrir, onSelect: () => onAbrir(persona) },
      ...(onCambiarEstado && persona.tieneCuenta
        ? [
            {
              etiqueta: persona.activo ? "Desactivar" : "Activar",
              onSelect: () => onCambiarEstado(persona),
            },
          ]
        : []),
    ],
  }));

  return (
    <>
      <HerramientasLista
        grupos={grupos}
        filtros={filtros}
        vacios={FILTROS_VACIOS}
        onFiltrosChange={onFiltrosChange}
        contar={(f) => filtrarPersonas(todas, f).length}
        busqueda={{ clave: "nombre", placeholder: "Buscar apellido o nombre…" }}
        ordenes={ORDENES}
        orden={orden}
        onOrdenChange={onOrdenChange}
      />
      <ListaTarjetas
        etiqueta="Personas"
        tarjetas={tarjetas}
        vacio="No hay personas que coincidan con los filtros."
        reinicio={JSON.stringify([filtros, orden])}
      />
    </>
  );
}
