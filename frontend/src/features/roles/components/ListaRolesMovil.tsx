import { HerramientasLista, type OpcionOrden } from "../../../shared/ui/HerramientasLista";
import type { GrupoFiltro } from "../../../shared/ui/HojaFiltros";
import { ListaTarjetas, type Tarjeta } from "../../../shared/ui/ListaTarjetas";
import type { OrdenURL } from "../../../shared/navegacion/estadoURL";
import {
  ETIQUETAS_ESTADO,
  ETIQUETAS_TIPO,
  FILTROS_VACIOS,
  filtrarRoles,
  type ColumnaOrdenRol,
  type FiltrosRoles,
} from "../filtrosRoles";
import { ETIQUETAS_SCOPE, SCOPES_ROL, type RolMock } from "../models";
import { InsigniaEstadoRol } from "./insigniasRol";

const ORDENES: OpcionOrden<ColumnaOrdenRol>[] = [
  { columna: "nombre", direccion: "asc", etiqueta: "Nombre (A–Z)" },
  { columna: "nombre", direccion: "desc", etiqueta: "Nombre (Z–A)" },
  { columna: "ambito", direccion: "asc", etiqueta: "Ámbito" },
  { columna: "tipo", direccion: "asc", etiqueta: "Tipo" },
  { columna: "permisos", direccion: "desc", etiqueta: "Más permisos" },
  { columna: "permisos", direccion: "asc", etiqueta: "Menos permisos" },
  { columna: "estado", direccion: "asc", etiqueta: "Estado" },
];

const GRUPOS: GrupoFiltro<FiltrosRoles>[] = [
  {
    clave: "ambito",
    titulo: "Ámbito",
    opciones: SCOPES_ROL.map((s) => ({ valor: s, etiqueta: ETIQUETAS_SCOPE[s] })),
  },
  {
    clave: "tipo",
    titulo: "Tipo",
    opciones: Object.entries(ETIQUETAS_TIPO).map(([valor, etiqueta]) => ({ valor, etiqueta })),
  },
  {
    clave: "estado",
    titulo: "Estado",
    opciones: Object.entries(ETIQUETAS_ESTADO).map(([valor, etiqueta]) => ({ valor, etiqueta })),
  },
  {
    clave: "descripcion",
    titulo: "Descripción",
    tipo: "texto",
    placeholder: "Buscar descripción…",
  },
];

interface ListaRolesMovilProps {
  /** Every role, unfiltered: the sheet counts results over this list. */
  todos: RolMock[];
  /** Filtered and sorted, as the table shows them. */
  roles: RolMock[];
  totalPermisos: number;
  filtros: FiltrosRoles;
  orden: OrdenURL<ColumnaOrdenRol> | null;
  onFiltrosChange: (filtros: FiltrosRoles) => void;
  onOrdenChange: (orden: OrdenURL<ColumnaOrdenRol>) => void;
  onAbrir: (rol: RolMock) => void;
  onCambiarEstado?: (rol: RolMock) => void;
  textoAbrir: string;
}

/** Phone rendering of the Roles table: same rows, filters, sort and row actions. */
export function ListaRolesMovil({
  todos,
  roles,
  totalPermisos,
  filtros,
  orden,
  onFiltrosChange,
  onOrdenChange,
  onAbrir,
  onCambiarEstado,
  textoAbrir,
}: ListaRolesMovilProps) {
  const tarjetas: Tarjeta[] = roles.map((rol) => ({
    id: rol.id,
    nombre: rol.nombre,
    titulo: rol.nombre,
    ruta: `/roles/${rol.id}`,
    etiquetaEnlace: `Ver el rol ${rol.nombre}`,
    meta: `${rol.permisos.length}/${totalPermisos} permisos`,
    linea: [
      ETIQUETAS_SCOPE[rol.scope],
      ETIQUETAS_TIPO[rol.es_sistema ? "sistema" : "personalizado"],
      rol.descripcion,
    ]
      .filter(Boolean)
      .join(" · "),
    insignias: <InsigniaEstadoRol rol={rol} />,
    acciones: [
      { etiqueta: textoAbrir, onSelect: () => onAbrir(rol) },
      ...(onCambiarEstado && !rol.es_sistema
        ? [
            {
              etiqueta: rol.activo ? "Desactivar" : "Activar",
              onSelect: () => onCambiarEstado(rol),
            },
          ]
        : []),
    ],
  }));

  return (
    <>
      <HerramientasLista
        grupos={GRUPOS}
        filtros={filtros}
        vacios={FILTROS_VACIOS}
        onFiltrosChange={onFiltrosChange}
        contar={(f) => filtrarRoles(todos, f).length}
        busqueda={{ clave: "nombre", placeholder: "Buscar rol…" }}
        ordenes={ORDENES}
        orden={orden}
        onOrdenChange={onOrdenChange}
      />
      <ListaTarjetas
        etiqueta="Roles"
        tarjetas={tarjetas}
        vacio="No hay roles que coincidan con los filtros."
        reinicio={JSON.stringify([filtros, orden])}
      />
    </>
  );
}
