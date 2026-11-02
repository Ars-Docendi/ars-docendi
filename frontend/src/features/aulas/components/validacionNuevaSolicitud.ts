import {
  fechaTextoAISO,
  horaTextoValida,
  MENSAJE_FECHA_INVALIDA,
  MENSAJE_HORA_INVALIDA,
} from "../../../shared/ui/camposFechaHora";

/** Estado del formulario: los campos de fecha, hora y cantidad son texto tal como se escribió. */
export interface FormularioNuevaSolicitud {
  materiaId: string;
  comision: string;
  dia: string;
  horarioDesde: string;
  horarioHasta: string;
  cantidadAlumnosAprox: string;
}

export const FORMULARIO_VACIO: FormularioNuevaSolicitud = {
  materiaId: "",
  comision: "",
  dia: "",
  horarioDesde: "",
  horarioHasta: "",
  cantidadAlumnosAprox: "",
};

export type ErroresNuevaSolicitud = Partial<Record<keyof FormularioNuevaSolicitud, string>>;

/** Campos en el orden del DOM: al fallar el envío, el primero inválido recibe el foco. */
export const CAMPOS_EN_ORDEN: ReadonlyArray<readonly [keyof FormularioNuevaSolicitud, string]> = [
  ["materiaId", "nueva-materia"],
  ["comision", "nueva-comision"],
  ["dia", "nueva-dia"],
  ["horarioDesde", "nueva-desde"],
  ["horarioHasta", "nueva-hasta"],
  ["cantidadAlumnosAprox", "nueva-alumnos"],
];

/** El backend vuelve a validar todo; esto sólo anticipa el mensaje por campo. */
export function validarNuevaSolicitud(datos: FormularioNuevaSolicitud): ErroresNuevaSolicitud {
  const errores: ErroresNuevaSolicitud = {};
  if (!datos.materiaId) errores.materiaId = "Elegí una materia.";
  if (!datos.comision.trim()) errores.comision = "Ingresá la comisión.";

  if (!datos.dia) errores.dia = "Elegí el día del examen.";
  else if (!fechaTextoAISO(datos.dia)) errores.dia = MENSAJE_FECHA_INVALIDA;

  const desde = horaTextoValida(datos.horarioDesde);
  const hasta = horaTextoValida(datos.horarioHasta);
  if (!datos.horarioDesde) errores.horarioDesde = "Completá el horario desde.";
  else if (!desde) errores.horarioDesde = MENSAJE_HORA_INVALIDA;
  if (!datos.horarioHasta) errores.horarioHasta = "Completá el horario hasta.";
  else if (!hasta) errores.horarioHasta = MENSAJE_HORA_INVALIDA;
  else if (desde && hasta <= desde) {
    errores.horarioHasta = "El horario hasta debe ser posterior al horario desde.";
  }

  const cantidad = Number(datos.cantidadAlumnosAprox);
  if (!cantidad || cantidad <= 0) {
    errores.cantidadAlumnosAprox = "Ingresá una cantidad de alumnos mayor a cero.";
  }
  return errores;
}

export function hayCambios(datos: FormularioNuevaSolicitud): boolean {
  return (Object.keys(FORMULARIO_VACIO) as Array<keyof FormularioNuevaSolicitud>).some(
    (campo) => datos[campo] !== FORMULARIO_VACIO[campo],
  );
}
