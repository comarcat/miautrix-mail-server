import type { Mailbox, EmailMessage, RawEmailMessage, Contact, CalendarEvent, SieveFilterRule, MailSignature, EmailAttachment } from '../types';

const normalizeEmailAttachment = (a: any): EmailAttachment => ({
  id: a.id,
  name: a.file_name ?? a.fileName ?? 'attachment',
  size: a.size_bytes ?? a.sizeBytes ?? 0,
  contentType: a.content_type ?? a.contentType ?? 'application/octet-stream',
  blobId: a.download_url ?? a.downloadUrl,
});

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

  private getDefaultHeaders(extra: Record<string, string> = {}): Record<string, string> {
    // Includes Authorization + tenant scoping headers.
    // Useful for downloading binary endpoints without opening a new tab.

    const headers: Record<string, string> = {
      ...(extra as Record<string, string> || {}),
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

    return headers;
  }

  private async request<T>(path: string, options: RequestInit = {}): Promise<T> {
    const headers: Record<string, string> = {
      'Accept': 'application/json',
      ...this.getDefaultHeaders(options.headers as Record<string, string> || {}),
    };

    // getDefaultHeaders already includes Authorization + tenant/user scoping.

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

  async downloadAttachment(downloadPath: string, fileName: string): Promise<void> {
    const headers = this.getDefaultHeaders();

    // downloadPath is expected to be a relative API path like `/api/v1/messages/.../attachments/...`.
    const url = downloadPath.startsWith('/') ? downloadPath : `/${downloadPath}`;

    const res = await fetch(url, {
      method: 'GET',
      headers,
    });

    if (!res.ok) {
      const errorText = await res.text().catch(() => '');
      throw new Error(`Download failed (${res.status}): ${errorText || res.statusText}`);
    }

    const blob = await res.blob();
    const blobUrl = window.URL.createObjectURL(blob);

    try {
      const a = document.createElement('a');
      a.href = blobUrl;
      a.download = fileName;
      a.rel = 'noopener';
      document.body.appendChild(a);
      a.click();
      a.remove();
    } finally {
      window.URL.revokeObjectURL(blobUrl);
    }
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

  async changePassword(currentPassword: string, newPassword: string): Promise<{ data?: any; message?: string }> {
    return this.request<{ data?: any; message?: string }>('/auth/change-password', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        current_password: currentPassword,
        new_password: newPassword,
      }),
    });
  }

  private normalizeArray<T>(res: any): { data: T[] } {
    if (Array.isArray(res)) {
      return { data: res };
    }
    if (res && Array.isArray(res.data)) {
      return { data: res.data };
    }
    if (res && Array.isArray(res.items)) {
      return { data: res.items };
    }
    return { data: [] };
  }

  async getMailboxes(): Promise<{ data: Mailbox[] }> {
    const res = await this.request<any>('/mailboxes');
    return this.normalizeArray<Mailbox>(res);
  }

  async getFolders(mailboxId: string): Promise<{ data: Mailbox[] }> {
    const res = await this.request<any>(`/mailboxes/${mailboxId}/folders`);
    return this.normalizeArray<Mailbox>(res);
  }

  async getMessages(mailboxId: string, folderId: string, params: { search?: string; limit?: number; cursor?: string } = {}): Promise<{ data: EmailMessage[] }> {
    const query = new URLSearchParams();
    query.set('folder_id', folderId);
    if (params.search) query.set('search', params.search);
    if (params.limit) query.set('limit', params.limit.toString());
    if (params.cursor) query.set('cursor', params.cursor);
    const qs = query.toString();
    const res = await this.request<any>(`/mailboxes/${mailboxId}/messages${qs ? `?${qs}` : ''}`);

    const rawMessages = this.normalizeArray<RawEmailMessage>(res).data;
    // The list endpoint is not guaranteed to populate every field (e.g. `preview`
    // is absent), so default instead of letting `undefined` reach component state.
    const messages: EmailMessage[] = rawMessages.map((msg) => ({
      id: msg.id,
      mailboxId: msg.mailbox_id,
      folderId: msg.folder_id,
      from: { name: msg.sender ?? '', email: msg.sender ?? '' },
      to: [{ name: msg.recipient ?? '', email: msg.recipient ?? '' }],
      subject: msg.subject ?? '(No Subject)',
      snippet: msg.preview ?? '',
      bodyHtml: '', // fetched on demand via getMessageDetail
      receivedAt: msg.date ?? '',
      isUnread: !msg.is_read,
      flagColor: msg.flag_color ?? msg.flagColor ?? undefined,
      securityChecks: { spfPass: true, dkimPass: true, dmarcPass: true },
      attachments: [],
    }));

    return { data: messages };
  }

  async getMessageDetail(mailboxId: string, messageId: string): Promise<{ data: EmailMessage }> {
    const res = await this.request<any>(`/mailboxes/${mailboxId}/messages/${messageId}`);
    const d = res?.data ?? res;

    const attachments = (d?.attachments ?? []).map((a: any) => ({
      id: a.id,
      name: a.file_name ?? a.fileName ?? 'attachment',
      size: a.size_bytes ?? a.sizeBytes ?? 0,
      contentType: a.content_type ?? a.contentType ?? 'application/octet-stream',
      blobId: a.download_url ?? a.downloadUrl,
    }));

    const email: EmailMessage = {
      id: d.id,
      mailboxId: d.mailbox_id,
      folderId: d.folder_id,
      from: { name: d.sender, email: d.sender },
      to: [{ name: d.recipient, email: d.recipient }],
      cc: (d.cc ?? d.cc_recipients ?? []).map ? (d.cc ?? d.cc_recipients ?? []).map((email: string) => ({ name: email, email })) : (d.cc ?? d.cc_recipients ?? '').split(',').map((email: string) => email.trim()).filter(Boolean).map((email: string) => ({ name: email, email })),
      bcc: (d.bcc ?? d.bcc_recipients ?? []).map ? (d.bcc ?? d.bcc_recipients ?? []).map((email: string) => ({ name: email, email })) : (d.bcc ?? d.bcc_recipients ?? '').split(',').map((email: string) => email.trim()).filter(Boolean).map((email: string) => ({ name: email, email })),
      subject: d.subject,
      snippet: d.body_text ? String(d.body_text).slice(0, 80) : '',
      bodyHtml: d.body_html ?? d.bodyHtml ?? '',
      bodyText: d.body_text ?? d.bodyText ?? undefined,
      receivedAt: d.date,
      isUnread: !d.is_read,
      flagColor: d.flag_color ?? d.flagColor ?? undefined,
      securityChecks: { spfPass: true, dkimPass: true, dmarcPass: true },
      attachments,
    };

    return { data: email };
  }

  async markRead(mailboxId: string, messageId: string, isRead: boolean): Promise<{ data: boolean }> {
    await this.request<any>(`/mailboxes/${mailboxId}/messages/${messageId}/read`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ is_read: isRead }),
    });

    return { data: true };
  }

  async setFlag(mailboxId: string, messageId: string, color: string | null): Promise<{ data: boolean }> {
    await this.request<any>(`/mailboxes/${mailboxId}/messages/${messageId}/flag`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ color }),
    });

    return { data: true };
  }

  async getFlagAlerts(mailboxId: string): Promise<{ data: any[] }> {
    const res = await this.request<any>(`/mailboxes/${mailboxId}/settings/flags/alerts`);
    return this.normalizeArray<any>(res);
  }

  async setFlagAlert(mailboxId: string, color: string, alertConfigurationJson: string): Promise<{ data: boolean }> {
    await this.request<any>(`/mailboxes/${mailboxId}/settings/flags/${color}/alert`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ alertConfigurationJson }),
    });

    return { data: true };
  }

  async moveMessage(mailboxId: string, messageId: string, targetFolderId: string): Promise<{ data: boolean }> {
    await this.request<any>(`/mailboxes/${mailboxId}/messages/${messageId}/move`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ target_folder_id: targetFolderId }),
    });

    return { data: true };
  }

  async deleteMessage(mailboxId: string, messageId: string, permanent: boolean = false): Promise<{ data: boolean }> {
    const url = `/mailboxes/${mailboxId}/messages/${messageId}?permanent=${permanent ? 'true' : 'false'}`;
    await this.request<any>(url, {
      method: 'DELETE',
    });

    return { data: true };
  }

  async createFolder(mailboxId: string, name: string, parentId: string | null = null): Promise<{ data: Mailbox }> {
    const res = await this.request<any>(`/mailboxes/${mailboxId}/folders`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, parent_id: parentId }),
    });

    return { data: res?.data ?? res };
  }

  async updateFolderParent(
    mailboxId: string,
    folderId: string,
    parentId: string | null,
  ): Promise<{ data: Mailbox }> {
    const res = await this.request<any>(`/mailboxes/${mailboxId}/folders/${folderId}/parent`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ parent_id: parentId }),
    });

    return { data: res?.data ?? res };
  }

  async deleteFolder(mailboxId: string, folderId: string): Promise<void> {
    await this.request<any>(`/mailboxes/${mailboxId}/folders/${folderId}`, {
      method: 'DELETE',
    });
  }

  async sendMessage(mailboxId: string, payload: { from: string; to: string; cc?: string; bcc?: string; subject: string; body: string; bodyHtml?: string }): Promise<{ data: EmailMessage }> {
    const res = await this.request<any>(`/mailboxes/${mailboxId}/messages/send`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        from: payload.from,
        to: payload.to.split(',').map((item) => item.trim()).filter(Boolean),
        cc: (payload.cc ?? '').split(',').map((item) => item.trim()).filter(Boolean),
        bcc: (payload.bcc ?? '').split(',').map((item) => item.trim()).filter(Boolean),
        subject: payload.subject,
        body_text: payload.body,
        body_html: payload.bodyHtml ?? `<p>${payload.body.replace(/\n/g, '<br/>')}</p>`,
      }),
    });

    return { data: res?.data ?? res };
  }

  async upsertDraft(mailboxId: string, payload: { draftId?: string | null; from: string; to: string; cc?: string; bcc?: string; subject: string; body: string; bodyHtml?: string }): Promise<{ data: { draftId: string; success: boolean; message: string } }> {
    const path = payload.draftId ? `/mailboxes/${mailboxId}/drafts/${payload.draftId}` : `/mailboxes/${mailboxId}/drafts`;
    const res = await this.request<any>(path, {
      method: payload.draftId ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        from: payload.from,
        to: payload.to.split(',').map((item) => item.trim()).filter(Boolean),
        cc: (payload.cc ?? '').split(',').map((item) => item.trim()).filter(Boolean),
        bcc: (payload.bcc ?? '').split(',').map((item) => item.trim()).filter(Boolean),
        subject: payload.subject,
        body_text: payload.body,
        body_html: payload.bodyHtml ?? `<p>${payload.body.replace(/\n/g, '<br/>')}</p>`,
      }),
    });

    const data = res?.data ?? res;
    return { data };
  }

  async sendDraft(mailboxId: string, draftId: string, payload: { from: string; to: string; cc?: string; bcc?: string; subject: string; body: string; bodyHtml?: string }): Promise<{ data: EmailMessage }> {
    const res = await this.request<any>(`/mailboxes/${mailboxId}/drafts/${draftId}/send`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        from: payload.from,
        to: payload.to.split(',').map((item) => item.trim()).filter(Boolean),
        cc: (payload.cc ?? '').split(',').map((item) => item.trim()).filter(Boolean),
        bcc: (payload.bcc ?? '').split(',').map((item) => item.trim()).filter(Boolean),
        subject: payload.subject,
        body_text: payload.body,
        body_html: payload.bodyHtml ?? `<p>${payload.body.replace(/\n/g, '<br/>')}</p>`,
      }),
    });

    return { data: res?.data ?? res };
  }

  async discardDraft(mailboxId: string, draftId: string): Promise<void> {
    await this.request(`/mailboxes/${mailboxId}/drafts/${draftId}`, { method: 'DELETE' });
  }

  async uploadDraftAttachments(mailboxId: string, draftId: string, files: File[]): Promise<{ data: EmailAttachment[] }> {
    const form = new FormData();
    for (const file of files) {
      form.append('files', file);
    }

    const path = `/mailboxes/${mailboxId}/drafts/${draftId}/attachments`;
    // Use this.request so the shared Idempotency-Key header is injected for POST.
    const json = await this.request<any>(path, {
      method: 'POST',
      body: form,
    });

    const raw = json?.data ?? json;
    const attachments = (raw ?? []).map(normalizeEmailAttachment);

    return { data: attachments };
  }

  async deleteDraftAttachment(mailboxId: string, draftId: string, attachmentId: string): Promise<void> {
    await this.request(`/mailboxes/${mailboxId}/drafts/${draftId}/attachments/${attachmentId}`, { method: 'DELETE' });
  }

  async getSignatures(): Promise<{ data: MailSignature[] }> {
    const res = await this.request<any>('/mail/signatures');
    const raw = this.normalizeArray<any>(res).data;
    return {
      data: raw.map((item) => ({
        id: item.id,
        name: item.name,
        contentText: item.contentText ?? item.content_text ?? '',
        contentHtml: item.contentHtml ?? item.content_html ?? null,
        isDefault: !!(item.isDefault ?? item.is_default),
      })),
    };
  }

  async saveSignature(signature: Partial<MailSignature> & { name: string; contentText: string }): Promise<{ data: MailSignature }> {
    const path = signature.id ? `/mail/signatures/${signature.id}` : '/mail/signatures';
    const res = await this.request<any>(path, {
      method: signature.id ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: signature.name,
        content_text: signature.contentText,
        content_html: signature.contentHtml ?? null,
        is_default: !!signature.isDefault,
      }),
    });
    const item = res?.data ?? res;
    return {
      data: {
        id: item.id,
        name: item.name,
        contentText: item.contentText ?? item.content_text ?? '',
        contentHtml: item.contentHtml ?? item.content_html ?? null,
        isDefault: !!(item.isDefault ?? item.is_default),
      },
    };
  }

  async deleteSignature(signatureId: string): Promise<void> {
    await this.request(`/mail/signatures/${signatureId}`, { method: 'DELETE' });
  }

  async setDefaultSignature(signatureId: string): Promise<void> {
    await this.request(`/mail/signatures/${signatureId}/default`, { method: 'PUT' });
  }

  async getContacts(): Promise<{ data: Contact[] }> {
    const res = await this.request<any>('/contacts');
    return this.normalizeArray<Contact>(res);
  }

  async getCalendarEvents(): Promise<{ data: CalendarEvent[] }> {
    const res = await this.request<any>('/calendar/events');
    return this.normalizeArray<CalendarEvent>(res);
  }

  async getSieveRules(): Promise<{ data: SieveFilterRule[] }> {
    const res = await this.request<any>('/mail/rules');
    return this.normalizeArray<SieveFilterRule>(res);
  }
}

export const webmailClient = new WebmailApiClient();
