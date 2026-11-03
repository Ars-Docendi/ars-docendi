import type { ArticuloAyuda, ContenidoAyuda, ModuloAyuda } from "../tipos";
import { ingresarAlSistema } from "./primeros-pasos/ingresar-al-sistema";
import { recorrerLaPantalla } from "./primeros-pasos/recorrer-la-pantalla";
import { permisosYPantallasVisibles } from "./primeros-pasos/permisos-y-pantallas-visibles";
import { usarElCentroDeAyuda } from "./primeros-pasos/usar-el-centro-de-ayuda";
import { entenderTuPerfil } from "./mi-portal/entender-tu-perfil";
import { completarDatosDeContacto } from "./mi-portal/completar-datos-de-contacto";
import { cargarEducacionYCertificaciones } from "./mi-portal/cargar-educacion-y-certificaciones";
import { cargarExperienciaYProyectos } from "./mi-portal/cargar-experiencia-y-proyectos";
import { elegirHabilidadesEIntereses } from "./mi-portal/elegir-habilidades-e-intereses";
import { subirTuCv } from "./mi-portal/subir-tu-cv";
import { entenderElListadoDeTareas } from "./tareas/entender-el-listado-de-tareas";
import { filtrarYOrdenarTareas } from "./tareas/filtrar-y-ordenar-tareas";
import { abrirUnaTarea } from "./tareas/abrir-una-tarea";
import { cambiarElEstadoDeUnaTarea } from "./tareas/cambiar-el-estado-de-una-tarea";
import { crearUnaTarea } from "./tareas/crear-una-tarea";
import { crearUnProyecto } from "./tareas/crear-un-proyecto";
import { seguirUnProyecto } from "./tareas/seguir-un-proyecto";
import { crearUnPedido } from "./designaciones/crear-un-pedido";
import { adjuntarDocumentacion } from "./designaciones/adjuntar-documentacion";
import { editarUnPedido } from "./designaciones/editar-un-pedido";
import { seguirElEstadoDeUnPedido } from "./designaciones/seguir-el-estado-de-un-pedido";
import { revisarElTablero } from "./designaciones/revisar-el-tablero";
import { resolverUnPedido } from "./designaciones/resolver-un-pedido";
import { descargarElLoteDeFinalizados } from "./designaciones/descargar-el-lote-de-finalizados";
import { gestionarPeriodos } from "./designaciones/gestionar-periodos";
import { administrarPersonas } from "./administracion/administrar-personas";
import { administrarRolesYPermisos } from "./administracion/administrar-roles-y-permisos";
import { leerElDashboardDelSistema } from "./administracion/leer-el-dashboard-del-sistema";
import { consultarLaAuditoria } from "./administracion/consultar-la-auditoria";

/**
 * Registro explícito del contenido de ayuda. Aulas (placeholder) y el Asistente
 * no se documentan mientras no tengan pantallas funcionales en `develop`.
 */
export const MODULOS: readonly ModuloAyuda[] = [
  {
    slug: "primeros-pasos",
    titulo: "Primeros pasos",
    descripcion: "Cómo ingresar, recorrer la pantalla y encontrar ayuda.",
    grupos: ["Empezar", "Moverte por el sistema"],
    permisos: [],
  },
  {
    slug: "mi-portal",
    titulo: "Mi portal",
    descripcion: "Tu perfil, tu trayectoria y tu CV.",
    grupos: ["Tu perfil", "Tu trayectoria", "Tu CV"],
    permisos: ["portal.ver"],
  },
  {
    slug: "tareas",
    titulo: "Tareas",
    descripcion: "Seguir tareas y proyectos, y gestionarlos.",
    grupos: ["Seguir tareas", "Gestionar tareas", "Proyectos"],
    // `tareas.gestionar` cubre los artículos de alta; el test de integridad lo exige.
    permisos: ["tareas.ver", "tareas.gestionar"],
  },
  {
    slug: "designaciones",
    titulo: "Designaciones",
    descripcion: "Pedir, seguir y revisar designaciones, y administrar los períodos.",
    grupos: ["Pedir una designación", "Revisar pedidos", "Períodos"],
    permisos: ["designaciones.gestionar", "designaciones.revisar", "periodos.administrar"],
  },
  {
    slug: "administracion",
    titulo: "Configuración y sistema",
    descripcion: "Personas, roles, estado del sistema y auditoría.",
    grupos: ["Personas", "Roles y permisos", "Sistema"],
    permisos: [
      "personas.ver",
      "personas.ver_ambito",
      "roles.ver",
      "sistema.estado.ver",
      "auditoria.ver",
    ],
  },
];

/**
 * Artículos en orden de declaración. El orden de presentación lo da el grupo
 * (índice en `ModuloAyuda.grupos`) y, dentro del grupo, este orden.
 */
export const ARTICULOS: readonly ArticuloAyuda[] = [
  // Primeros pasos
  ingresarAlSistema,
  recorrerLaPantalla,
  permisosYPantallasVisibles,
  usarElCentroDeAyuda,
  // Mi portal
  entenderTuPerfil,
  completarDatosDeContacto,
  cargarEducacionYCertificaciones,
  cargarExperienciaYProyectos,
  elegirHabilidadesEIntereses,
  subirTuCv,
  // Tareas
  entenderElListadoDeTareas,
  filtrarYOrdenarTareas,
  abrirUnaTarea,
  cambiarElEstadoDeUnaTarea,
  crearUnaTarea,
  crearUnProyecto,
  seguirUnProyecto,
  // Designaciones
  crearUnPedido,
  adjuntarDocumentacion,
  editarUnPedido,
  seguirElEstadoDeUnPedido,
  revisarElTablero,
  resolverUnPedido,
  descargarElLoteDeFinalizados,
  gestionarPeriodos,
  // Configuración y sistema
  administrarPersonas,
  administrarRolesYPermisos,
  leerElDashboardDelSistema,
  consultarLaAuditoria,
];

export const CONTENIDO: ContenidoAyuda = { modulos: MODULOS, articulos: ARTICULOS };
