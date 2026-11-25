import { describe, it, expect, vi, beforeEach } from 'vitest';
import { AdminApiClient } from './client';

describe('AdminApiClient', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('sends the bearer token without client supplied identity headers', async () => {
    localStorage.setItem('miautrix_admin_token', 'token');
    localStorage.setItem('miautrix_admin_tenant_id', 'all');
    localStorage.setItem('miautrix_admin_user_id', 'undefined');

    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ data: [], meta: { next_cursor: null, has_more: false } }),
    } as Response);

    const client = new AdminApiClient('http://test');
    await client.getQueue({ status: 'queued' });

    const [, init] = fetchMock.mock.calls[0];
    const headers = init?.headers as Record<string, string>;

    expect(headers.Authorization).toBe('Bearer token');
    expect(headers['X-Tenant-Id']).toBeUndefined();
    expect(headers['X-User-Id']).toBeUndefined();
  });

  it('does not send tenant and user identity headers', async () => {
    const tenantId = '11111111-1111-4111-8111-111111111111';
    const userId = '22222222-2222-4222-8222-222222222222';
    localStorage.setItem('miautrix_admin_tenant_id', ` ${tenantId} `);
    localStorage.setItem('miautrix_admin_user_id', userId);

    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ data: [], meta: { next_cursor: null, has_more: false } }),
    } as Response);

    const client = new AdminApiClient('http://test');
    await client.getQueue({ status: 'queued' });

    const [, init] = fetchMock.mock.calls[0];
    const headers = init?.headers as Record<string, string>;

    expect(headers['X-Tenant-Id']).toBeUndefined();
    expect(headers['X-User-Id']).toBeUndefined();
  });

  it('refreshes once after 401 and retries the request with the rotated bearer token', async () => {
    localStorage.setItem('miautrix_admin_token', 'old-access');
    localStorage.setItem('miautrix_admin_refresh_token', 'old-refresh');
    const fetchMock = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce({ status: 401, ok: false } as Response)
      .mockResolvedValueOnce({ status: 200, ok: true, json: async () => ({ token: 'new-access', refresh_token: 'new-refresh' }) } as Response)
      .mockResolvedValueOnce({ status: 200, ok: true, json: async () => ({ data: [] }) } as Response);
    const client = new AdminApiClient('http://test');
    await client.getQueue();
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect((fetchMock.mock.calls[0][1]?.headers as Record<string, string>).Authorization).toBe('Bearer old-access');
    expect(fetchMock.mock.calls[1][0]).toBe('http://test/auth/refresh');
    expect((fetchMock.mock.calls[2][1]?.headers as Record<string, string>).Authorization).toBe('Bearer new-access');
    expect(localStorage.getItem('miautrix_admin_refresh_token')).toBe('new-refresh');
  });

  it('clears auth and dispatches the login redirect event when refresh fails', async () => {
    localStorage.setItem('miautrix_admin_token', 'expired');
    localStorage.setItem('miautrix_admin_refresh_token', 'invalid-refresh');
    const expired = vi.fn();
    window.addEventListener('miautrix:auth:expired', expired);
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce({ status: 401, ok: false } as Response)
      .mockResolvedValueOnce({ status: 401, ok: false } as Response);
    const client = new AdminApiClient('http://test');
    await expect(client.getQueue()).rejects.toThrow('Unauthorized');
    expect(localStorage.getItem('miautrix_admin_token')).toBeNull();
    expect(localStorage.getItem('miautrix_admin_refresh_token')).toBeNull();
    expect(expired).toHaveBeenCalledOnce();
    window.removeEventListener('miautrix:auth:expired', expired);
  });

  it('sends bearer auth when fetching a mailbox archive', async () => {
    localStorage.setItem('miautrix_admin_token', 'archive-token');
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      status: 200,
      ok: true,
      blob: async () => new Blob(['archive']),
    } as Response);
    const client = new AdminApiClient('http://test');
    await client.exportMailbox('mailbox-id');
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect((fetchMock.mock.calls[0][1]?.headers as Record<string, string>).Authorization).toBe('Bearer archive-token');
  });
});
