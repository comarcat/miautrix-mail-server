import React, { useState, useEffect } from 'react';
import type { Mailbox, EmailMessage, Contact, CalendarEvent, SieveFilterRule } from './types';
import { InboxView } from './components/InboxView';
import { ComposerView } from './components/ComposerView';
import { ContactsView } from './components/ContactsView';
import { CalendarView } from './components/CalendarView';
import { SieveRulesView } from './components/SieveRulesView';
import { LoginView } from './components/LoginView';
import { ChangePasswordView } from './components/ChangePasswordView';
import { webmailClient } from './components/WebmailApiClient';
import './webmail.css';

const INITIAL_MAILBOXES: Mailbox[] = [
  { id: 'inbox', name: 'Inbox', role: 'inbox', unreadEmails: 3, totalEmails: 48, icon: 'inbox.png' },
  { id: 'drafts', name: 'Drafts', role: 'drafts', unreadEmails: 0, totalEmails: 2, icon: 'drafts.png' },
  { id: 'sent', name: 'Sent Items', role: 'sent', unreadEmails: 0, totalEmails: 34, icon: 'sent.png' },
  { id: 'junk', name: 'Quarantine / Spam', role: 'junk', unreadEmails: 1, totalEmails: 4, icon: 'security.png' },
  { id: 'archive', name: 'Archive', role: 'archive', unreadEmails: 0, totalEmails: 112, icon: 'archive.png' },
  { id: 'trash', name: 'Trash', role: 'trash', unreadEmails: 0, totalEmails: 8, icon: 'trash.png' },
];

const INITIAL_MESSAGES: EmailMessage[] = [
  {
    id: 'msg-1',
    mailboxId: 'inbox',
    from: { name: 'Miautrix Security Ops', email: 'security@miautrix.org' },
    to: [{ name: 'Alex Vance', email: 'alex.vance@miautrix.org' }],
    subject: 'Quarterly TLS & Security Audit Completed',
    snippet: 'All cipher suites verified. SPF, DKIM and DMARC enforcement active on all domains...',
    bodyHtml: `
      <p>Hello Team,</p>
      <p>The quarterly mail transport security review has completed with full compliance across all tenant domains.</p>
      <h4 style="color: var(--deep-navy); margin: 20px 0 10px 0; font-weight: 500;">Summary of Verification:</h4>
      <ul style="margin: 0 0 20px 20px; line-height: 1.8;">
        <li><strong>Outbound DKIM Signing:</strong> RSA-2048 keys active and validated.</li>
        <li><strong>Inbound Greylisting & Anti-Spam:</strong> Zero false-positives reported across 25,000 processed messages.</li>
        <li><strong>Content-Addressable Storage:</strong> SHA-256 deduplication achieved 38% storage optimization.</li>
      </ul>
      <p>If you require detailed audit logs, you can view the immutable audit trail in the administrative portal.</p>
      <p style="margin-top: 32px;">
        Best regards,<br>
        <strong>Miautrix Security Operations</strong>
      </p>
    `,
    receivedAt: 'Today at 10:42 AM',
    isUnread: true,
    securityChecks: {
      spfPass: true,
      dkimPass: true,
      dmarcPass: true,
      tlsVersion: 'TLS 1.3',
    },
    attachments: [
      { id: 'att-1', name: 'Security-Report-Q3-2026.pdf', size: 250880, contentType: 'application/pdf' },
    ],
  },
  {
    id: 'msg-2',
    mailboxId: 'inbox',
    from: { name: 'Alex Vance', email: 'alex.vance@blackmesa.internal' },
    to: [{ name: 'Alex Vance', email: 'alex.vance@miautrix.org' }],
    subject: 'Updated RFC-5322 MIME Parser Benchmark',
    snippet: 'Hi Team, I have attached the latest performance benchmarks for streaming storage...',
    bodyHtml: `
      <p>Hi Team,</p>
      <p>I have benchmarked the streaming MIME parser against large 50MB attachments. Memory footprint remained strictly under 4MB per worker.</p>
      <p>Let me know if you want to deploy the patch to the staging cluster.</p>
    `,
    receivedAt: 'Yesterday at 4:15 PM',
    isUnread: true,
    securityChecks: {
      spfPass: true,
      dkimPass: true,
      dmarcPass: true,
    },
    attachments: [],
  },
  {
    id: 'msg-3',
    mailboxId: 'inbox',
    from: { name: 'Postmaster Daemon', email: 'postmaster@miautrix.org' },
    to: [{ name: 'Alex Vance', email: 'alex.vance@miautrix.org' }],
    subject: 'Delivery Status Notification (Success)',
    snippet: 'Your message to operator@internal.miautrix was successfully delivered in 14ms...',
    bodyHtml: `
      <p>This is an automated delivery status notification from Miautrix Mail MTA.</p>
      <p>Status: <strong>2.0.0 (Delivered)</strong></p>
      <p>Message ID: &lt;20260918-091238.123@miautrix.org&gt;</p>
    `,
    receivedAt: 'Sep 16 at 2:04 PM',
    isUnread: false,
    securityChecks: {
      spfPass: true,
      dkimPass: true,
      dmarcPass: true,
    },
    attachments: [],
  },
];

