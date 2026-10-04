import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react';
import { QuarantineScreen } from './QuarantineScreen';
import { AdminApiClient } from '../api/client';
import { QuarantineItem } from '../types';

const makeItem = (overrides: Partial<QuarantineItem> = {}): QuarantineItem => ({
  id: 'q-1',
  sender: 'spammer@bad.example',
  recipient: 'victim@miautrix.local',
  subject: 'VIAGRA WINNER',
  spam_score: 10.5,
  threshold: 5.0,
  reasons_json: '[{"RuleName":"SPAM_KEYWORDS","Score":7.5,"Reason":"Suspicious keywords matched"},{"RuleName":"EXCESSIVE_PUNCTUATION","Score":1.5,"Reason":"Excessive punctuation detected"},{"RuleName":"ALL_CAPS_SUBJECT","Score":2,"Reason":"Subject is mostly uppercase"}]',
  status: 'quarantined',
  quarantined_at: '2026-09-18T12:00:00Z',
  released_at: undefined,
  ...overrides,
});

describe('QuarantineScreen', () => {
  it('fetches quarantine on load and filters via status chip', async () => {
    const client = new AdminApiClient('http://test') as unknown as {
      getQuarantine: (params: any) => Promise<any>;
    };

    const getQuarantine = vi
      .spyOn(client as any, 'getQuarantine')
      .mockResolvedValue({ data: [makeItem()] });

    render(<QuarantineScreen client={client as any} domainFilter={'*'} />);

    await waitFor(() => {
      expect(screen.getByTestId('quarantine-table')).toBeInTheDocument();
    });

    expect(getQuarantine).toHaveBeenCalledTimes(1);
    expect(getQuarantine).toHaveBeenCalledWith(expect.objectContaining({
      status: undefined,
      search: undefined,
      from: expect.any(String),
      to: expect.any(String),
    }));

    fireEvent.click(screen.getByTestId('filter-quarantine-quarantined'));

    await waitFor(() => {
      expect(getQuarantine).toHaveBeenCalledTimes(2);
    });
    expect(getQuarantine).toHaveBeenLastCalledWith(expect.objectContaining({
      status: 'quarantined',
      search: undefined,
      from: expect.any(String),
      to: expect.any(String),
    }));
  });

  it('opens inspect modal and triggers release/discard', async () => {
    const client = new AdminApiClient('http://test') as unknown as {
      getQuarantine: (params: any) => Promise<any>;
      releaseQuarantine: (id: string) => Promise<any>;
      deleteQuarantine: (id: string) => Promise<any>;
    };

    const items = [makeItem({ id: 'q-2' })];

    // status/controls under test now expect row-level buttons: Inspect, Release, Discard
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    vi.spyOn(client as any, 'getQuarantine').mockResolvedValue({ data: items });

    const releaseQuarantine = vi.spyOn(client as any, 'releaseQuarantine').mockResolvedValue({
      success: true,
      message: 'released',
    });


    const deleteQuarantine = vi.spyOn(client as any, 'deleteQuarantine').mockResolvedValue({});

    // Avoid blocking confirm dialogs
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    render(<QuarantineScreen client={client as any} domainFilter={'*'} />);

    await waitFor(() => {
      expect(screen.getByText('VIAGRA WINNER')).toBeInTheDocument();
    });

    expect(screen.getByText('Release')).toBeInTheDocument();
    expect(screen.queryByText('Discard')).not.toBeInTheDocument();
    expect(screen.queryByText('Deliver')).not.toBeInTheDocument();
    expect(screen.queryByText('Remove')).not.toBeInTheDocument();
    expect(screen.queryByText('Block Domain')).not.toBeInTheDocument();

    fireEvent.click(screen.getByText('Inspect'));

    await waitFor(() => {
      expect(screen.getByTestId('quarantine-inspect-modal')).toBeInTheDocument();
    });

    // Avoid ambiguous label matches (table header + modal label).
    const modal = await screen.findByTestId('quarantine-inspect-modal');
    const modalScope = within(modal);
    expect(modalScope.getByText(items[0].sender)).toBeInTheDocument();

    // sender/recipient appear in both table row + modal.
    expect(screen.getAllByText(items[0].recipient).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(items[0].recipient).length).toBeGreaterThanOrEqual(1);

    expect(screen.getAllByText(items[0].subject || '').length).toBeGreaterThanOrEqual(1);

    // sender label text is present in modal (table has same word).
    expect(modalScope.getByText('Sender')).toBeInTheDocument();
    expect(screen.getAllByText('SPAM_KEYWORDS').length).toBeGreaterThanOrEqual(1);
    expect(screen.queryByText('[object Object]')).not.toBeInTheDocument();

    fireEvent.click(screen.getByText('Release'));

    await waitFor(() => {
      expect(releaseQuarantine).toHaveBeenCalledWith('q-2');
    });

    // Hit Discard (row action) path.
    fireEvent.click(screen.getByText('Inspect'));

    await waitFor(() => {
      expect(screen.getByTestId('quarantine-inspect-modal')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Discard Message'));

    // After discard, row should move out of the quarantined view in the UI.
    // (backend keeps the row and marks it as Discarded).

    await waitFor(() => {
      expect(deleteQuarantine).toHaveBeenCalledWith('q-2');
    });
  });
});

// NOTE: row-level controls are now limited to Release + Discard; other power actions are in the modal.

