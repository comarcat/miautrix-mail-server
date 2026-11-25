import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AdminApiClient } from '../api/client';
import { DomainsScreen } from './DomainsScreen';

const makeClient = () => {
  const client = new AdminApiClient('http://test') as any;
  vi.spyOn(client, 'getDomains').mockResolvedValue({ data: [{
    id: 'domain-1', name: 'example.test', is_verified: true, is_primary: true,
    created_at: '2026-01-01T00:00:00Z', transport_mode: 'local', mta_sts_mode: 'testing',
    mta_sts_max_age: 604800, tls_report_rua: 'mailto:tls@example.test', mta_sts_id: 'policy-42',
  }] });
  vi.spyOn(client, 'updateDomain').mockResolvedValue({ data: {} });
  return client;
};

describe('DomainsScreen discovery policies', () => {
  it('shows MTA-STS mode options and max age in the domain editor', async () => {
    render(<DomainsScreen client={makeClient()} />);
    await screen.findByText('example.test');
    fireEvent.click(screen.getByRole('button', { name: /^edit$/i }));
    expect(screen.getByLabelText(/mta-sts policy/i)).toHaveValue('testing');
    expect(screen.getByRole('option', { name: /testing/i })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /enforce/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/policy max_age/i)).toHaveValue(604800);
  });

  it('displays copyable SRV, CNAME, MTA-STS and TLS-RPT suggestions', async () => {
    render(<DomainsScreen client={makeClient()} />);
    await screen.findByText('example.test');
    fireEvent.click(screen.getByRole('button', { name: /dns config/i }));
    await waitFor(() => expect(screen.getByText(/_imaps\._tcp\.example\.test/)).toBeInTheDocument());
    expect(screen.getByText(/_submissions\._tcp\.example\.test/)).toBeInTheDocument();
    expect(screen.getByText(/autodiscover\.example\.test/)).toBeInTheDocument();
    expect(screen.getByText(/_mta-sts\.example\.test.*policy-42/s)).toBeInTheDocument();
    expect(screen.getByText(/_smtp\._tls\.example\.test.*tls@example\.test/s)).toBeInTheDocument();
  });
});
