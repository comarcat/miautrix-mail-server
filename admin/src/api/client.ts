import { ApiResponse, QueueItem, QueueQueryParams } from '../types';

export class AdminApiClient {
  private baseUrl: string;

  constructor(baseUrl: string = '/api/v1') {
    this.baseUrl = baseUrl;
  }

  async getQueue(params: QueueQueryParams = {}): Promise<ApiResponse<QueueItem[]>> {
    const query = new URLSearchParams();
    if (params.cursor) query.set('cursor', params.cursor);
    if (params.limit) query.set('limit', params.limit.toString());
    if (params.status && params.status !== 'all') query.set('status', params.status);
    if (params.search) query.set('search', params.search);

    const qs = query.toString();
    const url = `${this.baseUrl}/mail/queue${qs ? `?${qs}` : ''}`;

    const res = await fetch(url, {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
      },
    });

    if (!res.ok) {
      throw new Error(`Failed to fetch mail queue: ${res.status} ${res.statusText}`);
    }

    return res.json();
  }

  async retryQueueItem(id: string): Promise<{ success: boolean; message: string }> {
    const res = await fetch(`${this.baseUrl}/mail/queue/${id}/retry`, {
      method: 'POST',
      headers: {
        'Accept': 'application/json',
      },
    });
    if (!res.ok) throw new Error(`Retry failed: ${res.status}`);
    return res.json();
  }

  async deleteQueueItem(id: string): Promise<{ success: boolean }> {
    const res = await fetch(`${this.baseUrl}/mail/queue/${id}`, {
      method: 'DELETE',
    });
    if (!res.ok) throw new Error(`Delete failed: ${res.status}`);
    return res.json();
  }

  async simulateRule(payload: {
    rule: unknown;
    sampleMessage: { sender: string; recipient: string; subject: string; headers: Record<string, string>; hasAttachment: boolean; spamScore?: number };
  }): Promise<{ matched: boolean; actionsTaken: string[]; score: number; log: string[] }> {
    const res = await fetch(`${this.baseUrl}/mail/rules/simulate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    if (!res.ok) throw new Error(`Simulation failed: ${res.status}`);
    return res.json();
  }
}

export const apiClient = new AdminApiClient();
