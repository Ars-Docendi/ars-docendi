import { useEffect, useRef, useState, type KeyboardEvent } from "react";

import type { CredencialDeEmpresa } from "../types";
import { textoDeLaClave } from "../utils/modeloDelAsistente";

export interface GrupoDeModelosEnLaNube {
  empresa: string;
  nombre: string;
  credencial: CredencialDeEmpresa | null;
  modelos: string[];
}

interface ListaDeModelosEnLaNubeProps {
  /** El `id` del botón; también prefija los ids de las opciones. */
  id: string;
  /** El id del elemento que rotula la lista. */
  etiquetadaPor: string;
  describedBy?: string;
  grupos: GrupoDeModelosEnLaNube[];
  seleccion: { empresa: string; modelo: string } | null;
  enUso: { empresa: string; modelo: string } | null;
  onElegir: (empresa: string, modelo: string) => void;
}

const CHEVRON = (
  <svg
    className="adoc-asistente-modelo-lista-chevron"
    width="16"
    height="16"
    viewBox="0 0 14 14"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.5"
    aria-hidden="true"
  >
    <path d="M3 5.5L7 9.5l4-4" />
  </svg>
);

const CHECK = (
  <svg
    width="14"
    height="14"
    viewBox="0 0 20 20"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    aria-hidden="true"
  >
    <path d="M5 10.5L8.5 14 15 6.5" />
  </svg>
);

