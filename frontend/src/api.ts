export type RegistrationMode = 'Open' | 'ApprovalRequired' | 'Closed'
export type RegistrationRequestStatus = 'Pending' | 'Approved' | 'Rejected'
export type ClaimType = 'Travel' | 'General'
export type ClaimStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Cancelled'
export type PayoutStatus = 'NotApplicable' | 'Pending' | 'Paid'
export type MealAllowanceStatus = 'Draft' | 'PendingTravelReview' | 'PendingReview' | 'Approved' | 'Rejected' | 'Cancelled'
export type ExpenseCategory = 'DepartureTransport' | 'ReturnTransport' | 'Lodging' | 'OfficeSupplies' | 'Meal' | 'Other' | 'Unspecified'

export type Session = {
  token: string
  user: { id: string; displayName: string; phoneNumber: string; profileIncomplete: boolean }
  roles: string[]
  profileIncomplete: boolean
}

export type UserProfile = { personalName?: string | null; bankCardNumber?: string | null; profileIncomplete: boolean }

export type PagedResult<T> = { items: T[]; page: number; pageSize: number; total: number }

export type Project = {
  id: string
  code: string
  name: string
  description?: string | null
  isActive: boolean
  createdAt?: string
  updatedAt?: string
  concurrencyToken: string
}

export type RegistrationRequest = {
  id: string
  displayName: string
  phoneNumber: string
  status: RegistrationRequestStatus
  createdAt: string
  reviewedAt?: string | null
  reviewedById?: string | null
  concurrencyToken: string
}

export type AdminUser = {
  id: string
  displayName: string
  phoneNumber: string
  personalName?: string | null
  bankCardNumber?: string | null
  isActive: boolean
  roles: string[]
}

export type PaymentProfile = { id: string; displayName: string; personalName?: string | null; bankCardNumber?: string | null }

export type ApplicantOption = {
  id: string
  displayName: string
  phoneNumber: string
}

export type ClaimListRow = {
  id: string
  claimNumber: string
  currentVersionId: string
  versionNumber: number
  type: ClaimType
  projectId: string
  projectCode: string
  projectName: string
  applicantId: string
  applicantName: string
  description: string
  totalAmount: number
  status: ClaimStatus
  payoutStatus: PayoutStatus
  mealAllowanceStatus?: MealAllowanceStatus | null
  mealAllowancePayoutStatus?: PayoutStatus | null
  mealAllowanceDays?: number | null
  mealAllowanceTotalAmount?: number | null
  mealAllowanceConcurrencyToken?: string | null
  concurrencyToken: string
  createdAt: string
  updatedAt: string
}

export type Attachment = {
  id: string
  originalFileName: string
  contentType: string
  size: number
  scanStatus: string
  bindingStatus?: string
}

export type ExpenseItem = {
  id: string
  clientKey: string
  category: ExpenseCategory
  amount?: number | null
  currency: string
  expenseDate?: string | null
  merchant?: string | null
  note?: string | null
  attachments: Attachment[]
}

export type ClaimVersion = {
  id: string
  versionNumber: number
  projectId: string
  project: { code: string; name: string }
  description: string
  totalAmount: number
  createdAt: string
  supersededAt?: string | null
  travelItinerary?: {
    departureLocation?: string | null
    destination?: string | null
    departureDate?: string | null
    returnDate?: string | null
  } | null
  mealAllowance?: {
    id: string
    departureDate?: string | null
    returnDate?: string | null
    days: number
    dailyAmount?: number | null
    totalAmount?: number | null
    status: MealAllowanceStatus
    payoutStatus: PayoutStatus
    concurrencyToken: string
    reviewedAt?: string | null
    reviewComment?: string | null
    approvalRecords: Array<{ fromStatus: MealAllowanceStatus; toStatus: MealAllowanceStatus; dailyAmount?: number | null; totalAmount?: number | null; actorId: string; actorDisplayName?: string | null; comment?: string | null; createdAt: string }>
    payoutRecord?: { amount: number; recipientName: string; bankCardLastFour: string; confirmedById: string; confirmedByDisplayName?: string | null; note?: string | null; confirmedAt: string } | null
  } | null
  expenseItems: ExpenseItem[]
}

export type WeeklyReport = {
  id: string
  authorId: string
  authorDisplayName: string
  authorPersonalName?: string | null
  projectId: string
  projectCode: string
  projectName: string
  weekStart: string
  completedWork: string
  nextWeekPlan: string
  issues?: string | null
  lastEditedById: string
  lastEditedByDisplayName: string
  createdAt: string
  updatedAt: string
  concurrencyToken: string
}

export type ClaimVersionSummary = {
  id: string
  versionNumber: number
  projectId: string
  projectCode: string
  projectName: string
  description: string
  totalAmount: number
  createdAt: string
  supersededAt?: string | null
  isCurrent: boolean
}

