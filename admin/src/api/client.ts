import {
  ApiResponse,
  QueueItem,
  QueueQueryParams,
  DomainItem,
  CreateDomainRequest,
  VerifyDomainResult,
  UpdateDomainRequest,
  AdminUserItem,
  CreateUserRequest,
  UpdateUserRequest,
  QuarantineItem,
  AuditLogItem,
  MailFlowRuleItem,
  DashboardSummary,
  SystemInfo,
  LicensingInfo,
  BackupJobItem,
} from '../types';

export class AdminApiClient {
  private baseUrl: string;
  private token: string | null;
  private tenantId: string | null;
  private userId: string | null;

  constructor(baseUrl: string = '/api/v1') {
    this.baseUrl = baseUrl;
    this.token = typeof window !== 'undefined' ? localStorage.getItem('miautrix_admin_token') : null;
    this.tenantId = typeof window !== 'undefined' ? localStorage.getItem('miautrix_admin_tenant_id') : null;
    this.userId = typeof window !== 'undefined' ? localStorage.getItem('miautrix_admin_user_id') : null;
  }

  setToken(token: string | null) {
    this.token = token;
    if (typeof window !== 'undefined') {
      if (token) {
        localStorage.setItem('miautrix_admin_token', token);
      } else {
        localStorage.removeItem('miautrix_admin_token');
      }
    }
  }

  setTenantId(tenantId: string | null) {
    this.tenantId = tenantId;
    if (typeof window !== 'undefined') {
      if (tenantId) {
        localStorage.setItem('miautrix_admin_tenant_id', tenantId);
      } else {
        localStorage.removeItem('miautrix_admin_tenant_id');
      }
    }
  }

  setUserId(userId: string | null) {
    this.userId = userId;
    if (typeof window !== 'undefined') {
      if (userId) {
        localStorage.setItem('miautrix_admin_user_id', userId);
      } else {
        localStorage.removeItem('miautrix_admin_user_id');
      }
    }
  }

  getToken(): string | null {
    return this.token;
  }

  getTenantId(): string | null {
    return this.tenantId;
  }

  getUserId(): string | null {
    return this.userId;
  }

