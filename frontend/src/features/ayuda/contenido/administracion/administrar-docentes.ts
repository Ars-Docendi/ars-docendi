import type { ArticuloAyuda } from "../../tipos";

export const administrarDocentes: ArticuloAyuda = {
  slug: "administrar-docentes",
  moduloSlug: "administracion",
  grupo: "Personas",
  titulo: "Cómo administrar docentes",
  resumen: 'Alta y edición de docentes, y la vista "Mis docentes" del Jefe de Cátedra.',
  introduccion:
    "Docentes lista a quienes dictan materias, con sus roles docentes, sus asignaciones (materia, cargo, dedicación y horas) y si tienen cuenta. Quien administra puede darlos de alta y editarlos; el Jefe de Cátedra ve una versión de solo lectura con los docentes de sus materias.",
  permisos: ["docentes.ver"],
  palabrasClave: [
    "docentes",
    "mis docentes",
    "alta de docente",
    "asignaciones",
    "cargo",
    "dedicación",
    "horas",
    "materia",
    "jefe de cátedra",
    "persona del sistema",
    "nueva persona",
    "activar",
    "desactivar",
    "cuenta",
  ],
  pasos: [
    {
      titulo: "Abrí Docentes",
      etiquetaIndice: "Abrir Docentes",
      cuerpo:
        "En el menú, grupo Configuración, elegí Docentes (para el Jefe de Cátedra, el título es «Mis docentes»). La tabla muestra Nombre, Documento, Legajo, Rol, Ámbitos, Asignaciones, Cuenta, Estado y Acciones. Cada encabezado tiene su filtro; Nombre, Documento, Legajo, Cuenta y Estado además ordenan.",
      captura: {
        src: "/ayuda/administracion/administrar-docentes-paso-1.webp",
        alt: "Tabla de Docentes con asignaciones y cuenta.",
      },
      nota: {
        tipo: "info",
        titulo: "Si sos Jefe de Cátedra",
        cuerpo:
          "Ves solo a los docentes con una designación vigente en las materias que tenés a cargo, y solo en lectura: no hay Nuevo docente, la columna Acciones no aparece y la fila no abre ningún formulario.",
      },
    },
    {
      titulo: "Leé Cuenta y Estado",
      etiquetaIndice: "Cuenta y Estado",
      cuerpo:
        "Cuenta dice «Sin cuenta» si la persona no tiene usuario en el sistema, o un enlace Ver usuario que te lleva a su ficha en Usuarios. Estado distingue Activo de Inactivo.",
    },
    {
      titulo: "Creá un docente",
      etiquetaIndice: "Nuevo docente",
      cuerpo:
        "Pulsá Nuevo docente. Arriba elegí el modo: Nueva persona (cargás todos los datos personales: nombre, apellido, DNI, legajo, fecha de nacimiento y UPN son obligatorios) o Persona del sistema (elegís en «Elegí una persona del sistema…» a alguien que ya existe; sus datos se muestran en solo lectura). Si esa persona ya es docente, el formulario lo avisa.",
      captura: {
        src: "/ayuda/administracion/administrar-docentes-paso-3.webp",
        alt: "Ventana Nuevo docente con las dos modalidades.",
      },
    },
    {
      titulo: "Cargá membresías y asignaciones",
      etiquetaIndice: "Membresías y asignaciones",
      cuerpo:
        "Las Membresías de rol (rol y ámbito) indican si es Docente o Jefe de Cátedra y en qué materia. Las Asignaciones (materia, carrera, cargo, dedicación y horas) describen su designación: cada fila lleva Materia, Carrera, Cargo, Dedicación y Hs (más de 0). Se necesita al menos una de cada una; + Agregar asignación suma otra fila.",
    },
    {
      titulo: "Creá el docente",
      etiquetaIndice: "Crear",
      cuerpo:
        "Pulsá Crear docente. Si falta algo, el formulario lo marca; si el servidor lo rechaza, el modal queda abierto con el motivo.",
    },
    {
      titulo: "Editá un docente",
      etiquetaIndice: "Editar",
      cuerpo:
        "Pulsá Editar (o la fila). El formulario tiene dos pestañas: Datos docentes (membresías y asignaciones) y Datos personales. Si hay campos incompletos en la otra pestaña, aparece un aviso. Pulsá Guardar cambios: las membresías y las asignaciones se reemplazan completas, no de a una.",
    },
    {
      titulo: "Desactivá o activá",
      etiquetaIndice: "Desactivar y activar",
      cuerpo:
        "Desactivar y Activar piden confirmación («Desactivar docente» o «Activar docente»). Desactivar deja al docente sin aparecer como activo y también desactiva su cuenta de usuario, si la tiene (deja de poder ingresar); no borra sus datos.",
    },
  ],
  despues: {
    titulo: "¿Qué sucede después?",
    cuerpo:
      "Los cambios se guardan en el momento y quedan en Registros de auditoría. Si el docente necesita ingresar al sistema, debe tener además un usuario: se crea desde Usuarios.",
  },
  preguntas: [
    {
      pregunta: "Un docente no aparece en mi lista de Mis docentes.",
      respuesta:
        "El Jefe de Cátedra solo ve docentes con una designación vigente en alguna de sus materias. Si falta alguno, verificá su asignación con quien administra los docentes.",
    },
    {
      pregunta: "¿Crear un docente le da acceso al sistema?",
      respuesta:
        "No por sí solo. La columna Cuenta indica si tiene usuario; si dice «Sin cuenta», hay que crearlo en Usuarios.",
    },
    {
      pregunta: "¿Puedo borrar un docente?",
      respuesta: "No: se lo desactiva. Sus datos y su historial se conservan.",
    },
  ],
  relacionados: ["administracion/administrar-usuarios", "administracion/consultar-la-auditoria"],
};