/** La lista de modelos en la nube, agrupada por empresa (tarea 4.6). */
export function ListaDeModelosEnLaNube({
  id,
  etiquetadaPor,
  describedBy,
  grupos,
  seleccion,
  enUso,
  onElegir,
}: ListaDeModelosEnLaNubeProps) {
  const [abierta, setAbierta] = useState(false);
  const [activa, setActiva] = useState(0);
  const contenedorRef = useRef<HTMLDivElement>(null);
  const botonRef = useRef<HTMLButtonElement>(null);
  const listaRef = useRef<HTMLDivElement>(null);

  const opciones = grupos.flatMap((g) =>
    g.modelos.map((modelo) => ({ empresa: g.empresa, modelo })),
  );
  const idDeOpcion = (indice: number) => `${id}-opcion-${indice}`;

  const grupoSeleccionado = grupos.find((g) => g.empresa === seleccion?.empresa);

  function abrir() {
    const indice = opciones.findIndex(
      (o) => o.empresa === seleccion?.empresa && o.modelo === seleccion?.modelo,
    );
    setActiva(indice >= 0 ? indice : 0);
    setAbierta(true);
  }

  function cerrar(devolverElFoco: boolean) {
    setAbierta(false);
    if (devolverElFoco) botonRef.current?.focus();
  }

  function elegir(indice: number) {
    const opcion = opciones[indice];
    if (!opcion) return;
    onElegir(opcion.empresa, opcion.modelo);
    cerrar(true);
  }

  // Al abrir, el foco pasa a la lista: es ella quien recibe las flechas.
  useEffect(() => {
    if (abierta) listaRef.current?.focus();
  }, [abierta]);

  // La opción resaltada siempre queda a la vista en una lista que scrollea.
  useEffect(() => {
    if (abierta) {
      document.getElementById(`${id}-opcion-${activa}`)?.scrollIntoView?.({ block: "nearest" });
    }
  }, [abierta, activa, id]);

  // Clic afuera.
  useEffect(() => {
    if (!abierta) return;
    function alPresionar(evento: MouseEvent) {
      if (!contenedorRef.current?.contains(evento.target as Node)) setAbierta(false);
    }
    document.addEventListener("mousedown", alPresionar);
    return () => document.removeEventListener("mousedown", alPresionar);
  }, [abierta]);

  function alTeclearEnElBoton(evento: KeyboardEvent<HTMLButtonElement>) {
    if (evento.key === "ArrowDown" || evento.key === "ArrowUp") {
      evento.preventDefault();
      abrir();
    }
  }

  function alTeclearEnLaLista(evento: KeyboardEvent<HTMLDivElement>) {
    switch (evento.key) {
      case "ArrowDown":
        evento.preventDefault();
        setActiva((i) => Math.min(i + 1, opciones.length - 1));
        break;
      case "ArrowUp":
        evento.preventDefault();
        setActiva((i) => Math.max(i - 1, 0));
        break;
      case "Home":
        evento.preventDefault();
        setActiva(0);
        break;
      case "End":
        evento.preventDefault();
        setActiva(opciones.length - 1);
        break;
      case "Enter":
      case " ":
        evento.preventDefault();
        elegir(activa);
        break;
      case "Escape":
        evento.preventDefault();
        // Esc cierra la lista, no el panel que la contiene.
        evento.stopPropagation();
        cerrar(true);
        break;
      case "Tab":
        cerrar(false);
        break;
    }
  }

  // Dónde empieza cada grupo dentro de la lista plana de opciones.
  const inicios = grupos.map((_, i) =>
    grupos.slice(0, i).reduce((suma, g) => suma + g.modelos.length, 0),
  );

  return (
    <div className="adoc-asistente-modelo-lista" ref={contenedorRef}>
      <button
        ref={botonRef}
        type="button"
        id={id}
        className="adoc-asistente-modelo-lista-boton"
        aria-haspopup="listbox"
        aria-expanded={abierta}
        aria-controls={abierta ? `${id}-lista` : undefined}
        aria-labelledby={`${etiquetadaPor} ${id}`}
        aria-describedby={describedBy}
        onClick={() => (abierta ? cerrar(false) : abrir())}
        onKeyDown={alTeclearEnElBoton}
      >
        <span className="adoc-asistente-modelo-lista-valor">
          <span className="adoc-asistente-modelo-lista-nombre">
            {seleccion?.modelo ?? "Sin modelos en el catálogo"}
          </span>
          {grupoSeleccionado && (
            <span className="adoc-asistente-modelo-lista-sub">
              {grupoSeleccionado.nombre} · {textoDeLaClave(grupoSeleccionado.credencial)}
            </span>
          )}
        </span>
        {CHEVRON}
      </button>

      {abierta && (
        <div
          ref={listaRef}
          id={`${id}-lista`}
          role="listbox"
          tabIndex={-1}
          aria-labelledby={etiquetadaPor}
          aria-activedescendant={idDeOpcion(activa)}
          className="adoc-asistente-modelo-lista-panel"
          onKeyDown={alTeclearEnLaLista}
        >
          {grupos.map((g, indiceDelGrupo) => {
            const conClave = g.credencial?.estado === "guardada";
            return (
              <div
                key={g.empresa}
                role="group"
                aria-label={`${g.nombre}, ${textoDeLaClave(g.credencial)}`}
                className="adoc-asistente-modelo-lista-grupo"
              >
                <div className="adoc-asistente-modelo-lista-encabezado" aria-hidden="true">
                  <span>{g.nombre}</span>
                  <span
                    className={`adoc-asistente-modelo-lista-clave ${
                      conClave
                        ? "adoc-asistente-modelo-lista-clave--con"
                        : "adoc-asistente-modelo-lista-clave--sin"
                    }`}
                  >
                    {textoDeLaClave(g.credencial)}
                  </span>
                </div>
                {g.modelos.map((modelo, indiceEnElGrupo) => {
                  const indice = inicios[indiceDelGrupo] + indiceEnElGrupo;
                  const elegida = seleccion?.empresa === g.empresa && seleccion.modelo === modelo;
                  const vigente = enUso?.empresa === g.empresa && enUso.modelo === modelo;
                  return (
                    <div
                      key={modelo}
                      id={idDeOpcion(indice)}
                      role="option"
                      aria-selected={elegida}
                      className={`adoc-asistente-modelo-lista-opcion${
                        indice === activa ? " adoc-asistente-modelo-lista-opcion--activa" : ""
                      }${elegida ? " adoc-asistente-modelo-lista-opcion--elegida" : ""}`}
                      onClick={() => elegir(indice)}
                      onMouseMove={() => indice !== activa && setActiva(indice)}
                    >
                      <span className="adoc-asistente-modelo-lista-check" aria-hidden="true">
                        {elegida && CHECK}
                      </span>
                      <span className="adoc-asistente-modelo-lista-opcion-nombre">{modelo}</span>
                      {vigente && (
                        <span className="sistema-pill sistema-pill--disponible">
                          <span aria-hidden="true" className="sistema-pill-punto" />
                          En uso
                        </span>
                      )}
                    </div>
                  );
                })}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
