import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Breadcrumbs, Button, Field, InlineAlert, Input } from "@ars-docendi/ui";

import { useCurrentUser } from "../../../shared/auth/useCurrentUser";
import { PageHeader } from "../../../shared/ui/PageHeader";
import { PermisosPorModulo } from "../components/PermisosPorModulo";
import { mensajeConflictoRol } from "../erroresRol";
import { useCatalogoPermisos, useGuardarRol, useRoles } from "../hooks/useRoles";
import {
  ETIQUETAS_SCOPE,
  normalizarNombreRol,
  SCOPES_ROL,
  type DatosRolEditables,
  type PermisoRol,
  type RolMock,
} from "../models";
import "../roles.css";

export function EditorPage() {
  const { rolId } = useParams();
  const { consulta } = useRoles();
  const catalogo = useCatalogoPermisos();

  if (consulta.isError || catalogo.isError) {
    return <InlineAlert severity="danger" title="No se pudo cargar el rol." />;
  }
  if (!consulta.data || !catalogo.data) return <p role="status">Cargando…</p>;
  const rol = rolId ? consulta.data.find((r) => r.id === rolId) : null;
  if (rol === undefined) return <InlineAlert severity="danger" title="El rol no existe." />;
  // El formulario se inicializa una vez por rol y versión: recargar tras un conflicto lo reinicia.
  return (
    <Editor
      key={rol ? `${rol.id}-${rol.version}` : "nuevo"}
      rol={rol}
      roles={consulta.data}
      catalogo={catalogo.data}
    />
  );
}

interface EditorProps {
  rol: RolMock | null;
  roles: RolMock[];
  catalogo: PermisoRol[];
}

function Editor({ rol, roles, catalogo }: EditorProps) {
  const navegar = useNavigate();
  const { consulta } = useRoles();
  const permisosUsuario = useCurrentUser().user?.permissions ?? [];
  const editarDatos = permisosUsuario.includes("roles.administrar") && !rol?.es_sistema;
  const editarPermisos = permisosUsuario.includes("roles.gestionar_membresia");
  const guardar = useGuardarRol(rol);
  const [datos, setDatos] = useState<DatosRolEditables>({
    nombre: rol?.nombre ?? "",
    descripcion: rol?.descripcion ?? "",
    scope: rol?.scope ?? "global",
  });
  // Un rol nuevo arranca con todos los permisos destildados.
  const [permisoIds, setPermisoIds] = useState(() => rol?.permisos.map((p) => p.id) ?? []);
  const [enviado, setEnviado] = useState(false);

  const titulo = rol?.nombre ?? "Nuevo rol";
  const nombreVacio = !datos.nombre.trim();
  const nombreDuplicado =
    !nombreVacio &&
    roles.some(
      (r) =>
        r.id !== rol?.id &&
        r.activo &&
        normalizarNombreRol(r.nombre) === normalizarNombreRol(datos.nombre),
    );
  const puedeGuardar = editarDatos || editarPermisos;

  function enviar() {
    setEnviado(true);
    if (nombreVacio || nombreDuplicado) return;
    guardar.mutate(
      {
        datos: {
          nombre: datos.nombre.trim(),
          descripcion: datos.descripcion.trim(),
          scope: datos.scope,
        },
        permisoIds,
      },
      {
        onSuccess: (r) =>
          navegar("/roles", {
            state: { aviso: `${r.nombre}: ${rol ? "cambios guardados." : "quedó creado."}` },
          }),
      },
    );
  }

  return (
    <div className="roles-editor">
      <Breadcrumbs
        separator="›"
        items={[
          { label: "Inicio", href: "/" },
          { label: "Roles", href: "/roles" },
          { label: titulo },
        ]}
      />
      <PageHeader title={titulo} />

      {guardar.isError && (
        <InlineAlert severity="danger" title={mensajeConflictoRol(guardar.error)}>
          {rol && (
            <Button variant="secondary" size="sm" onClick={() => void consulta.refetch()}>
              Recargar
            </Button>
          )}
        </InlineAlert>
      )}
      {rol?.es_sistema && (
        <InlineAlert severity="info" title="Es un rol creado por el sistema">
          A los roles del sistema no se les puede modificar el nombre, la descripción ni el ámbito.
          Sus permisos sí se pueden ajustar.
        </InlineAlert>
      )}

      <section className="roles-editor-tarjeta">
        <h2>Datos del rol</h2>
        <div className="roles-editor-datos">
          <Field
            label="Nombre"
            required
            hint="Cómo se ve el rol al asignarlo a una persona. No puede repetirse."
            error={
              enviado && nombreVacio
                ? "Campo obligatorio"
                : nombreDuplicado
                  ? "Ya existe un rol con ese nombre."
                  : undefined
            }
          >
            <Input
              value={datos.nombre}
              disabled={!editarDatos}
              onChange={(e) => setDatos({ ...datos, nombre: e.target.value })}
            />
          </Field>
          <Field label="Descripción" hint="Para qué sirve el rol o a quién se asigna.">
            <Input
              value={datos.descripcion}
              disabled={!editarDatos}
              onChange={(e) => setDatos({ ...datos, descripcion: e.target.value })}
            />
          </Field>
          <Field
            label="Ámbito"
            required
            hint="Dónde rige al asignarlo: en todo el sistema, en una materia o en una carrera."
          >
            <select
              className="adoc-select"
              value={datos.scope}
              disabled={!editarDatos}
              onChange={(e) =>
                setDatos({ ...datos, scope: e.target.value as DatosRolEditables["scope"] })
              }
            >
              {SCOPES_ROL.map((scope) => (
                <option key={scope} value={scope}>
                  {ETIQUETAS_SCOPE[scope]}
                </option>
              ))}
            </select>
          </Field>
        </div>
      </section>

      <section className="roles-editor-tarjeta">
        <h2>Permisos</h2>
        <PermisosPorModulo
          catalogo={catalogo}
          asignados={permisoIds}
          deshabilitado={!editarPermisos || guardar.isPending}
          onChange={setPermisoIds}
        />
      </section>

      <div className="roles-editor-pie">
        {puedeGuardar ? (
          <>
            <Button variant="secondary" onClick={() => navegar("/roles")}>
              Cancelar
            </Button>
            <Button variant="primary" loading={guardar.isPending} onClick={enviar}>
              {rol ? "Guardar cambios" : "Crear rol"}
            </Button>
          </>
        ) : (
          <Button variant="secondary" onClick={() => navegar("/roles")}>
            Volver al listado
          </Button>
        )}
      </div>
    </div>
  );
}
