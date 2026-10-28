import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Breadcrumbs, Button, Field, InlineAlert, Input, StatusBadge, Tabs } from "@ars-docendi/ui";

import { mensajeProblema } from "../../../shared/api/problemDetails";
import { MembresiasSelector } from "../../../shared/ui/MembresiasSelector";
import { PageHeader } from "../../../shared/ui/PageHeader";
import {
  verificarDocumento,
  type CatalogosPersonas,
  type DocumentoRegistrado,
  type Persona,
} from "../api/personasApi";
import { AsignacionesPersona } from "../components/AsignacionesPersona";
import { PortalPersona } from "../components/PortalPersona";
import { nombreCompleto } from "../filtrosPersonas";
import {
  CAMPOS,
  aGuardar,
  formularioDesde,
  formularioVacio,
  hayErroresDocentes,
  hayErroresPersonales,
  validar,
  type FormularioPersona,
} from "../formularioPersona";
import {
  useCatalogosPersonas,
  useGuardarPersona,
  usePermisosPersonas,
  usePersona,
} from "../hooks/usePersonas";
import "./personas.css";

type Pestana = "personales" | "docentes";

export function EditorPage() {
  const { personaId } = useParams();
  const persona = usePersona(personaId);
  const catalogos = useCatalogosPersonas();

  if (persona.isError || catalogos.isError) {
    return <InlineAlert severity="danger" title="No se pudo cargar la persona." />;
  }
  if (!catalogos.data || (personaId && !persona.data)) return <p role="status">Cargando…</p>;
  // El formulario se inicializa una vez por persona: `key` lo reinicia al cambiar de persona.
  return (
    <Editor
      key={personaId ?? "nueva"}
      personaId={personaId}
      persona={persona.data ?? null}
      catalogos={catalogos.data}
    />
  );
}

interface EditorProps {
  personaId: string | undefined;
  persona: Persona | null;
  catalogos: CatalogosPersonas;
}

