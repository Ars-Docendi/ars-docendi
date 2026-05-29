// ============================================================
// DEV MOCK LOGIN — remove with shared/auth/dev/
// ------------------------------------------------------------
// Role picker shown when "Iniciar sesión" is clicked while no real
// SSO is configured. Reuses the @ars-docendi/ui Modal (portal +
// backdrop + Escape handling) and lists the hardcoded MOCK_USERS.
// ============================================================
import { Modal } from "@ars-docendi/ui";

import { MOCK_USERS } from "./mockUsers";
import "./MockLoginModal.css";

interface MockLoginModalProps {
  open: boolean;
  onClose: () => void;
  /** Receives the selected mock user's id. */
  onSelect: (userId: string) => void;
}

export function MockLoginModal({ open, onClose, onSelect }: MockLoginModalProps) {
  return (
    <Modal
      open={open}
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
      title="Ingresar como… (mock de pruebas)"
      aria-label="Seleccionar usuario de prueba"
    >
      <ul className="mock-login-list">
        {MOCK_USERS.map((u) => (
          <li key={u.id}>
            <button type="button" className="mock-login-row" onClick={() => onSelect(u.id)}>
              <span className="mock-login-avatar" aria-hidden="true">
                {u.currentUser.initials}
              </span>
              <span className="mock-login-info">
                <span className="mock-login-name">{u.db.display_name}</span>
                <span className="mock-login-upn">{u.db.upn}</span>
              </span>
              <span className="mock-login-role">{u.assignment.name}</span>
            </button>
          </li>
        ))}
      </ul>
    </Modal>
  );
}