  private async request<T>(path: string, options: RequestInit = {}): Promise<T> {
    const headers: Record<string, string> = {
      'Accept': 'application/json',
      ...(options.headers as Record<string, string> || {}),
    };

    if (this.token) {
      headers['Authorization'] = `Bearer ${this.token}`;
    }

    if (this.tenantId) {
      headers['X-Tenant-Id'] = this.tenantId;
    }

    if (this.userId) {
      headers['X-User-Id'] = this.userId;
    }

    const method = options.method?.toUpperCase() || 'GET';
    if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(method) && !headers['Idempotency-Key']) {
      headers['Idempotency-Key'] = typeof crypto !== 'undefined' && crypto.randomUUID
        ? crypto.randomUUID()
        : `idemp-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;
    }

    const url = `${this.baseUrl}${path.startsWith('/') ? path : `/${path}`}`;
    const res = await fetch(url, {
      ...options,
      headers,
    });

    if (res.status === 401) {
      this.setToken(null);
      this.setTenantId(null);
      this.setUserId(null);
      if (typeof window !== 'undefined') {
        window.dispatchEvent(new CustomEvent('miautrix:auth:expired'));
      }
      throw new Error('Unauthorized');
    }

    if (!res.ok) {
      const errorText = await res.text().catch(() => '');
      throw new Error(`API error (${res.status}): ${errorText || res.statusText}`);
    }

    if (res.status === 204) {
      return {} as T;
    }

    return res.json();
  }

  // Authentication
  async login(emailOrUsername: string, password: string, totpCode?: string): Promise<{ data: any; token: string }> {
    const idempotencyKey = typeof crypto !== 'undefined' && crypto.randomUUID
      ? crypto.randomUUID()
      : `idemp-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;

    const res = await fetch(`${this.baseUrl}/auth/login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        'Idempotency-Key': idempotencyKey,
      },
      body: JSON.stringify({
        email_or_username: emailOrUsername,
        password,
        totp_code: totpCode,
      }),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err?.error?.message || err?.message || 'Login failed. Check credentials.');
    }

    const result = await res.json();
    if (result.token) {
      this.setToken(result.token);
    }
    if (result.data?.tenantId) {
      this.setTenantId(result.data.tenantId);
    }
    if (result.data?.id) {
      this.setUserId(result.data.id);
    }
    return result;
  }

  async logout(): Promise<void> {
    try {
      await this.request('/auth/logout', { method: 'POST' });
    } catch {
      // Ignore errors on logout
    } finally {
      this.setToken(null);
      this.setTenantId(null);
      this.setUserId(null);
    }
  }

  async me(): Promise<{ data: AdminUserItem }> {
    return this.request<{ data: AdminUserItem }>('/auth/me');
  }

  // Queue
  async getQueue(params: QueueQueryParams = {}): Promise<ApiResponse<QueueItem[]>> {
    const query = new URLSearchParams();
    if (params.cursor) query.set('cursor', params.cursor);
    if (params.limit) query.set('limit', params.limit.toString());
    if (params.status && params.status !== 'all') query.set('status', params.status);
    if (params.search) query.set('search', params.search);

    const qs = query.toString();
    return this.request<ApiResponse<QueueItem[]>>(`/mail/queue${qs ? `?${qs}` : ''}`);
  }

  async retryQueueItem(id: string): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>(`/mail/queue/${id}/retry`, {
      method: 'POST',
    });
  }

  async deleteQueueItem(id: string): Promise<{ success: boolean }> {
    return this.request<{ success: boolean }>(`/mail/queue/${id}`, {
      method: 'DELETE',
    });
  }

  // Domains
  async getDomains(): Promise<ApiResponse<DomainItem[]>> {
    return this.request<ApiResponse<DomainItem[]>>('/domains');
  }

  async createDomain(data: CreateDomainRequest): Promise<ApiResponse<DomainItem>> {
    return this.request<ApiResponse<DomainItem>>('/domains', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
  }

  async verifyDomain(id: string): Promise<ApiResponse<VerifyDomainResult>> {
    return this.request<ApiResponse<VerifyDomainResult>>(`/domains/${id}/verify`, {
      method: 'POST',
    });
  }

  async deleteDomain(id: string): Promise<void> {
    return this.request<void>(`/domains/${id}`, {
      method: 'DELETE',
    });
  }


  async updateDomain(id: string, data: UpdateDomainRequest): Promise<ApiResponse<DomainItem>> {
    return this.request<ApiResponse<DomainItem>>('/domains/' + id, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
  }
  // Users
  async getUsers(): Promise<ApiResponse<AdminUserItem[]>> {
    return this.request<ApiResponse<AdminUserItem[]>>('/users');
  }

  async createUser(data: CreateUserRequest): Promise<ApiResponse<AdminUserItem>> {
    return this.request<ApiResponse<AdminUserItem>>('/users', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
  }

  async updateUser(id: string, data: UpdateUserRequest): Promise<ApiResponse<AdminUserItem>> {
    return this.request<ApiResponse<AdminUserItem>>(`/users/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
  }

  async deleteUser(id: string): Promise<void> {
    return this.request<void>(`/users/${id}`, {
      method: 'DELETE',
    });
  }

  // Quarantine
  async getQuarantine(params: { status?: string; search?: string } = {}): Promise<ApiResponse<QuarantineItem[]>> {
    const query = new URLSearchParams();
    if (params.status) query.set('status', params.status);
    if (params.search) query.set('search', params.search);
    const qs = query.toString();
    return this.request<ApiResponse<QuarantineItem[]>>(`/quarantine${qs ? `?${qs}` : ''}`);
  }

  async releaseQuarantine(id: string): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>(`/quarantine/${id}/release`, {
      method: 'POST',
    });
  }

  async deleteQuarantine(id: string): Promise<void> {
    return this.request<void>(`/quarantine/${id}`, {
      method: 'DELETE',
    });
  }

  // Audit Logs
  async getAuditLogs(params: { action?: string; search?: string } = {}): Promise<ApiResponse<AuditLogItem[]>> {
    const query = new URLSearchParams();
    if (params.action) query.set('action', params.action);
    if (params.search) query.set('search', params.search);
    const qs = query.toString();
    return this.request<ApiResponse<AuditLogItem[]>>(`/audit${qs ? `?${qs}` : ''}`);
  }

  // Rules
  async getRules(): Promise<ApiResponse<MailFlowRuleItem[]>> {
    return this.request<ApiResponse<MailFlowRuleItem[]>>('/mail/rules');
  }

  async simulateRule(payload: {
    rule: unknown;
    sampleMessage: { sender: string; recipient: string; subject: string; headers: Record<string, string>; hasAttachment: boolean; spamScore?: number };
  }): Promise<{ matched: boolean; actionsTaken: string[]; score: number; log: string[] }> {
    return this.request<{ matched: boolean; actionsTaken: string[]; score: number; log: string[] }>('/mail/rules/simulate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
  }

  // System
  async getDashboardSummary(): Promise<ApiResponse<DashboardSummary>> {
    return this.request<ApiResponse<DashboardSummary>>('/system/dashboard-summary');
  }

  async getSystemInfo(): Promise<ApiResponse<SystemInfo>> {
    return this.request<ApiResponse<SystemInfo>>('/system/info');
  }

  async getLicensing(): Promise<ApiResponse<LicensingInfo>> {
    return this.request<ApiResponse<LicensingInfo>>('/system/licensing');
  }

  async getBackups(): Promise<ApiResponse<BackupJobItem[]>> {
    return this.request<ApiResponse<BackupJobItem[]>>('/system/backup');
  }

  async createBackup(): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>('/system/backup', {
      method: 'POST',
    });
  }
}

export const apiClient = new AdminApiClient();