function Editor({ personaId, persona: personaActual, catalogos: cat }: EditorProps) {
  const navegar = useNavigate();
  const { administrar, administrarPortal } = usePermisosPersonas();
  const guardar = useGuardarPersona(personaId);
  const [pestana, setPestana] = useState<Pestana>("personales");
  const [form, setForm] = useState<FormularioPersona>(() =>
    personaActual ? formularioDesde(personaActual) : formularioVacio(),
  );
  // Versión con la que se abrió el formulario: si otro la cambió mientras tanto, el guardado
  // falla por concurrencia en lugar de pisar ese cambio.
  const [versionInicial] = useState(personaActual?.version);
  const [enviado, setEnviado] = useState(false);
  // Resultado de la verificación de DNI junto con los dígitos consultados.
  const [verificacion, setVerificacion] = useState<{
    digitos: string;
    resultado: DocumentoRegistrado;
  } | null>(null);
  const persona = { data: personaActual };

  // DNI repetido: se avisa mientras se escribe (sólo quien puede guardar).
  const digitos = form.datos.documento.replace(/\D/g, "");
  useEffect(() => {
    if (!administrar || digitos.length < 6) return;
    const temporizador = setTimeout(() => {
      verificarDocumento(digitos, personaId)
        .then((resultado) => setVerificacion({ digitos, resultado }))
        .catch(() => setVerificacion(null));
    }, 350);
    return () => clearTimeout(temporizador);
  }, [digitos, administrar, personaId]);
  const duplicado =
    verificacion && verificacion.digitos === digitos && verificacion.resultado.existe
      ? verificacion.resultado
      : null;

  const soloLectura = !administrar;
  const titulo = persona.data ? nombreCompleto(persona.data) : "Nueva persona";
  const errores = validar(form, cat);
  const errorDni = duplicado
    ? `Ya existe una persona con este DNI: ${duplicado.nombre}${duplicado.legajo ? ` (legajo ${duplicado.legajo})` : ""}${estadoDuplicado(duplicado.activo)}`
    : undefined;
  const marcaPersonales = (enviado && hayErroresPersonales(errores)) || Boolean(errorDni);
  const marcaDocentes = enviado && hayErroresDocentes(errores);
  const otraConError = pestana === "personales" ? marcaDocentes : marcaPersonales;
  const setDatos = (campo: keyof FormularioPersona["datos"], valor: string) =>
    setForm({ ...form, datos: { ...form.datos, [campo]: valor } });

  function enviar() {
    setEnviado(true);
    if (hayErroresPersonales(errores) || hayErroresDocentes(errores) || errorDni) return;
    guardar.mutate(aGuardar(form, cat, versionInicial), {
      onSuccess: (p) =>
        navegar("/personas", {
          state: {
            aviso: `${nombreCompleto(p)}: ${personaId ? "cambios guardados." : "quedó creada."}`,
          },
        }),
    });
  }

  const marca = (visible: boolean) =>
    visible ? (
      <span className="adoc-personas-marca-error" aria-label="con datos incompletos" />
    ) : null;

  return (
    <div className="adoc-personas">
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Personas", href: "/personas" },
          { label: titulo },
        ]}
      />
      <PageHeader
        title={titulo}
        meta={
          persona.data &&
          (persona.data.tieneCuenta ? (
            <StatusBadge
              kind={persona.data.activo ? "aprobado" : "rechazado"}
              label={persona.data.activo ? "Activo" : "Inactivo"}
            />
          ) : (
            <StatusBadge kind="pendiente" label="Sin cuenta" />
          ))
        }
      />

      {guardar.isError && (
        <InlineAlert
          severity="danger"
          title={mensajeProblema(guardar.error, "No se pudo guardar la persona.")}
        />
      )}

      <Tabs
        aria-label="Secciones de la persona"
        value={pestana}
        onChange={(id) => setPestana(id as Pestana)}
        items={[
          { id: "personales", label: <>Datos personales{marca(marcaPersonales)}</> },
          { id: "docentes", label: <>Datos docentes{marca(marcaDocentes)}</> },
        ]}
      />
      {otraConError && (
        <InlineAlert
          severity="warning"
          title={`Hay datos incompletos en la pestaña "${pestana === "personales" ? "Datos docentes" : "Datos personales"}".`}
        />
      )}

      {pestana === "personales" && (
        <div
          role="tabpanel"
          aria-label="Datos personales"
          style={{ display: "flex", flexDirection: "column", gap: "var(--space-5)" }}
        >
          <section className="adoc-personas-tarjeta">
            <h2>Datos personales</h2>
            <div className="adoc-personas-grilla">
              {CAMPOS.map((c) => {
                const error =
                  c.campo === "documento" && errorDni
                    ? errorDni
                    : enviado
                      ? errores.campos[c.campo]
                      : undefined;
                return (
                  <div key={c.campo} style={c.ancho ? { gridColumn: "span 2" } : undefined}>
                    <Field label={c.etiqueta} required={c.requerido} error={error}>
                      <Input
                        type={c.tipo ?? "text"}
                        value={form.datos[c.campo]}
                        placeholder={c.placeholder}
                        disabled={soloLectura}
                        invalid={Boolean(error)}
                        onChange={(e) => setDatos(c.campo, e.target.value)}
                      />
                    </Field>
                  </div>
                );
              })}
            </div>
          </section>
          <section className="adoc-personas-tarjeta">
            <h2>Roles y ámbitos</h2>
            <MembresiasSelector
              filas={form.membresias}
              onChange={(membresias) => setForm({ ...form, membresias })}
              roles={cat.roles}
              materias={cat.materias}
              materiasPlan={cat.materiasPlan}
              carreras={cat.carreras}
              error={enviado ? errores.membresias : undefined}
              soloLectura={soloLectura}
            />
          </section>
        </div>
      )}

      {pestana === "docentes" && (
        <div
          role="tabpanel"
          aria-label="Datos docentes"
          style={{ display: "flex", flexDirection: "column", gap: "var(--space-5)" }}
        >
          <section className="adoc-personas-tarjeta">
            <h2>Asignaciones</h2>
            <AsignacionesPersona
              filas={form.asignaciones}
              onChange={(asignaciones) => setForm({ ...form, asignaciones })}
              catalogos={cat}
              soloLectura={soloLectura}
            />
            {enviado && errores.asignaciones && (
              <span className="adoc-personas-error">{errores.asignaciones}</span>
            )}
          </section>
          <section className="adoc-personas-tarjeta">
            <h2>Perfil en Mi Portal</h2>
            {personaId ? (
              <PortalPersona personaId={personaId} editable={administrarPortal} />
            ) : (
              <span className="adoc-personas-sin-dato">
                Una persona nueva todavía no tiene perfil en Mi Portal: lo completa al ingresar.
              </span>
            )}
          </section>
        </div>
      )}

      <div className="adoc-personas-pie">
        {soloLectura ? (
          <Button variant="secondary" onClick={() => navegar("/personas")}>
            Volver al listado
          </Button>
        ) : (
          <>
            <Button variant="secondary" onClick={() => navegar("/personas")}>
              Cancelar
            </Button>
            <Button variant="primary" loading={guardar.isPending} onClick={enviar}>
              {personaId ? "Guardar cambios" : "Crear persona"}
            </Button>
          </>
        )}
      </div>
    </div>
  );
}

function estadoDuplicado(activo: boolean | null | undefined) {
  if (activo === true) return ".";
  return activo === false ? ", inactiva." : ", sin cuenta.";
}
