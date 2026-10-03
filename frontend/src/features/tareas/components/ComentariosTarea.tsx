import { useState } from "react";
import { Button, Textarea } from "@ars-docendi/ui";
import type { ComentarioTarea } from "../types";
import "./comentariosTarea.css";

interface ComentariosTareaProps {
  comentarios: ComentarioTarea[];
  onAgregar: (texto: string) => void;
  enviando?: boolean;
}

/** Formatea un ISO a dd/mm/aaaa hh:mm, sin depender del locale. */
function formatearFecha(iso: string): string {
  const fecha = new Date(iso);
  const dia = String(fecha.getUTCDate()).padStart(2, "0");
  const mes = String(fecha.getUTCMonth() + 1).padStart(2, "0");
  const horas = String(fecha.getUTCHours()).padStart(2, "0");
  const minutos = String(fecha.getUTCMinutes()).padStart(2, "0");
  return `${dia}/${mes}/${fecha.getUTCFullYear()} ${horas}:${minutos}`;
}

/** Hilo de comentarios internos de la tarea: lista ordenada + input para agregar uno nuevo. */
export function ComentariosTarea({
  comentarios,
  onAgregar,
  enviando = false,
}: ComentariosTareaProps) {
  const [texto, setTexto] = useState("");

  function enviar() {
    if (!texto.trim()) return;
    onAgregar(texto.trim());
    setTexto("");
  }

  return (
    <section className="adoc-det-tarea-panel adoc-com-tarea" aria-label="Comentarios internos">
      <h2>Comentarios internos</h2>

      {comentarios.length === 0 ? (
        <p>Todavía no hay comentarios en esta tarea.</p>
      ) : (
        <ul className="adoc-com-tarea-lista">
          {comentarios.map((c) => (
            <li key={c.id} className="adoc-com-tarea-item">
              <div className="adoc-com-tarea-cabecera">
                <strong>{c.autor}</strong>
                <span>{c.rolAutor}</span>
                <span>{formatearFecha(c.fecha)}</span>
              </div>
              <p>{c.texto}</p>
            </li>
          ))}
        </ul>
      )}

      <div className="adoc-com-tarea-nuevo">
        <Textarea
          value={texto}
          onChange={(e) => setTexto(e.target.value)}
          placeholder="Escribí un comentario…"
          rows={2}
          aria-label="Nuevo comentario"
        />
        <Button variant="secondary" onClick={enviar} loading={enviando} disabled={!texto.trim()}>
          Comentar
        </Button>
      </div>
    </section>
  );
}
