import { beforeEach, describe, expect, it, vi } from 'vitest';
import { WebmailApiClient } from '../components/WebmailApiClient';

describe('WebmailApiClient session authentication', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('refreshes once and retries an authenticated JSON request', async () => {
    localStorage.setItem('miautrix_webmail_token', 'old-access');
    localStorage.setItem('miautrix_webmail_refresh_token', 'old-refresh');
    const fetchMock = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce({ status: 401, ok: false } as Response)
      .mockResolvedValueOnce({ status: 200, ok: true, json: async () => ({ token: 'new-access', refresh_token: 'new-refresh' }) } as Response)
      .mockResolvedValueOnce({ status: 200, ok: true, json: async () => ({ data: [] }) } as Response);
    const client = new WebmailApiClient('http://test/api/v1');
    await client.getMailboxes();
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect((fetchMock.mock.calls[0][1]?.headers as Record<string, string>).Authorization).toBe('Bearer old-access');
    expect(fetchMock.mock.calls[1][0]).toBe('http://test/api/v1/auth/refresh');
    expect((fetchMock.mock.calls[2][1]?.headers as Record<string, string>).Authorization).toBe('Bearer new-access');
    expect(localStorage.getItem('miautrix_webmail_refresh_token')).toBe('new-refresh');
  });

  it('sends the session bearer token to the Sieve script API', async () => {
    localStorage.setItem('miautrix_webmail_token', 'sieve-access');
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      status: 200,
      ok: true,
      json: async () => ({ data: [] }),
    } as Response);
    const client = new WebmailApiClient('http://test/api/v1');
    await client.getSieveScripts('mailbox-1');
    expect(fetchMock.mock.calls[0][0]).toBe('http://test/api/v1/mail/sieve/scripts?mailboxId=mailbox-1');
    expect((fetchMock.mock.calls[0][1]?.headers as Record<string, string>).Authorization).toBe('Bearer sieve-access');
  });

  it('uses bearer auth and refresh retry for attachment downloads', async () => {
    localStorage.setItem('miautrix_webmail_token', 'old-access');
    localStorage.setItem('miautrix_webmail_refresh_token', 'old-refresh');
    const fetchMock = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce({ status: 401, ok: false } as Response)
      .mockResolvedValueOnce({ status: 200, ok: true, json: async () => ({ token: 'new-access', refresh_token: 'new-refresh' }) } as Response)
      .mockResolvedValueOnce({ status: 200, ok: true, blob: async () => new Blob(['file']) } as Response);
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:test') });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    const client = new WebmailApiClient('http://test/api/v1');
    await client.downloadAttachment('/api/v1/messages/1/attachments/2', 'file.bin');
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect((fetchMock.mock.calls[0][1]?.headers as Record<string, string>).Authorization).toBe('Bearer old-access');
    expect((fetchMock.mock.calls[2][1]?.headers as Record<string, string>).Authorization).toBe('Bearer new-access');
  });

  it('returns to login after a failed one-time refresh', async () => {
    localStorage.setItem('miautrix_webmail_token', 'expired');
    localStorage.setItem('miautrix_webmail_refresh_token', 'invalid');
    const expired = vi.fn();
    window.addEventListener('miautrix:auth:expired', expired);
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce({ status: 401, ok: false } as Response)
      .mockResolvedValueOnce({ status: 401, ok: false } as Response);
    const client = new WebmailApiClient('http://test/api/v1');
    await expect(client.getMailboxes()).rejects.toThrow('Unauthorized');
    expect(localStorage.getItem('miautrix_webmail_token')).toBeNull();
    expect(localStorage.getItem('miautrix_webmail_refresh_token')).toBeNull();
    expect(expired).toHaveBeenCalledOnce();
    window.removeEventListener('miautrix:auth:expired', expired);
  });
});
