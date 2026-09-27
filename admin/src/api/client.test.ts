import { describe, it, expect, vi, beforeEach } from 'vitest';
import { AdminApiClient } from './client';

describe('AdminApiClient', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('does not send malformed tenant or user headers', async () => {
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

  it('sends valid tenant and user GUID headers', async () => {
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

    expect(headers['X-Tenant-Id']).toBe(tenantId);
    expect(headers['X-User-Id']).toBe(userId);
  });
});
