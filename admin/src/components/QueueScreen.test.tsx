import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { QueueScreen } from './QueueScreen';
import { AdminApiClient } from '../api/client';
import { ApiResponse, QueueItem } from '../types';

const makeItem = (overrides: Partial<QueueItem> = {}): QueueItem => ({
  id: 'q-1',
  message_id: 'msg-1',
  sender: 'alice@example.com',
  recipient: 'bob@example.com',
  size_bytes: 2048,
  status: 'queued',
  attempts: 2,
  next_retry_at: '2026-09-18T12:00:00Z',
  created_at: '2026-09-18T11:00:00Z',
  ...overrides,
});

const makeResponse = (
  items: QueueItem[],
  nextCursor: string | null = null,
  hasMore = false
): ApiResponse<QueueItem[]> => ({
  data: items,
  meta: { next_cursor: nextCursor, has_more: hasMore },
});

describe('QueueScreen', () => {
  let client: AdminApiClient;
  const today = new Date().toISOString().slice(0, 10);

  beforeEach(() => {
    client = new AdminApiClient('http://test');
    vi.restoreAllMocks();
  });

  it('renders queue items returned by the API', async () => {
    const getQueue = vi
      .spyOn(client, 'getQueue')
      .mockResolvedValue(makeResponse([makeItem()]));

    render(<QueueScreen client={client} />);

    await waitFor(() => {
      expect(screen.getByTestId('queue-table')).toBeInTheDocument();
    });

    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
    expect(getQueue).toHaveBeenCalledTimes(1);

    const callArg = getQueue.mock.calls[0][0] as any;
    expect(callArg.start_at).toBe(`${today}T00:00:01`);
    expect(callArg.end_at).toBe(`${today}T23:59:59`);
    expect(callArg.domain).toBe('all');
  });

  it('issues exactly one request per screen load (no N+1)', async () => {
    const getQueue = vi
      .spyOn(client, 'getQueue')
      .mockResolvedValue(
        makeResponse([makeItem(), makeItem({ id: 'q-2', message_id: 'msg-2' })])
      );

    render(<QueueScreen client={client} />);

    await waitFor(() => {
      expect(screen.getByTestId('queue-table')).toBeInTheDocument();
    });

    expect(getQueue).toHaveBeenCalledTimes(1);

    const callArg = getQueue.mock.calls[0][0] as any;
    expect(callArg).toEqual(expect.objectContaining({ limit: 20 }));
    expect(callArg.start_at).toBe(`${today}T00:00:01`);
    expect(callArg.end_at).toBe(`${today}T23:59:59`);
  });

  it('does not fire extra requests when filters change without submit', async () => {
    const getQueue = vi
      .spyOn(client, 'getQueue')
      .mockResolvedValue(makeResponse([makeItem()]));

    render(<QueueScreen client={client} />);

    await waitFor(() => {
      expect(screen.getByTestId('queue-table')).toBeInTheDocument();
    });

    expect(getQueue).toHaveBeenCalledTimes(1);

    fireEvent.change(screen.getByLabelText('Search queue items'), {
      target: { value: 'bob' },
    });

    expect(getQueue).toHaveBeenCalledTimes(1);
  });

  it('paginates with cursor and does not re-request on non-cursor interactions', async () => {
    const getQueue = vi
      .spyOn(client, 'getQueue')
      .mockResolvedValueOnce(makeResponse([makeItem()], 'cursor-2', true))
      .mockResolvedValueOnce(
        makeResponse([makeItem({ id: 'q-2', message_id: 'msg-2' })], null, false)
      );

    render(<QueueScreen client={client} />);

    await waitFor(() => {
      expect(screen.getByTestId('queue-table')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('pagination-next'));

    await waitFor(() => {
      expect(getQueue).toHaveBeenCalledTimes(2);
    });

    expect(getQueue).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({ cursor: 'cursor-2' })
    );
  });

  it('sends start_at/end_at/domain filters to the API', async () => {
    const getQueue = vi
      .spyOn(client, 'getQueue')
      .mockResolvedValue(makeResponse([makeItem()]));

    render(<QueueScreen client={client} domainFilter="example.com" />);

    await waitFor(() => {
      expect(screen.getByTestId('queue-table')).toBeInTheDocument();
    });

    expect(getQueue).toHaveBeenCalledTimes(1);
    const callArg = getQueue.mock.calls[0][0] as any;

    expect(callArg.domain).toBe('example.com');
    expect(callArg.start_at).toBe(`${today}T00:00:01`);
    expect(callArg.end_at).toBe(`${today}T23:59:59`);
  });

  it('resets cursor history when status/search/domain/date filters change', async () => {
    const getQueue = vi
      .spyOn(client, 'getQueue')
      .mockResolvedValueOnce(makeResponse([makeItem()], 'cursor-2', true))
      .mockResolvedValueOnce(
        makeResponse([makeItem({ id: 'q-2', message_id: 'msg-2' })], null, false)
      )
      .mockResolvedValueOnce(makeResponse([makeItem({ id: 'q-3' })]));

    render(<QueueScreen client={client} domainFilter="all" />);

    await waitFor(() => {
      expect(screen.getByTestId('queue-table')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('pagination-next'));

    await waitFor(() => {
      expect(getQueue).toHaveBeenCalledTimes(2);
    });

    fireEvent.click(screen.getByTestId('filter-queued'));

    await waitFor(() => {
      expect(getQueue).toHaveBeenCalledTimes(3);
    });

    const thirdCallArg = getQueue.mock.calls[2][0] as any;
    expect(thirdCallArg.cursor).toBeUndefined();
  });

  it('resets cursor history when start/end dates change', async () => {
    const getQueue = vi
      .spyOn(client, 'getQueue')
      .mockResolvedValue(makeResponse([makeItem()]));

    render(<QueueScreen client={client} domainFilter="all" />);

    await waitFor(() => {
      expect(screen.getByTestId('queue-table')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('pagination-next'));
    // No-op: hasMore=false in this test setup.

    fireEvent.change(screen.getByLabelText('Start date'), {
      target: { value: '2026-09-01' },
    });

    await waitFor(() => {
      expect(getQueue).toHaveBeenCalledTimes(2);
    });

    const secondCallArg = getQueue.mock.calls[1][0] as any;
    expect(secondCallArg.start_at).toBe('2026-09-01T00:00:01');
    expect(secondCallArg.end_at).toBe(`${today}T23:59:59`);

    fireEvent.change(screen.getByLabelText('End date'), {
      target: { value: '2026-09-02' },
    });

    await waitFor(() => {
      expect(getQueue).toHaveBeenCalledTimes(3);
    });

    const thirdCallArg = getQueue.mock.calls[2][0] as any;
    expect(thirdCallArg.end_at).toBe('2026-09-02T23:59:59');
  });
});
