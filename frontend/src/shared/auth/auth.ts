const TOKEN_KEY = "adoc.auth.token";

/** STUB: presence of a token === authenticated. Replace with MSAL account check. */
export function isAuthenticated(): boolean {
  return Boolean(localStorage.getItem(TOKEN_KEY));
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}
