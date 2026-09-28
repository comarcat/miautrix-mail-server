import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import App from '../App';
import { webmailClient } from '../components/WebmailApiClient';

describe('Webmail SPA UI Flow', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('renders login screen when unauthenticated and logs in successfully', async () => {
    vi.spyOn(webmailClient, 'login').mockResolvedValue({
      data: { id: 'u1', email: 'alex.vance@miautrix.org' },
      token: 'mock-jwt-token',
    });

    vi.spyOn(webmailClient, 'me').mockResolvedValue({
      data: {
        id: 'u1',
        email: 'alex.vance@miautrix.org',
        must_change_password: false,
      },
    });

    render(<App />);

    expect(screen.getByText('Miautrix Webmail')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('user@yourdomain.com')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText(/email address/i), {
      target: { value: 'alex.vance@miautrix.org' },
    });
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: 'password123' },
    });

    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
    });
  });

  it('renders change password form when must_change_password is true after login', async () => {
    vi.spyOn(webmailClient, 'login').mockResolvedValue({
      data: { id: 'u1', email: 'alex.vance@miautrix.org' },
      token: 'mock-jwt-token',
    });

    const meSpy = vi.spyOn(webmailClient, 'me');
    meSpy
      .mockResolvedValueOnce({
        data: {
          id: 'u1',
          email: 'alex.vance@miautrix.org',
          must_change_password: true,
        },
      })
      .mockResolvedValueOnce({
        data: {
          id: 'u1',
          email: 'alex.vance@miautrix.org',
          must_change_password: false,
        },
      });

    vi.spyOn(webmailClient, 'changePassword').mockResolvedValue({ data: {} });

    render(<App />);

    fireEvent.change(screen.getByLabelText(/email address/i), {
      target: { value: 'alex.vance@miautrix.org' },
    });
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: 'password123' },
    });

    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /change password/i })).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText(/current password/i), {
      target: { value: 'old-password' },
    });
    fireEvent.change(screen.getByLabelText(/new password/i), {
      target: { value: 'new-password' },
    });

    fireEvent.click(screen.getByRole('button', { name: /update password/i }));

    await waitFor(() => {
      expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
    });

    expect(meSpy).toHaveBeenCalled();
  });

  describe('Authenticated Session', () => {
    beforeEach(() => {
      vi.spyOn(webmailClient, 'getToken').mockReturnValue('valid-mock-token');
      vi.spyOn(webmailClient, 'me').mockResolvedValue({
        data: { id: 'u1', email: 'alex.vance@miautrix.org' },
      });
      vi.spyOn(webmailClient, 'getMailboxes').mockResolvedValue({
        data: [{ id: 'mb1', address: 'alex.vance@miautrix.org', name: 'Alex Vance', kind: 'user', accessLevel: 'write', quotaBytes: 0, usedBytes: 0 } as any],
      });
      vi.spyOn(webmailClient, 'getFolders').mockResolvedValue({
        data: [{ id: 'inbox', name: 'Inbox', role: 'inbox', unreadEmails: 1, totalEmails: 1 } as any],
      });
      vi.spyOn(webmailClient, 'getMessages').mockResolvedValue({
        data: [{
          id: 'm1',
          mailboxId: 'mb1',
          folderId: 'inbox',
          from: { name: 'Miautrix Security Ops', email: 'security@miautrix.org' },
          to: [{ name: 'Alex Vance', email: 'alex.vance@miautrix.org' }],
          subject: 'Quarterly TLS & Security Audit Completed',
          snippet: 'Security audit summary',
          bodyHtml: '<p>Security audit summary</p>',
          receivedAt: '2026-09-18T10:00:00Z',
          isUnread: true,
          securityChecks: { spfPass: true, dkimPass: true, dmarcPass: true },
          attachments: [],
        } as any],
      });
      vi.spyOn(webmailClient, 'getContacts').mockResolvedValue({
        data: [
          { id: 'c1', name: 'Miautrix Postmaster', email: 'postmaster@miautrix.org', organization: 'Miautrix', book: 'personal' },
          { id: 'c2', name: 'Security Team', email: 'security-team@miautrix.org', organization: 'Miautrix', book: 'directory', kind: 'group' },
        ],
      });
      vi.spyOn(webmailClient, 'getCalendarEvents').mockResolvedValue({ data: [] });
      vi.spyOn(webmailClient, 'getSieveRules').mockResolvedValue({
        data: [{ id: 'r1', name: 'Move Jira Notifications', field: 'from', comparator: 'contains', value: 'jira', action: 'fileinto', targetFolder: 'Jira', active: true }],
      });
    });

    it('renders webmail navigation tabs and default inbox messages', async () => {
      render(<App />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
      });

      expect(screen.getAllByText('Quarterly TLS & Security Audit Completed')[0]).toBeInTheDocument();

      // Reading pane renders the sender identity and the SPF/DKIM verification badge
      const readingPane = screen.getByRole('main', { name: /reading pane/i });
      expect(readingPane.textContent).toContain('Miautrix Security Ops');
      expect(readingPane.textContent).toContain('security@miautrix.org');
      expect(readingPane.textContent).toContain('SPF & DKIM Valid');
      expect(readingPane.textContent).toContain('To: alex.vance@miautrix.org');
    });

    it('navigates to Compose view when New Message is clicked', async () => {
      render(<App />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
      });

      const newMsgBtn = screen.getAllByText(/new message/i)[0];
      fireEvent.click(newMsgBtn);

      expect(screen.getByPlaceholderText('Add a subject')).toBeInTheDocument();
      expect(screen.getByPlaceholderText('Enter recipients...')).toBeInTheDocument();
      fireEvent.click(screen.getAllByRole('button', { name: 'Contacts' }).at(-1)!);
      expect(screen.getByRole('dialog', { name: /select to recipients/i })).toBeInTheDocument();
      expect(screen.getByText('Security Team')).toBeInTheDocument();
      fireEvent.click(screen.getByLabelText(/security-team@miautrix.org/i));
      fireEvent.click(screen.getByRole('button', { name: /add selected/i }));
      expect(screen.getByPlaceholderText('Enter recipients...')).toHaveValue('security-team@miautrix.org');
    });

    it('navigates to Contacts view and allows searching contacts', async () => {
      render(<App />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
      });

      const contactsTab = screen.getByRole('button', { name: /contacts/i });
      fireEvent.click(contactsTab);

      expect(screen.getByRole('heading', { level: 1, name: /personal contacts/i })).toBeInTheDocument();
      expect(screen.getByText('Miautrix Postmaster')).toBeInTheDocument();
    });

    it('navigates to Settings & Rules to manage ManageSieve filters', async () => {
      render(<App />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
      });

      const rulesTab = screen.getByRole('button', { name: /settings & rules/i });
      fireEvent.click(rulesTab);

      expect(screen.getByRole('heading', { level: 1, name: /managesieve filter rules/i })).toBeInTheDocument();
      expect(screen.getAllByText('Move Jira Notifications')[0]).toBeInTheDocument();
    });
  });
});
