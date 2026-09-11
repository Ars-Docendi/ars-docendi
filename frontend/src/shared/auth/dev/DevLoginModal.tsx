import { Modal } from "@ars-docendi/ui";
import { useIdentidadesDesarrollo } from "./useIdentidadesDesarrollo";
import "./DevLoginModal.css";

interface DevLoginModalProps {
  open: boolean;
  onClose: () => void;
  onSelect: (userId: string, roleCode: string) => void;
}

export function DevLoginModal({ open, onClose, onSelect }: DevLoginModalProps) {
  const identidades = useIdentidadesDesarrollo();
  return (
    <Modal
      open={open}
      onOpenChange={(next) => !next && onClose()}
      title="Ingresar con una identidad de desarrollo"
      aria-label="Seleccionar identidad de desarrollo"
    >
      {identidades.isLoading && <p role="status">Cargando identidades…</p>}
      {identidades.isError && (
        <div role="alert">
          No se pudieron cargar las identidades.{" "}
          <button type="button" onClick={() => identidades.refetch()}>
            Reintentar
          </button>
        </div>
      )}
      {identidades.data?.length === 0 && <p>No hay identidades habilitadas.</p>}
      <ul className="dev-login-list">
        {identidades.data?.flatMap((identidad) =>
          identidad.roles.map((rol) => (
            <li key={`${identidad.usuarioId}:${rol.codigo}`}>
              <button
                type="button"
                className="dev-login-row"
                onClick={() => onSelect(identidad.usuarioId, rol.codigo)}
              >
                <span className="dev-login-avatar" aria-hidden="true">
                  {iniciales(identidad.nombreParaMostrar)}
                </span>
                <span className="dev-login-info">
                  <span className="dev-login-name">{identidad.nombreParaMostrar}</span>
                  <span className="dev-login-upn">{identidad.upn}</span>
                </span>
                <span className="dev-login-role">{rol.nombre}</span>
              </button>
            </li>
          )),
        )}
      </ul>
    </Modal>
  );
}

function iniciales(nombre: string): string {
  return nombre
    .split(/\s+/)
    .slice(0, 2)
    .map((parte) => parte[0])
    .join("")
    .toUpperCase();
}
