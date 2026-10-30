import type { ArticuloAyuda } from "../../tipos";

export const administrarRolesYPermisos: ArticuloAyuda = {
  slug: "administrar-roles-y-permisos",
  moduloSlug: "administracion",
  grupo: "Roles y permisos",
  titulo: "Cómo administrar roles y permisos",
  resumen: "Crear roles, asignar permisos y qué cambia al guardar.",
  introduccion:
    "Un rol agrupa permisos, y los permisos deciden qué pantallas y acciones tiene cada persona. En Roles ves los roles del sistema, creás roles propios y marcás qué permisos tiene cada uno.",
  permisos: ["roles.ver"],
  palabrasClave: [
    "roles",
    "permisos",
    "nuevo rol",
    "crear rol",
    "editar rol",
    "desactivar rol",
    "activar rol",
    "buscar permiso",
    "módulo",
    "ámbito",
    "membresía",
    "guardar cambios",
    "rol de sistema",
    "rol personalizado",
    "acceso",
  ],
  pasos: [
    {
      titulo: "Abrí Roles",
      etiquetaIndice: "Abrir Roles",
      cuerpo:
        "En el menú, grupo Configuración, elegí Roles. La grilla muestra los roles con su Nombre, Descripción, Ámbito, Tipo (Sistema o Personalizado), cuántos permisos tiene cada uno sobre el total («7/19») y su Estado (Activo o Inactivo). Podés filtrar desde el encabezado de cada columna y ordenar por Nombre, Ámbito, Tipo, Permisos o Estado.",
      captura: {
        src: "/ayuda/administracion/administrar-roles-y-permisos-paso-1.webp",
        alt: "Grilla de roles con nombre, ámbito, tipo, permisos, estado y las acciones Editar y Activar o Desactivar.",
      },
      nota: {
        tipo: "info",
        titulo: "Qué ves según tus permisos",
        cuerpo:
          "Con roles.ver solo mirás: al abrir un rol las casillas aparecen bloqueadas. Nuevo rol, la edición de nombre, descripción y ámbito y Activar/Desactivar requieren roles.administrar; modificar las casillas de permisos requiere roles.gestionar_membresia.",
      },
    },
    {
      titulo: "Creá un rol",
      etiquetaIndice: "Nuevo rol",
      cuerpo:
        "Pulsá Nuevo rol. Completá el Nombre (obligatorio y único entre los roles activos; si ya existe, avisa «Ya existe un rol con ese nombre.»), una Descripción opcional y el Ámbito: Global, Materia o Carrera. El ámbito indica dónde rige el rol cuando se lo asigna a una persona. Debajo están los permisos agrupados por módulo, todos destildados: marcá los que corresponda y pulsá Crear rol.",
      captura: {
        src: "/ayuda/administracion/administrar-roles-y-permisos-paso-2.webp",
        alt: "Pantalla Nuevo rol con los campos Nombre, Descripción y Ámbito y su ayuda.",
      },
      nota: {
        tipo: "importante",
        titulo: "Alcance de un rol personalizado",
        cuerpo:
          "Un rol propio puede agrupar permisos, pero no habilita a aceptar, rechazar ni devolver pedidos en el circuito de aprobación de designaciones: esa facultad es de los roles del circuito.",
      },
    },
    {
      titulo: "Marcá los permisos por módulo y guardá",
      etiquetaIndice: "Permisos",
      cuerpo:
        "Hacé clic en un rol de la grilla para abrirlo. Cada módulo (Designaciones, Tareas, Personas…) muestra cuántos permisos tiene marcados sobre su total; desplegalo para ver sus permisos. Con el buscador encontrás un permiso por su nombre o descripción: quedan solo los módulos con coincidencias, ya desplegados. La casilla del módulo marca o desmarca todos sus permisos de una vez, y aparece a medias cuando solo algunos están marcados. Los cambios no se aplican hasta que pulses Guardar cambios; Cancelar los descarta. Esto vale también para los roles de sistema.",
      captura: {
        src: "/ayuda/administracion/administrar-roles-y-permisos-paso-3.webp",
        alt: "Permisos agrupados por módulo, con el buscador y un módulo marcado a medias.",
      },
    },
    {
      titulo: "Editá, desactivá o reactivá un rol propio",
      etiquetaIndice: "Editar y desactivar",
      cuerpo:
        "Al abrir un rol personalizado podés cambiar Nombre, Descripción y Ámbito junto con sus permisos, y Guardar cambios confirma todo. No se puede cambiar el ámbito si el rol tiene asignaciones vigentes. Desactivar, en la fila del rol (solo en roles personalizados), pide confirmación: el rol queda inactivo y las personas que lo tienen asignado pierden sus permisos, pero las asignaciones se conservan. Activar lo vuelve a habilitar, salvo que otro rol activo ya use su nombre. Los roles de sistema no se pueden renombrar ni desactivar, solo ajustar sus permisos.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Los cambios quedan guardados y los toma cada persona cuando se revalida su sesión: puede hacer falta que actualice la página o vuelva a ingresar. Al quitar un permiso, la pantalla correspondiente deja de aparecer en su menú. Los cambios quedan en Registros de auditoría.",
  },
  preguntas: [
    {
      pregunta: "Cambié permisos y la persona sigue sin ver la pantalla.",
      respuesta:
        "Los permisos se leen al validar la sesión. Pedile que actualice la página o cierre sesión y vuelva a ingresar.",
    },
    {
      pregunta: "Otra persona modificó el rol mientras yo editaba.",
      respuesta:
        "La pantalla avisa del conflicto, no guarda tus cambios y te deja recargar el estado actual antes de reintentar.",
    },
    {
      pregunta: "¿Por qué no puedo cambiar el ámbito de un rol?",
      respuesta:
        "Porque ya hay personas con ese rol asignado. Revisá sus roles en Personas antes de cambiar el ámbito.",
    },
  ],
  relacionados: [
    "primeros-pasos/permisos-y-pantallas-visibles",
    "administracion/administrar-personas",
    "administracion/consultar-la-auditoria",
  ],
};