const INITIAL_CONTACTS: Contact[] = [
  {
    id: 'cnt-1',
    name: 'Alex Vance',
    email: 'alex.vance@blackmesa.internal',
    organization: 'Engineering Dept',
    book: 'personal',
  },
  {
    id: 'cnt-2',
    name: 'Miautrix Postmaster',
    email: 'postmaster@miautrix.org',
    organization: 'Mail Infrastructure',
    book: 'personal',
  },
  {
    id: 'cnt-3',
    name: 'Security Response Team',
    email: 'secops@miautrix.org',
    organization: 'Platform Security',
    book: 'directory',
  },
];

const INITIAL_EVENTS: CalendarEvent[] = [
  {
    id: 'evt-1',
    title: 'Weekly MTA Architecture Review',
    startTime: '2026-09-18T14:00:00Z',
    endTime: '2026-09-18T15:00:00Z',
    organizer: 'Alex Vance',
    status: 'confirmed',
  },
  {
    id: 'evt-2',
    title: 'DNSBL Rotation Window',
    startTime: '2026-09-22T09:00:00Z',
    endTime: '2026-09-22T10:00:00Z',
    organizer: 'SecOps',
    status: 'confirmed',
  },
];

const INITIAL_RULES: SieveFilterRule[] = [
  {
    id: 'rule-1',
    name: 'Move Jira Notifications',
    field: 'subject',
    comparator: 'contains',
    value: '[JIRA]',
    action: 'fileinto',
    targetFolder: 'Archive',
    active: true,
  },
  {
    id: 'rule-2',
    name: 'Flag High Priority Mails',
    field: 'header',
    comparator: 'contains',
    value: 'X-Priority: 1',
    action: 'addflag',
    active: true,
  },
];

