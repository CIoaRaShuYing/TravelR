import { computed, ref } from 'vue'
import { api, type Session } from './api'

const storageKey = 'travel-reimbursement.session'

function isSession(value: unknown): value is Session {
  if (!value || typeof value !== 'object') return false
  const candidate = value as Partial<Session>
  return typeof candidate.token === 'string'
    && typeof candidate.user?.id === 'string'
    && typeof candidate.user?.displayName === 'string'
    && typeof candidate.user?.phoneNumber === 'string'
    && Array.isArray(candidate.roles)
    && candidate.roles.every(role => typeof role === 'string')
}

function tokenExpired(value: string) {
  try {
    const payload = value.split('.')[1]
    if (!payload) return true
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/')
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=')
    const expiresAt = (JSON.parse(atob(padded)) as { exp?: unknown }).exp
    return typeof expiresAt !== 'number' || expiresAt * 1000 <= Date.now()
  } catch {
    return true
  }
}

function removeStoredSession() {
  try { sessionStorage.removeItem(storageKey) }
  catch { /* Browser storage may be unavailable. */ }
}

function restoreSession() {
  try {
    const stored = sessionStorage.getItem(storageKey)
    if (!stored) return null
    const parsed: unknown = JSON.parse(stored)
    if (isSession(parsed) && !tokenExpired(parsed.token)) return parsed
    removeStoredSession()
  } catch {
    removeStoredSession()
  }
  return null
}

function persistSession(value: Session) {
  try { sessionStorage.setItem(storageKey, JSON.stringify(value)) }
  catch { /* The active login still works when browser storage is unavailable. */ }
}

export const session = ref<Session | null>(restoreSession())
export const isAdministrator = computed(() => session.value?.roles.includes('Administrator') ?? false)

api.setToken(session.value?.token ?? '')
api.setUnauthorizedHandler(clearSession)

export async function createSession(phoneNumber: string, password: string) {
  const result = await api.login({ phoneNumber, password })
  api.setToken(result.token)
  session.value = result
  persistSession(result)
  return result
}

export function clearSession() {
  api.setToken('')
  session.value = null
  removeStoredSession()
}
