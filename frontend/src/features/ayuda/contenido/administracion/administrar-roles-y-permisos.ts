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
    "eliminar rol",
    "rol base",
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
        "En el menú, grupo Configuración, elegí Roles. A la izquierda está la lista de roles activos, con un buscador («Nombre o descripción…»); a la derecha, el panel de permisos del rol seleccionado. Cada permiso tiene su casilla, su nombre y su descripción, y arriba se cuenta cuántos tiene el rol («3 de 24»).",
      captura: {
        src: "/ayuda/administracion/administrar-roles-y-permisos-paso-1.webp",
        alt: "Pantalla Roles con la lista y el panel de permisos.",
      },
      nota: {
        tipo: "info",
        titulo: "Qué ves según tus permisos",
        cuerpo:
          "Con roles.ver solo mirás: las casillas aparecen bloqueadas y el panel dice «Sólo lectura.». Los botones Nuevo rol, Editar y Eliminar requieren roles.administrar, y modificar las casillas requiere roles.gestionar_membresia.",
      },
    },
    {
      titulo: "Creá un rol",
      etiquetaIndice: "Nuevo rol",
      cuerpo:
        "Pulsá Nuevo rol. Completá el Nombre (obligatorio y único entre los roles activos; si ya existe, el formulario avisa «Ya existe un rol con ese nombre.»), una Descripción opcional y el Ámbito: Global, Materia o Carrera. El ámbito indica dónde rige el rol cuando se lo asigna a una persona.",
      captura: {
        src: "/ayuda/administracion/administrar-roles-y-permisos-paso-2.webp",
        alt: "Ventana Nuevo rol con ámbito y rol base.",
      },
    },
    {
      titulo: "Partí de un rol existente (opcional)",
      etiquetaIndice: "Rol base",
      cuerpo:
        "Marcá «Usar un rol existente como base (hereda sus permisos)» y elegí el Rol base: el rol nuevo arranca con los mismos permisos y después los ajustás. Pulsá Crear rol; el rol nuevo queda seleccionado.",
      nota: {
        tipo: "importante",
        titulo: "Alcance de un rol personalizado",
        cuerpo:
          "Un rol propio puede agrupar permisos, pero no habilita a aceptar, rechazar ni devolver pedidos en el circuito de aprobación de designaciones: esa facultad es de los roles del circuito.",
      },
    },
    {
      titulo: "Marcá los permisos y guardá",
      etiquetaIndice: "Permisos",
      cuerpo:
        "Seleccioná un rol en la lista y tildá o destildá sus permisos. Los cambios no se aplican hasta que pulses Guardar cambios («Los cambios se aplican al guardar.»). Si seleccionás otro rol antes de guardar, los cambios pendientes se descartan. Esto vale también para los roles de sistema.",
      captura: {
        src: "/ayuda/administracion/administrar-roles-y-permisos-paso-4.webp",
        alt: "Panel de permisos con casillas marcadas y el botón Guardar cambios.",
      },
    },
    {
      titulo: "Editá o eliminá un rol propio",
      etiquetaIndice: "Editar y eliminar",
      cuerpo:
        "Editar abre «Editar rol» con Nombre, Descripción y Ámbito; Guardar confirma. No se puede cambiar el ámbito si el rol tiene asignaciones vigentes. Eliminar (solo en roles personalizados) pide confirmación en «Eliminar rol»: el rol queda inactivo, pero sus asignaciones históricas y sus permisos se conservan. Los roles de sistema no se pueden renombrar ni eliminar, solo ajustar sus permisos.",
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
        "Porque ya hay personas con ese rol asignado. Revisá sus membresías en Usuarios antes de cambiar el ámbito.",
    },
  ],
  relacionados: [
    "primeros-pasos/permisos-y-pantallas-visibles",
    "administracion/administrar-usuarios",
    "administracion/consultar-la-auditoria",
  ],
};
