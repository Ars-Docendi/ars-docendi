import { Button } from "@ars-docendi/ui";

interface SugerenciasProps {
  sugerencias: string[];
  onElegir: (pregunta: string) => void;
  deshabilitado: boolean;
}

/**
 * Qué otra cosa probar.
 *
 * NO BLOQUEAN nada: el turno ya terminó. Se presentan como preguntas nuevas y no
 * como una elección pendiente, que es la diferencia con las opciones de una
 * aclaración. Colapsar las dos cosas en un solo componente haría que el usuario no
 * pueda distinguir «tengo que elegir» de «puedo probar».
 *
 * Todas salen del catálogo de ejemplos verificados del backend, así que son, por
 * construcción, preguntas que el asistente sabe responder.
 */
export function Sugerencias({ sugerencias, onElegir, deshabilitado }: SugerenciasProps) {
  if (sugerencias.length === 0) return null;

  return (
    <div className="adoc-asistente-sugerencias">
      <p className="adoc-asistente-sugerencias-titulo">Probá con alguna de estas:</p>
      <ul>
        {sugerencias.map((sugerencia) => (
          <li key={sugerencia}>
            <Button
              variant="ghost"
              size="sm"
              disabled={deshabilitado}
              onClick={() => onElegir(sugerencia)}
            >
              {sugerencia}
            </Button>
          </li>
        ))}
      </ul>
    </div>
  );
}
