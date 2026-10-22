import type { ArticuloAyuda } from "../../tipos";

export const administrarUsuarios: ArticuloAyuda = {
  slug: "administrar-usuarios",
  moduloSlug: "administracion",
  grupo: "Personas",
  titulo: "Cómo administrar usuarios",
  resumen: "Alta, edición, roles por ámbito, activación y desactivación de usuarios.",
  introduccion:
    "Usuarios lista a las personas que tienen cuenta en el sistema, con sus roles y su estado. Desde acá se da de alta a una persona, se corrigen sus datos y sus roles, y se activa o desactiva su acceso.",
  permisos: ["usuarios.ver"],
  palabrasClave: [
    "usuarios",
    "alta de usuario",
    "nuevo usuario",
    "editar usuario",
    "activar",
    "desactivar",
    "inactivo",
    "roles",
    "membresía",
    "ámbito",
    "upn",
    "email institucional",
    "legajo",
    "dni",
    "acceso",
  ],
  pasos: [
    {
      titulo: "Abrí Usuarios",
      etiquetaIndice: "Abrir Usuarios",
      cuerpo:
        "En el menú, grupo Configuración, elegí Usuarios. El título es «Administración de usuarios» e indica cuántos hay y cuántos están activos. La tabla muestra Nombre (Apellido, Nombre), Documento, Legajo, Email, Roles, Perfil docente, Estado y Acciones.",
      captura: {
        src: "/ayuda/administracion/administrar-usuarios-paso-1.webp",
        alt: "Tabla de Usuarios con roles y estados.",
      },
    },
    {
      titulo: "Buscá y ordená",
      etiquetaIndice: "Buscar y ordenar",
      cuerpo:
        "Cada encabezado tiene un filtro: texto para Nombre, Documento, Legajo y Email, y casillas para Roles, Perfil docente y Estado (Activo o Inactivo). Hacé clic en el nombre de Nombre, Documento, Legajo o Email para ordenar: ascendente, descendente y sin orden. Los filtros solo achican lo que ves.",
    },
    {
      titulo: "Creá un usuario",
      etiquetaIndice: "Nuevo usuario",
      cuerpo:
        "Pulsá Nuevo usuario. Son obligatorios Nombre, Apellido, Documento (DNI), Legajo, Fecha de nacimiento y UPN / Email institucional (por ejemplo nombre@unlam.edu.ar); CUIL y Teléfono son opcionales. Si la UPN ya existe, el formulario avisa «Ya existe un usuario con esa UPN.» y no deja crear.",
      captura: {
        src: "/ayuda/administracion/administrar-usuarios-paso-3.webp",
        alt: "Ventana Nuevo usuario con los datos personales.",
      },
    },
    {
      titulo: "Asigná roles con su ámbito",
      etiquetaIndice: "Roles y ámbito",
      cuerpo:
        "En Membresías de rol (rol y ámbito) cargá al menos una fila: elegí el Rol y, según el rol, la Materia o la Carrera donde rige. Un rol global (por ejemplo Secretaría) no pide ámbito; un Coordinador de Carrera pide Carrera; un Jefe de Cátedra pide Materia; un Docente pide Materia y Carrera. + Agregar membresía suma otra fila y × la quita. No se puede repetir la misma membresía.",
      captura: {
        src: "/ayuda/administracion/administrar-usuarios-paso-4.webp",
        alt: "Filas de membresía con Rol, Materia y Carrera.",
      },
      nota: {
        tipo: "importante",
        cuerpo:
          "El rol define qué pantallas ve la persona y el ámbito define sobre qué materias o carreras actúa. Un error de ámbito deja a la persona sin ver lo que necesita o viendo de más.",
      },
    },
    {
      titulo: "Creá el usuario",
      etiquetaIndice: "Crear",
      cuerpo:
        "Pulsá Crear usuario. Recién cuando el servidor lo confirma, el modal se cierra y el usuario aparece en la tabla como Activo. Si el servidor rechaza el alta, el modal queda abierto con el motivo.",
    },
    {
      titulo: "Editá un usuario",
      etiquetaIndice: "Editar",
      cuerpo:
        "Hacé clic en la fila o pulsá Editar. El formulario trae los datos y las membresías actuales; cambiá lo que haga falta y pulsá Guardar cambios. Se guardan juntos los datos personales y la lista completa de membresías: si algo es inválido, no se guarda nada. Si la persona es docente, la columna Perfil docente tiene un enlace Ver docente.",
    },
    {
      titulo: "Desactivá o activá un usuario",
      etiquetaIndice: "Desactivar y activar",
      cuerpo:
        "En la fila, Desactivar abre una confirmación («Desactivar usuario») que avisa que dejará de poder acceder al sistema; Activar la revierte y le devuelve el acceso. Los inactivos se ven con el estado Inactivo. Los datos no se borran.",
      captura: {
        src: "/ayuda/administracion/administrar-usuarios-paso-7.webp",
        alt: "Confirmación Desactivar usuario.",
      },
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Los cambios se guardan en el servidor y rigen en las solicitudes siguientes de esa persona. Si cambiaste sus roles, ve el menú nuevo cuando se revalide su sesión; si estaba conectada, puede ser necesario que actualice la página o vuelva a ingresar. Todo queda registrado en Registros de auditoría.",
  },
  preguntas: [
    {
      pregunta: "Veo los botones pero me aparece un error al guardar.",
      respuesta:
        "Ver Usuarios y modificarlos son permisos distintos (usuarios.ver y usuarios.administrar). La pantalla muestra los botones a quien puede ver, pero el servidor solo acepta los cambios de quien puede administrar. Pedí el permiso a quien administra los roles.",
    },
    {
      pregunta: "Editaron al usuario al mismo tiempo y no puedo guardar.",
      respuesta:
        "El sistema detecta que los datos cambiaron desde que abriste el formulario y rechaza el guardado para no pisar el cambio ajeno. Cerrá el formulario, volvé a abrirlo y repetí tu cambio.",
    },
    {
      pregunta: "¿Cómo ingresa el usuario nuevo?",
      respuesta:
        "Con su cuenta institucional de Microsoft, cuyo mail es el que cargaste como UPN. Para entrar necesita estar activa y tener al menos un rol vigente. Ver «Cómo ingresar al sistema».",
    },
  ],
  relacionados: [
    "administracion/administrar-docentes",
    "administracion/administrar-roles-y-permisos",
    "administracion/consultar-la-auditoria",
    "primeros-pasos/ingresar-al-sistema",
  ],
};