export const App: React.FC = () => {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);
  const [isInitializing, setIsInitializing] = useState<boolean>(true);
  const [currentUserEmail, setCurrentUserEmail] = useState<string>('alex.vance@miautrix.org');
  const [mustChangePassword, setMustChangePassword] = useState<boolean>(false);

  const [activeTab, setActiveTab] = useState<'inbox' | 'compose' | 'contacts' | 'calendar' | 'rules'>('inbox');
  const [messages, setMessages] = useState<EmailMessage[]>(INITIAL_MESSAGES);

  useEffect(() => {
    const initAuth = async () => {
      const token = webmailClient.getToken();
      if (!token) {
        setIsAuthenticated(false);
        setMustChangePassword(false);
        setIsInitializing(false);
        return;
      }

      try {
        const res = await webmailClient.me();
        setCurrentUserEmail(res.data.email || 'alex.vance@miautrix.org');

        const flag = !!(res.data.must_change_password ?? res.data.mustChangePassword);
        setMustChangePassword(flag);

        setIsAuthenticated(true);
      } catch {
        setIsAuthenticated(false);
        setMustChangePassword(false);
      } finally {
        setIsInitializing(false);
      }
    };

    initAuth();

    const handleAuthExpired = () => {
      setIsAuthenticated(false);
      setMustChangePassword(false);
    };

    window.addEventListener('miautrix:auth:expired', handleAuthExpired);
    return () => {
      window.removeEventListener('miautrix:auth:expired', handleAuthExpired);
    };
  }, []);

  const handleSignOut = () => {
    webmailClient.logout().then(() => {
      setIsAuthenticated(false);
      setMustChangePassword(false);
    });
  };

  const handleSendEmail = (msgData: { to: string; subject: string; body: string }) => {
    const newMessage: EmailMessage = {
      id: `msg-${Date.now()}`,
      mailboxId: 'sent',
      from: { name: 'Alex Vance', email: currentUserEmail },
      to: [{ name: msgData.to, email: msgData.to }],
      subject: msgData.subject || '(No Subject)',
      snippet: msgData.body.substring(0, 80),
      bodyHtml: `<p>${msgData.body.replace(/\n/g, '<br/>')}</p>`,
      receivedAt: 'Just now',
      isUnread: false,
      securityChecks: {
        spfPass: true,
        dkimPass: true,
        dmarcPass: true,
      },
      attachments: [],
    };

    setMessages([newMessage, ...messages]);
    setActiveTab('inbox');
  };

  if (isInitializing) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100vh', background: 'var(--surface-canvas)', color: 'var(--deep-navy)' }}>
        Loading Webmail...
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <LoginView
        onLoginSuccess={async (email) => {
          const res = await webmailClient.me();
          setCurrentUserEmail(res.data.email || email);

          const flag = !!(res.data.must_change_password ?? res.data.mustChangePassword);
          setMustChangePassword(flag);

          setIsAuthenticated(true);
        }}
      />
    );
  }

  if (mustChangePassword) {
    return (
      <ChangePasswordView
        onChanged={async () => {
          const res = await webmailClient.me();
          setCurrentUserEmail(res.data.email || currentUserEmail);
          const flag = !!(res.data.must_change_password ?? res.data.mustChangePassword);
          setMustChangePassword(flag);
        }}
      />
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100vh' }}>
      {/* Top Universal App Bar */}
      <header className="wm-header">
        <nav className="wm-tabs" aria-label="Main Navigation">
          <div
            className={`wm-tab ${activeTab === 'inbox' ? 'active' : ''}`}
            onClick={() => setActiveTab('inbox')}
            role="button"
            tabIndex={0}
          >
            Inbox
          </div>
          <div
            className={`wm-tab ${activeTab === 'compose' ? 'active' : ''}`}
            onClick={() => setActiveTab('compose')}
            role="button"
            tabIndex={0}
          >
            New Message
          </div>
          <div
            className={`wm-tab ${activeTab === 'contacts' ? 'active' : ''}`}
            onClick={() => setActiveTab('contacts')}
            role="button"
            tabIndex={0}
          >
            Contacts
          </div>
          <div
            className={`wm-tab ${activeTab === 'calendar' ? 'active' : ''}`}
            onClick={() => setActiveTab('calendar')}
            role="button"
            tabIndex={0}
          >
            Calendar
          </div>
          <div
            className={`wm-tab ${activeTab === 'rules' ? 'active' : ''}`}
            onClick={() => setActiveTab('rules')}
            role="button"
            tabIndex={0}
          >
            Settings & Rules
          </div>

          <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '12px' }}>
            <span style={{ fontSize: '13px', color: 'var(--neutral-body)' }}>{currentUserEmail}</span>
            <button
              type="button"
              className="btn btn-ghost"
              style={{ fontSize: '12px', padding: '4px 8px' }}
              onClick={handleSignOut}
            >
              Sign Out
            </button>
          </div>
        </nav>
      </header>

      {/* Main View Display */}
      <div style={{ flex: 1, overflow: 'hidden' }}>
        {activeTab === 'inbox' && (
          <InboxView
            mailboxes={INITIAL_MAILBOXES}
            messages={messages}
            onComposeClick={() => setActiveTab('compose')}
            onOpenRulesClick={() => setActiveTab('rules')}
          />
        )}
        {activeTab === 'compose' && (
          <ComposerView
            onDiscardClick={() => setActiveTab('inbox')}
            onSendClick={handleSendEmail}
          />
        )}
        {activeTab === 'contacts' && (
          <ContactsView
            contacts={INITIAL_CONTACTS}
            onEmailContact={(_email) => setActiveTab('compose')}
          />
        )}
        {activeTab === 'calendar' && (
          <CalendarView events={INITIAL_EVENTS} />
        )}
        {activeTab === 'rules' && (
          <SieveRulesView initialRules={INITIAL_RULES} />
        )}
      </div>
    </div>
  );
};

export default App;
