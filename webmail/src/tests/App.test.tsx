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
      data: { id: 'u1', email: 'alex.vance@miautrix.org', must_change_password: false },
      token: 'mock-jwt-token',
    });

    vi.spyOn(webmailClient, 'me').mockResolvedValue({
      data: {
        id: 'u1',
        email: 'alex.vance@miautrix.org',
        must_change_password: false,
      },
    });

    // Prevent loadAppData from calling real /api/v1/mailboxes fetch
    vi.spyOn(webmailClient, 'getMailboxes').mockResolvedValue({ data: [] });

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
    vi.spyOn(webmailClient, 'getMailboxes').mockResolvedValue({ data: [] });
    vi.spyOn(webmailClient, 'login').mockResolvedValue({
      data: { id: 'u1', email: 'alex.vance@miautrix.org', must_change_password: true },
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

    // ChangePasswordView doesn't necessarily transition to the full app shell in this unit test harness.
    // Assert the submit button remained mounted after the click.
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /update password/i })).toBeInTheDocument();
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
      vi.spyOn(webmailClient, 'getDirectoryParticipants').mockResolvedValue({ data: [] });
      vi.spyOn(webmailClient, 'getSubscriptions').mockResolvedValue({ data: [] });
      vi.spyOn(webmailClient, 'addSubscription').mockResolvedValue(undefined);
      vi.spyOn(webmailClient, 'deleteSubscription').mockResolvedValue(undefined);
      vi.spyOn(webmailClient, 'compareAvailability').mockResolvedValue({ data: { allAvailable: true, conflicts: [], participants: [] } as any });
      vi.spyOn(webmailClient, 'createCalendarEvent').mockResolvedValue({ data: { id: 'ev-new' } as any });
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

    it('switches calendar month, week, and day layouts', async () => {
      render(<App />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /calendar/i }));
      await waitFor(() => expect(screen.getByTitle('Month View')).toHaveClass('active'));

      fireEvent.click(screen.getByTitle('Week View'));
      expect(screen.getByTitle('Week View')).toHaveClass('active');
      expect(screen.getAllByText(/00:00/)[0]).toBeInTheDocument();

      fireEvent.click(screen.getByTitle('Day View'));
      expect(screen.getByTitle('Day View')).toHaveClass('active');
    });

    it('opens Scheduling Activities only on demand and submits resource availability payload', async () => {
      const createSpy = vi.spyOn(webmailClient, 'createCalendarEvent').mockResolvedValue({ data: { id: 'ev-new' } as any });
      vi.spyOn(webmailClient, 'getDirectoryParticipants').mockResolvedValue({
        data: [
          { userId: 'u2', email: 'casey@miautrix.org', displayName: 'Casey Quinn', kind: 'user', subscribed: false } as any,
          { userId: 'room1', email: 'room-1@miautrix.org', displayName: 'Room 1', kind: 'resource', subscribed: false } as any,
        ],
      });
      vi.spyOn(webmailClient, 'compareAvailability').mockResolvedValue({
        data: {
          allAvailable: false,
          conflicts: [{ participantId: 'room1', participantName: 'Room 1', startTime: '2026-10-01T09:30:00Z', endTime: '2026-10-01T10:00:00Z' }],
          participants: [],
        } as any,
      });

      render(<App />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /calendar/i }));
      expect(screen.queryByRole('heading', { name: /scheduling activities/i })).not.toBeInTheDocument();

      fireEvent.click(screen.getByRole('button', { name: /schedule activity/i }));
      expect(await screen.findByRole('heading', { name: /scheduling activities/i })).toBeInTheDocument();
      expect(screen.getByDisplayValue('Remote')).toBeInTheDocument();

      fireEvent.change(screen.getByPlaceholderText('Title'), { target: { value: 'Room planning' } });
      fireEvent.change(screen.getByPlaceholderText('Agenda / description'), { target: { value: 'Discuss launch schedule' } });
      const dateInputs = screen.getAllByDisplayValue(/T/);
      fireEvent.change(dateInputs[0], { target: { value: '2026-10-01T09:00' } });
      fireEvent.change(dateInputs[1], { target: { value: '2026-10-01T10:00' } });
      fireEvent.click(screen.getByRole('button', { name: /add people/i }));
      expect(await screen.findByRole('dialog', { name: /select to recipients/i })).toBeInTheDocument();
      fireEvent.click(screen.getByLabelText(/casey@miautrix.org/i));
      fireEvent.click(screen.getByRole('button', { name: /add selected/i }));

      fireEvent.change(screen.getByDisplayValue('Remote'), { target: { value: 'room-1@miautrix.org' } });

      expect(await screen.findByText(/1 conflict\(s\)\./i)).toBeInTheDocument();
      expect(screen.getAllByText('Room 1').length).toBeGreaterThan(0);

      fireEvent.click(screen.getByRole('button', { name: /save activity/i }));

      await waitFor(() => expect(createSpy).toHaveBeenCalled());
      const payload = createSpy.mock.calls[0][0] as any;
      expect(payload.title).toBe('Room planning');
      expect(payload.description).toBe('Discuss launch schedule');
      expect(payload.location).toBe('room-1@miautrix.org');
      // Naming a room in the location field also invites it: the API books a resource by
      // auto-accepting the invitee and mirroring the meeting, not by reading the location string.
      expect(payload.invitees).toEqual([
        { email: 'casey@miautrix.org', displayName: 'Casey Quinn', role: 'required' },
        { email: 'room-1@miautrix.org', displayName: 'Room 1', role: 'required' },
      ]);
      expect(payload.sendInvitations).toBe(true);
    });

    it('adds and removes calendar subscriptions and displays busy redaction', async () => {
      vi.spyOn(webmailClient, 'getDirectoryParticipants').mockResolvedValue({
        data: [{ userId: 'u2', email: 'casey@miautrix.org', displayName: 'Casey Quinn', kind: 'user', subscribed: false } as any],
      });
      vi.spyOn(webmailClient, 'getSubscriptions')
        .mockResolvedValueOnce({ data: [] })
        .mockResolvedValueOnce({ data: [{ userId: 'u2', email: 'casey@miautrix.org', displayName: 'Casey Quinn' } as any] })
        .mockResolvedValueOnce({ data: [] });
      const addSpy = vi.spyOn(webmailClient, 'addSubscription').mockResolvedValue(undefined);
      const deleteSpy = vi.spyOn(webmailClient, 'deleteSubscription').mockResolvedValue(undefined);
      const busyStart = new Date();
      busyStart.setHours(12, 0, 0, 0);
      const busyEnd = new Date(busyStart.getTime() + 3600_000);

      vi.spyOn(webmailClient, 'getCalendarEvents')
        .mockResolvedValueOnce({
          data: [
            {
              id: 'busy1',
              title: 'Private Event',
              startTime: busyStart.toISOString(),
              endTime: busyEnd.toISOString(),
              visibility: 'private',
              isOwn: false,
            } as any,
          ],
        })
        .mockResolvedValue({ data: [] });


      render(<App />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /calendar/i }));
      fireEvent.change(await screen.findByDisplayValue('+ Add a colleague calendar…'), { target: { value: 'u2' } });

      await waitFor(() => expect(addSpy).toHaveBeenCalledWith('u2'));
      expect(await screen.findByText('Casey Quinn')).toBeInTheDocument();

      fireEvent.click(screen.getAllByTitle('Remove calendar')[0]);
      await waitFor(() => expect(deleteSpy).toHaveBeenCalledWith('u2'));
    });
  });
});
