import type { ArticuloAyuda } from "../../tipos";

export const administrarPersonas: ArticuloAyuda = {
  slug: "administrar-personas",
  moduloSlug: "administracion",
  grupo: "Personas",
  titulo: "Cómo administrar personas",
  resumen: "Alta, edición, roles por ámbito, asignaciones docentes, activación y desactivación.",
  introduccion:
    "Personas reúne en una sola pantalla a todas las personas del sistema, tengan cuenta o no, con sus roles, sus asignaciones docentes y su estado. Desde acá se da de alta a una persona, se corrigen sus datos, roles y asignaciones, y se activa o desactiva su acceso.",
  permisos: ["personas.ver", "personas.ver_ambito"],
  palabrasClave: [
    "personas",
    "usuarios",
    "docentes",
    "alta",
    "nueva persona",
    "editar persona",
    "activar",
    "desactivar",
    "inactivo",
    "sin cuenta",
    "roles",
    "ámbito",
    "asignaciones",
    "upn",
    "email institucional",
    "legajo",
    "dni",
  ],
  pasos: [
    {
      titulo: "Abrí Personas",
      etiquetaIndice: "Abrir Personas",
      cuerpo:
        "En el menú, grupo Configuración, elegí Personas. El título es «Administrar personas» e indica cuántas hay y cuántas están activas. La tabla muestra Nombre (Apellido, Nombre), Documento, Legajo, Rol, Asignaciones y Estado.",
      captura: {
        src: "/ayuda/administracion/administrar-personas-paso-1.webp",
        alt: "Tabla Administrar personas con nombre, documento, legajo, rol, asignaciones, estado y acciones.",
      },
      nota: {
        tipo: "info",
        titulo: "Si sos Jefe de Cátedra",
        cuerpo:
          "Ves solo a las personas de las materias que tenés a cargo, y solo en lectura: no aparece Nueva persona y al abrir una persona no podés modificarla.",
      },
    },
    {
      titulo: "Buscá y ordená",
      etiquetaIndice: "Buscar y ordenar",
      cuerpo:
        "Cada encabezado tiene un filtro: texto para Nombre, Documento y Legajo, y casillas para Rol (incluida la opción Sin rol) y Estado. Hacé clic en el nombre de una columna para ordenar. Los filtros solo achican lo que ves.",
    },
    {
      titulo: "Leé el estado",
      etiquetaIndice: "Estado",
      cuerpo:
        "Activo e Inactivo indican si la persona puede ingresar al sistema. Sin cuenta significa que la persona existe, por ejemplo porque figura en un pedido de designación, pero todavía no tiene acceso.",
    },
    {
      titulo: "Creá una persona",
      etiquetaIndice: "Nueva persona",
      cuerpo:
        "Pulsá Nueva persona. En la pestaña Datos personales son obligatorios Nombre, Apellido, Documento (DNI), Legajo, Fecha de nacimiento y UPN / Email institucional; CUIL y Teléfono son opcionales. Si el DNI ya existe, el formulario muestra a quién pertenece y no deja guardar.",
      captura: {
        src: "/ayuda/administracion/administrar-personas-paso-4.webp",
        alt: "Formulario Nueva persona en la pestaña Datos personales, con los datos personales y la sección Roles y ámbitos.",
      },
    },
    {
      titulo: "Asigná roles con su ámbito",
      etiquetaIndice: "Roles y ámbitos",
      cuerpo:
        "En Roles y ámbitos cargá al menos una fila: elegí el Rol y, según el rol, la Materia o la Carrera donde rige. Un rol global (por ejemplo Secretaría) no pide ámbito; un Coordinador de Carrera pide Carrera; un Jefe de Cátedra pide Materia; un Docente pide Materia y Carrera.",
      nota: {
        tipo: "importante",
        cuerpo:
          "El rol define qué pantallas ve la persona y el ámbito define sobre qué materias o carreras actúa. Un error de ámbito deja a la persona sin ver lo que necesita o viendo de más.",
      },
    },
    {
      titulo: "Cargá las asignaciones",
      etiquetaIndice: "Asignaciones",
      cuerpo:
        "En la pestaña Datos docentes, Asignaciones describe su designación: cada fila lleva la materia y la carrera, el Cargo, la Dedicación y las Horas. Cambiar elige otra materia, × quita la fila y + Agregar asignación suma otra. Si una pestaña tiene datos incompletos, se marca y el formulario avisa.",
      captura: {
        src: "/ayuda/administracion/administrar-personas-paso-6.webp",
        alt: "Pestaña Datos docentes con una asignación: materia y carrera, cargo, dedicación y horas.",
      },
    },
    {
      titulo: "Consultá su perfil en Mi Portal",
      etiquetaIndice: "Perfil en Mi Portal",
      cuerpo:
        "Debajo de las asignaciones, Perfil en Mi Portal muestra el perfil docente de la persona: contacto, CV, experiencia, educación, certificaciones, proyectos y habilidades. Lo completa la propia persona desde Mi portal; una persona nueva lo completa al ingresar.",
      captura: {
        src: "/ayuda/administracion/administrar-personas-paso-7.webp",
        alt: "Sección Perfil en Mi Portal con contacto, CV y las secciones de experiencia, educación, certificaciones, proyectos y habilidades.",
      },
    },
    {
      titulo: "Guardá",
      etiquetaIndice: "Guardar",
      cuerpo:
        "Al guardar, el sistema vuelve al listado y muestra un aviso con el nombre de la persona. Se guardan juntos los datos personales, los roles y las asignaciones: si algo es inválido, no se guarda nada. Para editar a alguien, abrí su fila, cambiá lo que haga falta y guardá.",
    },
    {
      titulo: "Desactivá o activá una persona",
      etiquetaIndice: "Desactivar y activar",
      cuerpo:
        "En la fila, Desactivar abre la confirmación «Desactivar persona»: la persona deja de poder ingresar al sistema. Activar la revierte y le devuelve el acceso. Los datos no se borran.",
      captura: {
        src: "/ayuda/administracion/administrar-personas-paso-9.webp",
        alt: "Confirmación Desactivar persona: la persona no va a poder ingresar al sistema y sus datos se conservan.",
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
      pregunta: "Puedo abrir una persona pero no modificarla.",
      respuesta:
        "Ver Personas y modificarlas son permisos distintos (personas.ver y personas.administrar). Si solo podés ver, pedí el permiso a quien administra los roles.",
    },
    {
      pregunta: "Editaron a la persona al mismo tiempo y no puedo guardar.",
      respuesta:
        "El sistema detecta que los datos cambiaron desde que abriste el formulario y rechaza el guardado para no pisar el cambio ajeno. Volvé a abrir la persona y repetí tu cambio.",
    },
    {
      pregunta: "¿Cómo ingresa la persona nueva?",
      respuesta:
        "Con su cuenta institucional @docentes.unlam.edu.ar, cuyo mail es el que cargaste como UPN. Para entrar necesita estar activa y tener al menos un rol vigente. Ver «Cómo ingresar al sistema».",
    },
  ],
  relacionados: [
    "administracion/administrar-roles-y-permisos",
    "administracion/consultar-la-auditoria",
    "primeros-pasos/ingresar-al-sistema",
  ],
};
