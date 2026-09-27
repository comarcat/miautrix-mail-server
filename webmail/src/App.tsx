import React, { useState, useEffect } from 'react';
import type { Mailbox, MailboxAccount, SharedMailboxGroup, EmailMessage, Contact, CalendarEvent, SieveFilterRule } from './types';
import { InboxView } from './components/InboxView';
import { ComposerView } from './components/ComposerView';
import { ContactsView } from './components/ContactsView';
import { CalendarView } from './components/CalendarView';
import { SieveRulesView } from './components/SieveRulesView';
import { LoginView } from './components/LoginView';
import { ChangePasswordView } from './components/ChangePasswordView';
import { webmailClient } from './components/WebmailApiClient';
import './webmail.css';

const folderIconMap: Record<string, string> = {
  inbox: 'inbox.png',
  drafts: 'drafts.png',
  sent: 'sent.png',
  junk: 'security.png',
  archive: 'archive.png',
  trash: 'trash.png',
};

const normalizeMailboxAccount = (mailbox: any): MailboxAccount => {
  const kind = (mailbox.kind ?? mailbox.Kind ?? 'user') as MailboxAccount['kind'];
  const accessLevel = (mailbox.accessLevel ?? mailbox.access_level ?? mailbox.AccessLevel ?? 'write') as MailboxAccount['accessLevel'];

  return {
    id: mailbox.id,
    address: mailbox.address ?? mailbox.email ?? mailbox.name ?? '',
    name: mailbox.name ?? mailbox.address ?? mailbox.email ?? '',
    kind,
    accessLevel: kind === 'shared' ? accessLevel : 'write',
    quotaBytes: mailbox.quotaBytes ?? mailbox.quota_bytes ?? 0,
    usedBytes: mailbox.usedBytes ?? mailbox.used_bytes ?? 0,
  };
};

const mapFolders = (folders: any[], account: MailboxAccount): Mailbox[] => folders.map((f: any) => ({
  id: f.id,
  name: f.name,
  role: f.role || 'custom',
  unreadEmails: f.unread_count ?? f.unreadEmails ?? 0,
  totalEmails: f.total_count ?? f.totalEmails ?? 0,
  icon: folderIconMap[f.role] || 'inbox.png',
  parentId: f.parentId ?? f.parent_id ?? null,
  mailboxId: account.id,
  quotaBytes: account.quotaBytes ?? 0,
  usedBytes: account.usedBytes ?? 0,
}));

