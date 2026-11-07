import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Button, Field, InlineAlert, Input, StatusBadge } from "@ars-docendi/ui";
import { EnlaceVolver } from "../../../shared/navegacion/EnlaceVolver";
import { usePestanaURL } from "../../../shared/navegacion/estadoURL";
import { useGuardiaDeCambios } from "../../../shared/navegacion/guardias";
import { Migas } from "../../../shared/navegacion/Migas";
import { useDestinoVolver } from "../../../shared/navegacion/useDestinoVolver";
import { migaDeVolver } from "../../../shared/navegacion/volver";
import { PanelPestana, Pestanas } from "../../../shared/ui/Pestanas";

import { mensajeProblema } from "../../../shared/api/problemDetails";
import { MembresiasSelector } from "../../../shared/ui/MembresiasSelector";
import { BarraAccionesFija } from "../../../shared/ui/BarraAccionesFija";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { usePantallaTelefono } from "../../../shared/ui/usePantallaTelefono";
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
  hayErroresPersonales,
  membresiasSugeridas,
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
const PESTANAS: Pestana[] = ["personales", "docentes"];

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
  const telefono = usePantallaTelefono();
  // Las dos pestañas son vistas de un mismo formulario: cambiar de una a otra reemplaza la
  // entrada del historial, así Atrás sale del formulario en lugar de recorrer pestañas.
  const [pestana, setPestana] = usePestanaURL(PESTANAS, "personales", { reemplazar: true });
  const volver = useDestinoVolver("persona");
  const [form, setForm] = useState<FormularioPersona>(() =>
    personaActual ? formularioDesde(personaActual, cat) : formularioVacio(),
  );
  // Rol Docente precargado desde las designaciones de una persona sin cuenta.
  const [conSugeridas] = useState(
    () => personaActual !== null && membresiasSugeridas(personaActual, cat).length > 0,
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
  // Cambios sin guardar: el formulario se compara con el que se abrió.
  const [formInicial] = useState(() => JSON.stringify(form));
  const { permitirSalida } = useGuardiaDeCambios(
    administrar && JSON.stringify(form) !== formInicial,
  );

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
  // La pestaña de datos docentes es de sólo lectura: no puede tener errores.
  const otraConError = pestana === "personales" ? false : marcaPersonales;
  const setDatos = (campo: keyof FormularioPersona["datos"], valor: string) =>
    setForm({ ...form, datos: { ...form.datos, [campo]: valor } });

  function enviar() {
    setEnviado(true);
    if (hayErroresPersonales(errores) || errorDni) return;
    guardar.mutate(aGuardar(form, cat, versionInicial), {
      onSuccess: (p) => {
        permitirSalida();
        // Reemplaza la edición en el historial: Atrás no vuelve a un formulario ya guardado.
        navegar(volver.ruta, {
          replace: true,
          state: {
            aviso: `${nombreCompleto(p)}: ${personaId ? "cambios guardados." : "quedó creada."}`,
          },
        });
      },
    });
  }

  const marca = (visible: boolean) =>
    visible ? (
      <span className="adoc-personas-marca-error" aria-label="con datos incompletos" />
    ) : null;

  const botonesPie = (
    <>
      <Button variant="secondary" onClick={() => navegar(volver.ruta)}>
        Cancelar
      </Button>
      <Button variant="primary" loading={guardar.isPending} onClick={enviar}>
        {personaId ? "Guardar cambios" : "Crear persona"}
      </Button>
    </>
  );

  return (
    <div className="adoc-personas">
      <Migas
        conVolver
        items={[{ label: "Inicio", href: "/" }, migaDeVolver(volver), { label: titulo }]}
      />
      <PageHeader
        title={titulo}
        actions={<EnlaceVolver id="persona-volver" destino={volver} />}
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

      <Pestanas
        prefijo="persona"
        aria-label="Secciones de la persona"
        value={pestana}
        onChange={(id) => setPestana(id as Pestana)}
        items={[
          { id: "personales", label: <>Datos personales{marca(marcaPersonales)}</> },
          { id: "docentes", label: "Datos docentes" },
        ]}
      />
      {otraConError && (
        <InlineAlert
          severity="warning"
          title={`Hay datos incompletos en la pestaña "${pestana === "personales" ? "Datos docentes" : "Datos personales"}".`}
        />
      )}

      {pestana === "personales" && (
        <PanelPestana
          prefijo="persona"
          id="personales"
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
            {conSugeridas && !soloLectura && (
              <InlineAlert severity="info" title="Rol Docente precargado">
                Se asigna al crear la cuenta, según sus designaciones. Podés quitarlo antes de
                guardar.
              </InlineAlert>
            )}
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
        </PanelPestana>
      )}

      {pestana === "docentes" && (
        <PanelPestana
          prefijo="persona"
          id="docentes"
          style={{ display: "flex", flexDirection: "column", gap: "var(--space-5)" }}
        >
          <section className="adoc-personas-tarjeta">
            <h2>Designaciones vigentes</h2>
            <AsignacionesPersona filas={form.asignaciones} catalogos={cat} />
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
        </PanelPestana>
      )}

      {soloLectura ? null : telefono ? (
        <BarraAccionesFija>{botonesPie}</BarraAccionesFija>
      ) : (
        <div className="adoc-personas-pie">{botonesPie}</div>
      )}
    </div>
  );
}

function estadoDuplicado(activo: boolean | null | undefined) {
  if (activo === true) return ".";
  return activo === false ? ", inactiva." : ", sin cuenta.";
}
