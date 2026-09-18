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
  });

  it('issues exactly one request per screen load (no N+1)', async () => {
    const getQueue = vi
      .spyOn(client, 'getQueue')
      .mockResolvedValue(makeResponse([makeItem(), makeItem({ id: 'q-2', message_id: 'msg-2' })]));

    render(<QueueScreen client={client} />);

    await waitFor(() => {
      expect(screen.getByTestId('queue-table')).toBeInTheDocument();
    });

    // The screen must issue exactly one request for the initial load,
    // regardless of how many rows it renders.
    expect(getQueue).toHaveBeenCalledTimes(1);
    expect(getQueue).toHaveBeenCalledWith(
      expect.objectContaining({ limit: 20 })
    );
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

    // Typing in the search box alone must not trigger additional requests.
    fireEvent.change(screen.getByLabelText('Search queue items'), {
      target: { value: 'bob' },
    });

    expect(getQueue).toHaveBeenCalledTimes(1);
  });

  it('paginates with cursor and does not re-request on non-cursor interactions', async () => {
    const getQueue = vi
      .spyOn(client, 'getQueue')
      .mockResolvedValueOnce(
        makeResponse([makeItem()], 'cursor-2', true)
      )
      .mockResolvedValueOnce(
        makeResponse([makeItem({ id: 'q-2', message_id: 'msg-2' })], null, false)
      );

    render(<QueueScreen client={client} />);

    await waitFor(() => {
      expect(screen.getByTestId('queue-table')).toBeInTheDocument();
    });

    // Load page 2 via the Next control.
    fireEvent.click(screen.getByTestId('pagination-next'));

    await waitFor(() => {
      expect(getQueue).toHaveBeenCalledTimes(2);
    });

    expect(getQueue).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({ cursor: 'cursor-2' })
    );
  });
});
