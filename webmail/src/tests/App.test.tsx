import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import App from '../App';

describe('Webmail SPA UI Flow', () => {
  it('renders webmail navigation tabs and default inbox messages', () => {
    render(<App />);

    expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
    expect(screen.getAllByText('Quarterly TLS & Security Audit Completed')[0]).toBeInTheDocument();

    // Reading pane renders the sender identity and the SPF/DKIM verification badge
    const readingPane = screen.getByRole('main', { name: /reading pane/i });
    expect(readingPane.textContent).toContain('Miautrix Security Ops');
    expect(readingPane.textContent).toContain('security@miautrix.org');
    expect(readingPane.textContent).toContain('SPF & DKIM Valid');
    expect(readingPane.textContent).toContain('To: alex.vance@miautrix.org');
  });

  it('navigates to Compose view when New Message is clicked', () => {
    render(<App />);

    const newMsgBtn = screen.getAllByText(/new message/i)[0];
    fireEvent.click(newMsgBtn);

    expect(screen.getByPlaceholderText('Add a subject')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Enter recipients...')).toBeInTheDocument();
  });

  it('navigates to Contacts view and allows searching contacts', () => {
    render(<App />);

    const contactsTab = screen.getByRole('button', { name: /contacts/i });
    fireEvent.click(contactsTab);

    expect(screen.getByRole('heading', { level: 1, name: /personal contacts/i })).toBeInTheDocument();
    expect(screen.getByText('Miautrix Postmaster')).toBeInTheDocument();
  });

  it('navigates to Settings & Rules to manage ManageSieve filters', () => {
    render(<App />);

    const rulesTab = screen.getByRole('button', { name: /settings & rules/i });
    fireEvent.click(rulesTab);

    expect(screen.getByRole('heading', { level: 1, name: /managesieve filter rules/i })).toBeInTheDocument();
    expect(screen.getByText('Move Jira Notifications')).toBeInTheDocument();
  });
});
