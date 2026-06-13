// ============================================================
// DEV MOCK LOGIN — remove with shared/auth/dev/
// ------------------------------------------------------------
// Persists which mock user was picked in the login modal, kept
// separate from the real auth token in ../auth.ts. useCurrentUser()
// reads getMockUser() to render the app as the chosen role.
// ============================================================
import type { CurrentUser } from "../useCurrentUser";
import { MOCK_USERS } from "./mockUsers";

const MOCK_USER_KEY = "adoc.dev.mockUser";

export function setMockUser(id: string): void {
  localStorage.setItem(MOCK_USER_KEY, id);
}

export function clearMockUser(): void {
  localStorage.removeItem(MOCK_USER_KEY);
}

/** The selected mock user as a CurrentUser, or null if none chosen. */
export function getMockUser(): CurrentUser | null {
  const id = localStorage.getItem(MOCK_USER_KEY);
  if (!id) return null;
  return MOCK_USERS.find((u) => u.id === id)?.currentUser ?? null;
}