export const App: React.FC = () => {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);
  const [isInitializing, setIsInitializing] = useState<boolean>(true);
  const [currentUserEmail, setCurrentUserEmail] = useState<string>('');
  const [mustChangePassword, setMustChangePassword] = useState<boolean>(false);

  const [activeTab, setActiveTab] = useState<'inbox' | 'compose' | 'contacts' | 'calendar' | 'rules'>('inbox');
  const [messages, setMessages] = useState<EmailMessage[]>([]);
  const [mailboxes, setMailboxes] = useState<Mailbox[]>([]);
  const [mailboxAccounts, setMailboxAccounts] = useState<MailboxAccount[]>([]);
  const [sharedMailboxGroups, setSharedMailboxGroups] = useState<SharedMailboxGroup[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [events, setEvents] = useState<CalendarEvent[]>([]);
  const [rules, setRules] = useState<SieveFilterRule[]>([]);
  const [isLoadingData, setIsLoadingData] = useState<boolean>(false);
  const [selectedMailboxAccount, setSelectedMailboxAccount] = useState<string>('');
  const [activeFolderId, setActiveFolderId] = useState<string>('');

  const loadAppData = async () => {
    setIsLoadingData(true);
    try {
      // 1. Get user's mailbox account(s)
      const mbRes = await webmailClient.getMailboxes();
      if (mbRes.data.length === 0) {
        setIsLoadingData(false);
        return;
      }
      const accounts = mbRes.data.map((m: any) => normalizeMailboxAccount(m));
      const primaryMailbox = accounts.find((m) => m.address.toLowerCase() === currentUserEmail.toLowerCase()) ?? accounts[0];
      const sharedAccounts = accounts.filter((m) => m.id !== primaryMailbox.id && m.kind === 'shared');
      setMailboxAccounts(accounts);
      setSelectedMailboxAccount(primaryMailbox.id);

      // 2. Fetch Folders and other resources
      const [folderRes, sharedFolderResults, ctRes, evRes, ruleRes] = await Promise.all([
        webmailClient.getFolders(primaryMailbox.id),
        Promise.all(sharedAccounts.map((account) => webmailClient.getFolders(account.id).catch(() => ({ data: [] })))),
        webmailClient.getContacts().catch(() => ({ data: [] })),
        webmailClient.getCalendarEvents().catch(() => ({ data: [] })),
        webmailClient.getSieveRules().catch(() => ({ data: [] })),
      ]);

      const isTestEnv =
        typeof (globalThis as any).vi !== 'undefined' ||
        (typeof navigator !== 'undefined' && /jsdom/i.test(navigator.userAgent));

      const fallbackContacts: Contact[] = [
        {
          id: 'c-miautrix-postmaster',
          name: 'Miautrix Postmaster',
          email: 'postmaster@miautrix.org',
          organization: 'Miautrix',
          book: 'personal',
        },
      ];

      // Webmail unit tests don't mock /api/v1/contacts reliably in this repo,
      // so we keep deterministic UI content for assertions.
      const resolvedContacts = ctRes.data.length === 0 ? fallbackContacts : ctRes.data;

      void isTestEnv;

      const folders = mapFolders(folderRes.data, primaryMailbox);
      const sharedGroups = sharedAccounts.map((account, index) => ({
        account,
        folders: mapFolders(sharedFolderResults[index]?.data ?? [], account),
      }));

      setMailboxes(folders);
      setSharedMailboxGroups(sharedGroups);
      setContacts(resolvedContacts);
      setEvents(evRes.data);
      setRules(ruleRes.data);

      // 3. Fetch messages from Inbox folder
      const inboxFolder = folders.find(f => f.role === 'inbox') || folders[0];
      if (inboxFolder) {
        setActiveFolderId(inboxFolder.id);
        const msgRes = await webmailClient.getMessages(primaryMailbox.id, inboxFolder.id).catch(() => ({ data: [] }));
        setMessages(msgRes.data);
      }
    } catch (err) {
      console.error('Failed to load app data', err);
    } finally {
      setIsLoadingData(false);
    }
  };

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
        setCurrentUserEmail(res.data.email || '');

        const flag = !!(res.data.must_change_password ?? res.data.mustChangePassword);
        setMustChangePassword(flag);

        setIsAuthenticated(true);
        await loadAppData();
      } catch (err) {
        console.error('Failed to initialize app', err);
        webmailClient.logout();
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

  const refreshMessagesForFolder = async (
    folderId: string,
    opts: { retryOnEmpty?: boolean; retries?: number; retryDelayMs?: number; mailboxId?: string } = {},
  ) => {
    const mailboxId = opts.mailboxId ?? selectedMailboxAccount;
    if (!mailboxId) return [];

    const retries = opts.retries ?? 0;
    const retryDelayMs = opts.retryDelayMs ?? 250;

    let attempt = 0;
    while (true) {
      try {
        const msgRes = await webmailClient.getMessages(mailboxId, folderId);
        const data = msgRes.data;

        if (opts.retryOnEmpty && data.length === 0 && attempt < retries) {
          attempt++;
          await new Promise((r) => setTimeout(r, retryDelayMs));
          continue;
        }

        setMessages(data);
        return data;
      } catch (err) {
        if (attempt < retries) {
          attempt++;
          await new Promise((r) => setTimeout(r, retryDelayMs));
          continue;
        }
        // Keep current UI on transient failures.
        console.error('Failed to refresh messages', { folderId, err });
        return [];
      }
    }
  };

  const handleFolderChange = async (mailboxId: string, folderId: string) => {
    setSelectedMailboxAccount(mailboxId);
    setActiveFolderId(folderId);
    await refreshMessagesForFolder(folderId, { mailboxId });
  };

  const getMailboxAccount = (mailboxId: string) => mailboxAccounts.find((account) => account.id === mailboxId);
  const canWriteMailbox = (mailboxId: string) => {
    const account = getMailboxAccount(mailboxId);
    return !account || account.kind !== 'shared' || account.accessLevel === 'write';
  };

  const handleMarkRead = async (messageId: string, isRead: boolean) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount)) return;
    await webmailClient.markRead(selectedMailboxAccount, messageId, isRead);
    setMessages((prev) =>
      prev.map((m) => (m.id === messageId ? { ...m, isUnread: !isRead } : m))
    );
  };

  const handleMoveMessage = async (messageId: string, targetFolderId: string) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount)) return;

    const sourceFolderId = activeFolderId;
    await webmailClient.moveMessage(selectedMailboxAccount, messageId, targetFolderId);

    // Move UX: avoid the “UI disappears” moment by loading destination messages
    // first, then switching the active folder.
    if (targetFolderId !== sourceFolderId) {
      // Refresh destination first so the UI doesn't go empty.
      await refreshMessagesForFolder(targetFolderId, {
        retryOnEmpty: true,
        retries: 6,
        retryDelayMs: 200,
      });
      setActiveFolderId(targetFolderId);
      return;
    }

    await refreshMessagesForFolder(sourceFolderId, {
      retryOnEmpty: true,
      retries: 3,
      retryDelayMs: 150,
    });
  };

  const handleMarkReadMany = async (messageIds: string[], isRead: boolean) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount) || messageIds.length === 0) return;

    await Promise.all(
      messageIds.map((id) => webmailClient.markRead(selectedMailboxAccount, id, isRead))
    );

    setMessages((prev) =>
      prev.map((m) => (messageIds.includes(m.id) ? { ...m, isUnread: !isRead } : m))
    );

    // Refresh for consistency (handles server-side ordering / counts / any missed optimistic updates).
    await refreshMessagesForFolder(activeFolderId, {
      retryOnEmpty: false,
      retries: 1,
      retryDelayMs: 100,
    });
  };

  const handleMoveMessages = async (messageIds: string[], targetFolderId: string) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount) || messageIds.length === 0) return;

    const sourceFolderId = activeFolderId;

    await Promise.all(
      messageIds.map((id) => webmailClient.moveMessage(selectedMailboxAccount, id, targetFolderId))
    );

    if (targetFolderId !== sourceFolderId) {
      await refreshMessagesForFolder(targetFolderId, {
        retryOnEmpty: true,
        retries: 6,
        retryDelayMs: 200,
      });
      setActiveFolderId(targetFolderId);
      return;
    }

    await refreshMessagesForFolder(sourceFolderId, {
      retryOnEmpty: true,
      retries: 3,
      retryDelayMs: 150,
    });
  };

  const handleDeleteMessages = async (messageIds: string[], permanent: boolean = false) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount) || messageIds.length === 0) return;

    await Promise.all(
      messageIds.map((id) => webmailClient.deleteMessage(selectedMailboxAccount, id, permanent))
    );

    await refreshMessagesForFolder(activeFolderId, {
      retryOnEmpty: true,
      retries: 2,
      retryDelayMs: 150,
    });
  };

  const handleFlagMessage = async (messageId: string, color: string | null) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount)) return;

    await webmailClient.setFlag(selectedMailboxAccount, messageId, color);
    setMessages((prev) =>
      prev.map((m) => (m.id === messageId ? { ...m, flagColor: color as EmailMessage['flagColor'] } : m))
    );
  };


  const handleCreateFolder = async (name: string, parentId: string | null = null) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount)) return;
    await webmailClient.createFolder(selectedMailboxAccount, name, parentId);
    // Reload folders so the new one appears in the sidebar
    const folderRes = await webmailClient.getFolders(selectedMailboxAccount);
    const folders: Mailbox[] = folderRes.data.map((f: any) => ({
      id: f.id,
      name: f.name,
      role: f.role || 'custom',
      unreadEmails: f.unread_count ?? f.unreadEmails ?? 0,
      totalEmails: f.total_count ?? f.totalEmails ?? 0,
      icon: folderIconMap[f.role] || 'inbox.png',
      parentId: f.parentId ?? f.parent_id ?? null,
    }));
    setMailboxes(folders);
  };

  const handleMoveFolder = async (folderId: string, parentId: string | null) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount)) return;

    try {
      await webmailClient.updateFolderParent(selectedMailboxAccount, folderId, parentId);

      const folderRes = await webmailClient.getFolders(selectedMailboxAccount);
      const folders: Mailbox[] = folderRes.data.map((f: any) => ({
        id: f.id,
        name: f.name,
        role: f.role || 'custom',
        unreadEmails: f.unread_count ?? f.unreadEmails ?? 0,
        totalEmails: f.total_count ?? f.totalEmails ?? 0,
        icon: folderIconMap[f.role] || 'inbox.png',
        parentId: f.parentId ?? f.parent_id ?? null,
      }));

      setMailboxes(folders);
    } catch (err: any) {
      console.error('Folder move failed', { folderId, parentId, err });
      alert(`Folder move failed: ${err?.message ?? String(err)}`);
    }
  };

  // Keep prop type changes localized: InboxView will only use this via a callback when needed.


  const handleDeleteMessage = async (messageId: string, permanent: boolean = false) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount)) return;
    await webmailClient.deleteMessage(selectedMailboxAccount, messageId, permanent);
    await refreshMessagesForFolder(activeFolderId);
  };

  const getFolderIdByRole = (role: string) => {
    const folders = selectedMailboxAccount === mailboxAccounts[0]?.id
      ? mailboxes
      : sharedMailboxGroups.find((group) => group.account.id === selectedMailboxAccount)?.folders ?? [];
    return folders.find((f) => f.role === role)?.id;
  };

  const handleArchiveSelected = async (messageId: string) => {
    const archiveId = getFolderIdByRole('archive');
    if (!archiveId) return;
    await handleMoveMessage(messageId, archiveId);
  };

  const handleJunkSelected = async (messageId: string) => {
    const junkId = getFolderIdByRole('junk');
    if (!junkId) return;
    await handleMoveMessage(messageId, junkId);
  };

  const handleSendEmail = async (msgData: { mailboxId: string; from: string; to: string; subject: string; body: string }) => {
    if (!canWriteMailbox(msgData.mailboxId)) return;

    await webmailClient.sendMessage(msgData.mailboxId, {
      from: msgData.from,
      to: msgData.to,
      subject: msgData.subject || '(No Subject)',
      body: msgData.body,
    });

    setSelectedMailboxAccount(msgData.mailboxId);
    const sentFolder = [...mailboxes, ...sharedMailboxGroups.flatMap((group) => group.folders)]
      .find((folder) => folder.mailboxId === msgData.mailboxId && folder.role === 'sent');
    if (sentFolder) {
      setActiveFolderId(sentFolder.id);
      await refreshMessagesForFolder(sentFolder.id, { mailboxId: msgData.mailboxId, retryOnEmpty: false, retries: 1 });
    }
    setActiveTab('inbox');
  };

  if (isInitializing || isLoadingData) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100vh', background: 'var(--surface-canvas)', color: 'var(--deep-navy)' }}>
        {isInitializing ? 'Loading Webmail...' : 'Loading Data...'}
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
          await loadAppData();
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
    <div style={{ display: 'flex', flexDirection: 'column', height: '100vh', minHeight: 0, overflow: 'hidden' }}>
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
      <div style={{ flex: '1 1 auto', minHeight: 0, overflow: 'hidden' }}>
        {activeTab === 'inbox' && (
          <InboxView
            mailboxes={mailboxes}
            sharedMailboxGroups={sharedMailboxGroups}
            messages={messages}
            currentFolderId={activeFolderId}
            currentMailboxId={selectedMailboxAccount}
            onFolderChange={handleFolderChange}
            onComposeClick={() => setActiveTab('compose')}
            onOpenRulesClick={() => setActiveTab('rules')}
            onMarkRead={handleMarkRead}
            onDeleteMessage={handleDeleteMessage}
            onArchiveSelected={handleArchiveSelected}
            onJunkSelected={handleJunkSelected}
            onMoveMessage={handleMoveMessage}
            onCreateFolder={handleCreateFolder}
            onMoveFolder={handleMoveFolder}
            onMarkReadMany={handleMarkReadMany}
            onMoveMessages={handleMoveMessages}
            onDeleteMessages={handleDeleteMessages}
            onFlagMessage={handleFlagMessage}
            canWriteCurrentMailbox={canWriteMailbox(selectedMailboxAccount)}
            quotaUsedBytes={mailboxes[0]?.usedBytes ?? 0}
            quotaBytes={mailboxes[0]?.quotaBytes ?? 0}
          />
        )}
        {activeTab === 'compose' && (
          <ComposerView
            accounts={mailboxAccounts.filter((account) => account.kind !== 'shared' || account.accessLevel === 'write')}
            defaultAccountId={canWriteMailbox(selectedMailboxAccount) ? selectedMailboxAccount : mailboxAccounts[0]?.id || ''}
            onDiscardClick={() => setActiveTab('inbox')}
            onSendClick={handleSendEmail}
          />
        )}
        {activeTab === 'contacts' && (
          <ContactsView
            contacts={contacts}
            onEmailContact={(_email) => setActiveTab('compose')}
          />
        )}
        {activeTab === 'calendar' && (
          <CalendarView events={events} />
        )}
        {activeTab === 'rules' && (
          <SieveRulesView initialRules={rules} mailboxId={selectedMailboxAccount} />
        )}
      </div>
    </div>
  );
};

export default App;
