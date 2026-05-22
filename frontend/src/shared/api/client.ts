import axios from 'axios'

const baseURL = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

export const apiClient = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
})

apiClient.interceptors.request.use((config) => {
  // TODO: inyectar token Azure AD cuando MSAL esté configurado.
  return config
})
