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

  describe('Authenticated Session', () => {
    beforeEach(() => {
      vi.spyOn(webmailClient, 'getToken').mockReturnValue('valid-mock-token');
      vi.spyOn(webmailClient, 'me').mockResolvedValue({
        data: { id: 'u1', email: 'alex.vance@miautrix.org' },
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
      expect(screen.getByText('Move Jira Notifications')).toBeInTheDocument();
    });
  });
});
