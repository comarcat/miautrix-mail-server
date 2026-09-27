import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { AntiSpamScreen } from './AntiSpamScreen';
import { AdminApiClient } from '../api/client';

const makeClient = () => {
  const client = new AdminApiClient('http://test') as any;
  vi.spyOn(client, 'getDomains').mockResolvedValue({
    data: [{ id: 'domain-1', name: 'miautrix.tech', is_verified: true, is_primary: true, created_at: '2026-09-23T00:00:00Z', transport_mode: 'local' }],
  });
  vi.spyOn(client, 'getDomainAntiSpamSettings').mockResolvedValue({
    data: {
      reject_score: 14,
      quarantine_score: 10,
      header_score: 6,
      greylist_score: 4,
      greylisting_enabled: true,
      spf_dmarc_enforcement_enabled: true,
    },
  });
  vi.spyOn(client, 'updateDomainAntiSpamSettings').mockResolvedValue({
    data: {
      reject_score: 15,
      quarantine_score: 10,
      header_score: 6,
      greylist_score: 4,
      greylisting_enabled: false,
      spf_dmarc_enforcement_enabled: true,
    },
  });
  return client;
};

describe('AntiSpamScreen', () => {
  it('loads and saves anti-spam policy per domain', async () => {
    const client = makeClient();

    render(<AntiSpamScreen client={client} selectedDomain="miautrix.tech" />);

    await waitFor(() => {
      expect(client.getDomainAntiSpamSettings).toHaveBeenCalledWith('domain-1');
    });

    const greylisting = screen.getByRole('checkbox', { name: /dynamic greylisting/i });
    fireEvent.click(greylisting);

    const btn = screen.getByRole('button', { name: /apply anti-spam policy/i });
    fireEvent.click(btn);

    await waitFor(() => {
      expect(client.updateDomainAntiSpamSettings).toHaveBeenCalledWith('domain-1', expect.objectContaining({
        reject_score: 14,
        quarantine_score: 10,
        header_score: 6,
        greylist_score: 4,
        greylisting_enabled: false,
        spf_dmarc_enforcement_enabled: true,
      }));
    });

    expect(screen.getByText(/anti-spam policy saved/i)).toBeInTheDocument();
  });

});
