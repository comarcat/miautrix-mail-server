import type { Mailbox, EmailMessage } from '../types';

export interface WebmailLoginResult {
  data: any;
  token: string;
}

export class WebmailApiClient {
  private baseUrl: string;
  private token: string | null;
  private tenantId: string | null;
  private userId: string | null;

  constructor(baseUrl: string = '/api/v1') {
    this.baseUrl = baseUrl;
    this.token = typeof window !== 'undefined' ? localStorage.getItem('miautrix_webmail_token') : null;
    this.tenantId = typeof window !== 'undefined' ? localStorage.getItem('miautrix_webmail_tenant_id') : null;
    this.userId = typeof window !== 'undefined' ? localStorage.getItem('miautrix_webmail_user_id') : null;
  }

  setToken(token: string | null) {
    this.token = token;
    if (typeof window !== 'undefined') {
      if (token) {
        localStorage.setItem('miautrix_webmail_token', token);
      } else {
        localStorage.removeItem('miautrix_webmail_token');
      }
    }
  }

  setTenantId(tenantId: string | null) {
    this.tenantId = tenantId;
    if (typeof window !== 'undefined') {
      if (tenantId) {
        localStorage.setItem('miautrix_webmail_tenant_id', tenantId);
      } else {
        localStorage.removeItem('miautrix_webmail_tenant_id');
      }
    }
  }

  setUserId(userId: string | null) {
    this.userId = userId;
    if (typeof window !== 'undefined') {
      if (userId) {
        localStorage.setItem('miautrix_webmail_user_id', userId);
      } else {
        localStorage.removeItem('miautrix_webmail_user_id');
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
    const res = await fetch(url, { ...options, headers });

    if (res.status === 401) {
      this.setToken(null);
      this.setTenantId(null);
      this.setUserId(null);
      window.dispatchEvent(new CustomEvent('miautrix:auth:expired'));
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

  async login(email: string, password: string): Promise<WebmailLoginResult> {
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
      body: JSON.stringify({ email_or_username: email, password }),
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
    return result as WebmailLoginResult;
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

  async me(): Promise<{ data: any }> {
    return this.request<{ data: any }>('/auth/me');
  }

  async getMailboxes(): Promise<{ data: Mailbox[] }> {
    return this.request<{ data: Mailbox[] }>('/mailboxes');
  }

  async getMessages(mailboxId: string, params: { search?: string; limit?: number; cursor?: string } = {}): Promise<{ data: EmailMessage[] }> {
    const query = new URLSearchParams();
    if (params.search) query.set('search', params.search);
    if (params.limit) query.set('limit', params.limit.toString());
    if (params.cursor) query.set('cursor', params.cursor);
    const qs = query.toString();
    return this.request<{ data: EmailMessage[] }>(`/mailboxes/${mailboxId}/messages${qs ? `?${qs}` : ''}`);
  }

  async sendMessage(mailboxId: string, payload: { to: string; subject: string; body: string }): Promise<{ data: EmailMessage }> {
    return this.request<{ data: EmailMessage }>(`/mailboxes/${mailboxId}/messages/send`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
  }
}

export const webmailClient = new WebmailApiClient();
