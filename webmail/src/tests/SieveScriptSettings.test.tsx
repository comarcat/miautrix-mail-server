import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SieveScriptSettings, vacationScript } from '../components/SieveScriptSettings';
import { webmailClient } from '../components/WebmailApiClient';

describe('SieveScriptSettings', () => {
  it('shows the filter list and editor', async () => {
    vi.spyOn(webmailClient, 'getSieveScripts').mockResolvedValue([{ id: '1', name: 'Inbox filter', content: 'keep;', isActive: true }]);
    render(<SieveScriptSettings mailboxId="mailbox" />);
    expect(await screen.findByText('Inbox filter')).toBeTruthy();
    expect(screen.getByLabelText('Sieve script')).toBeTruthy();
  });

  it('shows compile error positions from the API', async () => {
    vi.spyOn(webmailClient, 'getSieveScripts').mockResolvedValue([]);
    vi.spyOn(webmailClient, 'saveSieveScript').mockRejectedValue({ message: 'Unexpected token', details: { line: ['3'], column: ['8'] } });
    render(<SieveScriptSettings mailboxId="mailbox" />);
    fireEvent.click(screen.getByText('Save script'));
    expect(await screen.findByRole('alert')).toHaveTextContent('Line 3, column 8');
  });

  it('generates and saves an active vacation script', async () => {
    vi.spyOn(webmailClient, 'getSieveScripts').mockResolvedValue([]);
    vi.spyOn(webmailClient, 'saveSieveScript').mockResolvedValue({ id: 'vacation' });
    const activate = vi.spyOn(webmailClient, 'setSieveScriptActive').mockResolvedValue();
    render(<SieveScriptSettings mailboxId="mailbox" />);
    fireEvent.click(screen.getByLabelText('Out of office'));
    fireEvent.click(screen.getByText('Save out-of-office'));
    await waitFor(() => expect(activate).toHaveBeenCalledWith('mailbox', 'vacation', true));
    expect(vacationScript('Gone', 'Back soon', 90)).toContain('vacation :days 30');
  });
});