export type ClaimDetail = {
  id: string
  claimNumber: string
  type: ClaimType
  status: ClaimStatus
  payoutStatus: PayoutStatus
  concurrencyToken: string
  currentVersionId: string
  currentVersion: ClaimVersion
  applicant: { applicantId: string; displayName: string; phoneNumber: string }
  createdAt: string
  updatedAt: string
  submittedAt?: string | null
  reviewedAt?: string | null
  cancelledAt?: string | null
  paidAt?: string | null
  approvalRecords: Array<{
    claimVersionId: string
    versionNumber: number
    fromStatus: ClaimStatus
    toStatus: ClaimStatus
    actorId: string
    actorDisplayName?: string | null
    comment?: string | null
    createdAt: string
  }>
  payoutRecord?: {
    approvedVersionId: string
    amount: number
    confirmedById: string
    confirmedByDisplayName?: string | null
    note?: string | null
    confirmedAt: string
  } | null
}

export type ClaimDraftItem = {
  clientKey: string
  category: ExpenseCategory
  amount?: number | null
  expenseDate?: string | null
  merchant?: string | null
  note?: string | null
  attachmentIds: string[]
}

export type ClaimDraftPayload = {
  projectId: string
  description?: string | null
  travelItinerary?: {
    departureLocation?: string | null
    destination?: string | null
    departureDate?: string | null
    returnDate?: string | null
  } | null
  expenseItems: ClaimDraftItem[]
}

export type ApiProblem = {
  status?: number
  code?: string
  message?: string
  errors?: Record<string, string[]>
  traceId?: string
}

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api'
let token = ''
let unauthorizedHandler: (() => void) | undefined

function handleUnauthorized(status: number) {
  if (status !== 401 || !token) return
  token = ''
  unauthorizedHandler?.()
}

function queryString(values: Record<string, string | number | boolean | null | undefined>) {
  const params = new URLSearchParams()
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  })
  const query = params.toString()
  return query ? `?${query}` : ''
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const isForm = init.body instanceof FormData
  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      ...(isForm ? {} : { 'Content-Type': 'application/json' }),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init.headers ?? {}),
    },
  })
  if (!response.ok) {
    const body = await response.json().catch(() => ({})) as ApiProblem
    handleUnauthorized(response.status)
    throw { ...body, status: response.status } satisfies ApiProblem
  }
  return response.status === 204 ? (undefined as T) : response.json() as Promise<T>
}

async function download(path: string) {
  const response = await fetch(`${baseUrl}${path}`, { headers: token ? { Authorization: `Bearer ${token}` } : {} })
  if (!response.ok) {
    const body = await response.json().catch(() => ({})) as ApiProblem
    handleUnauthorized(response.status)
    throw { ...body, status: response.status } satisfies ApiProblem
  }
  const disposition = response.headers.get('content-disposition') ?? ''
  const encodedName = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1]
  const quotedName = disposition.match(/filename="([^"]+)"/i)?.[1]
  return { blob: await response.blob(), fileName: encodedName ? decodeURIComponent(encodedName) : quotedName ?? 'export.xlsx' }
}

export const api = {
  setToken(value: string) { token = value },
  setUnauthorizedHandler(handler: () => void) { unauthorizedHandler = handler },
  getPublicSettings: () => request<{ registrationMode: RegistrationMode; initialAdministratorRegistration: boolean }>('/registration-settings'),
  login: (body: { phoneNumber: string; password: string }) => request<Session>('/auth/login', { method: 'POST', body: JSON.stringify(body) }),
  register: (body: { displayName: string; phoneNumber: string; password: string }) => request<{ message: string; registrationMode: RegistrationMode; registrationCompleted: boolean; initialAdministrator?: boolean }>('/auth/register', { method: 'POST', body: JSON.stringify(body) }),
  listAvailableProjects: () => request<Project[]>('/projects/available'),
  listMyProjects: () => request<Project[]>('/projects/mine'),
  listClaims: (filters: { projectId?: string; status?: ClaimStatus; page?: number; pageSize?: number }) => request<PagedResult<ClaimListRow>>(`/claims${queryString(filters)}`),
  getClaim: (id: string) => request<ClaimDetail>(`/claims/${id}`),
  getClaimVersions: (id: string) => request<ClaimVersionSummary[]>(`/claims/${id}/versions`),
  getClaimVersion: (claimId: string, versionId: string) => request<ClaimVersion>(`/claims/${claimId}/versions/${versionId}`),
  createClaim: (type: ClaimType, body: ClaimDraftPayload) => request<ClaimDetail>('/claims', { method: 'POST', body: JSON.stringify({ type, ...body }) }),
  createClaimVersion: (id: string, body: ClaimDraftPayload & { expectedCurrentVersionId: string; concurrencyToken: string }) => request<ClaimDetail>(`/claims/${id}/versions`, { method: 'POST', body: JSON.stringify(body) }),
  submitClaim: (id: string, body: { expectedCurrentVersionId: string; concurrencyToken: string }) => request<ClaimDetail>(`/claims/${id}/submit`, { method: 'POST', body: JSON.stringify(body) }),
  cancelClaim: (id: string, body: { expectedCurrentVersionId: string; concurrencyToken: string }) => request<ClaimDetail>(`/claims/${id}/cancel`, { method: 'POST', body: JSON.stringify(body) }),
  uploadStagedAttachment: (file: File) => {
    const body = new FormData()
    body.append('file', file)
    return request<Attachment>('/attachments/staged', { method: 'POST', body })
  },
  attachmentDownloadUrl: (id: string) => `${baseUrl}/attachments/${id}/download`,
  async downloadAttachment(id: string) {
    const response = await fetch(`${baseUrl}/attachments/${id}/download`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
    if (!response.ok) {
      const body = await response.json().catch(() => ({})) as ApiProblem
      handleUnauthorized(response.status)
      throw { ...body, status: response.status } satisfies ApiProblem
    }
    const disposition = response.headers.get('content-disposition') ?? ''
    const encodedName = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1]
    const quotedName = disposition.match(/filename="([^"]+)"/i)?.[1]
    return {
      blob: await response.blob(),
      fileName: encodedName ? decodeURIComponent(encodedName) : quotedName ?? 'attachment',
    }
  },
  changePassword: (body: { currentPassword: string; newPassword: string }) => request<{ message: string }>('/me/password', { method: 'PUT', body: JSON.stringify(body) }),
  getProfile: () => request<UserProfile>('/me/profile'),
  updateProfile: (body: { personalName: string; bankCardNumber: string }) => request<UserProfile>('/me/profile', { method: 'PUT', body: JSON.stringify(body) }),
  listWeeklyReports: (filters: { projectId?: string; weekFrom?: string; weekTo?: string; page?: number; pageSize?: number }) => request<PagedResult<WeeklyReport>>(`/weekly-reports${queryString(filters)}`),
  listAdminWeeklyReports: (filters: { projectId?: string; authorId?: string; weekFrom?: string; weekTo?: string; page?: number; pageSize?: number }) => request<PagedResult<WeeklyReport>>(`/admin/weekly-reports${queryString(filters)}`),
  createWeeklyReport: (body: { projectId: string; weekStart: string; completedWork: string; nextWeekPlan: string; issues?: string }) => request<WeeklyReport>('/weekly-reports', { method: 'POST', body: JSON.stringify(body) }),
  updateWeeklyReport: (id: string, body: { projectId: string; weekStart: string; completedWork: string; nextWeekPlan: string; issues?: string; concurrencyToken: string }) => request<WeeklyReport>(`/weekly-reports/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  getAdminSettings: () => request<{ registrationMode: RegistrationMode; updatedAt: string }>('/admin/registration-settings'),
  updateAdminSettings: (registrationMode: RegistrationMode) => request<{ registrationMode: RegistrationMode; updatedAt: string }>('/admin/registration-settings', { method: 'PUT', body: JSON.stringify({ registrationMode }) }),
  listRegistrationRequests: (filters: { status?: RegistrationRequestStatus; page?: number; pageSize?: number }) => request<PagedResult<RegistrationRequest>>(`/admin/registration-requests${queryString(filters)}`),
  approveRegistration: (id: string, body: { concurrencyToken: string }) => request<{ message: string }>(`/admin/registration-requests/${id}/approve`, { method: 'POST', body: JSON.stringify(body) }),
  rejectRegistration: (id: string, body: { concurrencyToken: string }) => request<{ message: string }>(`/admin/registration-requests/${id}/reject`, { method: 'POST', body: JSON.stringify(body) }),
  listUsers: (filters: { isActive?: boolean; keyword?: string; page?: number; pageSize?: number }) => request<PagedResult<AdminUser>>(`/admin/users${queryString(filters)}`),
  setUserActive: (id: string, active: boolean) => request<{ id: string; isActive: boolean }>(`/admin/users/${id}/${active ? 'enable' : 'disable'}`, { method: 'POST', body: '{}' }),
  setUserAdministrator: (id: string, administrator: boolean) => request<{ id: string; roles: string[] }>(`/admin/users/${id}/administrator/${administrator ? 'grant' : 'revoke'}`, { method: 'POST', body: '{}' }),
  resetUserPassword: (id: string, newPassword: string) => request<{ id: string; message: string }>(`/admin/users/${id}/password`, { method: 'PUT', body: JSON.stringify({ newPassword }) }),
  recordBankCardCopied: (id: string) => request<void>(`/admin/users/${id}/bank-card/copied`, { method: 'POST', body: '{}' }),
  getPaymentProfile: (id: string) => request<PaymentProfile>(`/admin/users/${id}/payment-profile`),
  listApplicants: (filters: { keyword?: string; page?: number; pageSize?: number }) => request<PagedResult<ApplicantOption>>(`/admin/applicants${queryString(filters)}`),
  listProjects: (filters: { isActive?: boolean; keyword?: string; page?: number; pageSize?: number }) => request<PagedResult<Project>>(`/admin/projects${queryString(filters)}`),
  createProject: (body: { code: string; name: string; description?: string }) => request<Project>('/admin/projects', { method: 'POST', body: JSON.stringify(body) }),
  updateProject: (id: string, body: { name: string; description?: string; concurrencyToken: string }) => request<Project>(`/admin/projects/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  setProjectActive: (id: string, active: boolean) => request<{ id: string; isActive: boolean; concurrencyToken: string }>(`/admin/projects/${id}/${active ? 'enable' : 'disable'}`, { method: 'POST', body: '{}' }),
  listAdminClaims: (filters: { projectId?: string; applicantId?: string; status?: ClaimStatus; payoutStatus?: PayoutStatus; workQueue?: 'approval' | 'payout'; createdFrom?: string; createdTo?: string; page?: number; pageSize?: number }) => request<PagedResult<ClaimListRow> & { summary: { claimCount: number; totalAmount: number } }>(`/admin/claims${queryString(filters)}`),
  getClaimGroupSummary: (filters: { groupBy: 'project' | 'applicant'; projectId?: string; applicantId?: string; status?: ClaimStatus; payoutStatus?: PayoutStatus; workQueue?: 'approval' | 'payout'; createdFrom?: string; createdTo?: string }) => request<Array<{ key: string; label: string; claimCount: number; totalAmount: number }>>(`/admin/claims/group-summary${queryString(filters)}`),
  reviewClaim: (claimId: string, versionId: string, action: 'approve' | 'reject', body: { expectedCurrentVersionId: string; concurrencyToken: string; comment?: string }) => request<ClaimDetail>(`/admin/claims/${claimId}/versions/${versionId}/${action}`, { method: 'POST', body: JSON.stringify(body) }),
  confirmPayout: (claimId: string, body: { expectedCurrentVersionId: string; concurrencyToken: string; note?: string }) => request<ClaimDetail>(`/admin/claims/${claimId}/payout/confirm`, { method: 'POST', body: JSON.stringify(body) }),
  reviewMealAllowance: (claimId: string, action: 'approve' | 'reject', body: { expectedCurrentVersionId: string; claimConcurrencyToken: string; mealConcurrencyToken: string; dailyAmount?: number; comment?: string }) => request<ClaimDetail>(`/admin/claims/${claimId}/meal-allowance/${action}`, { method: 'POST', body: JSON.stringify(body) }),
  confirmMealAllowancePayout: (claimId: string, body: { expectedCurrentVersionId: string; claimConcurrencyToken: string; mealConcurrencyToken: string; note?: string }) => request<ClaimDetail>(`/admin/claims/${claimId}/meal-allowance/payout/confirm`, { method: 'POST', body: JSON.stringify(body) }),
  exportClaims: (filters: { projectId: string; submittedFrom?: string; submittedTo?: string }) => download(`/admin/claims/export.xlsx${queryString(filters)}`),
  message(error: unknown, fallback: string) {
    const data = error as ApiProblem
    if (data.code === 'CLAIM_VERSION_STALE') return '数据已发生变化，请刷新后重试。'
    if (data.code === 'MEAL_ALLOWANCE_STALE') return '餐补已发生变化，请刷新后重试。'
    if (data.code === 'PROFILE_INCOMPLETE') return '请先填写个人姓名和银行卡号。'
    if (data.code === 'USER_SELF_DISABLE') return '不能停用当前登录账户。'
    if (data.code === 'LAST_ADMIN_DISABLE') return '不能停用最后一个启用的管理员账户。'
    if (data.code === 'PASSWORD_INCORRECT') return '原密码不正确。'
    if (data.code === 'PASSWORD_UNCHANGED') return '新密码不能与原密码相同。'
    if (data.code === 'USER_INACTIVE_ROLE_CHANGE') return '停用用户不能设为管理员。'
    if (data.code === 'USER_SELF_ADMIN_REVOKE') return '不能取消当前登录账户的管理员角色。'
    if (data.code === 'SUPER_ADMIN_ROLE_REQUIRED') return '超级管理员账号不能取消管理员角色。'
    if (data.code === 'USER_SELF_PASSWORD_RESET') return '请使用账号安全页面修改自己的密码。'
    return data.message ?? (Object.values(data.errors ?? {}).flat().join('；') || fallback)
  },
}
