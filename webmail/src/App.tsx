import React, { useState, useEffect, useRef } from 'react';
import type { Mailbox, MailboxAccount, SharedMailboxGroup, EmailMessage, Contact, CalendarEvent, SieveFilterRule } from './types';
import { InboxView } from './components/InboxView';
import { ComposerView, type ComposeInitialState } from './components/ComposerView';
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
  const [currentUserId, setCurrentUserId] = useState<string>('');
  const [currentUserRoles, setCurrentUserRoles] = useState<string[]>([]);
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
  const [composeInitialState, setComposeInitialState] = useState<ComposeInitialState>({ mode: 'new' });
  const refreshTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const canEditCompanyDirectory = currentUserRoles.includes('owner') || currentUserRoles.includes('admin');

  useEffect(() => {
    const username = currentUserEmail?.trim();
    document.title = username ? `Miautrix WebMail - ${username}` : 'Miautrix WebMail - ';
  }, [currentUserEmail]);


  const loadAppData = async (userEmail: string = currentUserEmail) => {
    setIsLoadingData(true);
    try {
      // 1. Get user's mailbox account(s)
      const mbRes = await webmailClient.getMailboxes();
      if (mbRes.data.length === 0) {
        setIsLoadingData(false);
        return;
      }
      const accounts = mbRes.data.map((m: any) => normalizeMailboxAccount(m));
      const primaryMailbox = accounts.find((m) => m.address.toLowerCase() === userEmail.toLowerCase()) ?? accounts[0];
      const sharedAccounts = accounts.filter((m) => m.id !== primaryMailbox.id && m.kind === 'shared');
      setMailboxAccounts(accounts);
      setSelectedMailboxAccount(primaryMailbox.id);

      // 2. Fetch Folders and other resources
      const [folderRes, sharedFolderResults, ctRes, evRes, ruleRes] = await Promise.all([
        webmailClient.getFolders(primaryMailbox.id),
        Promise.all(sharedAccounts.map((account) => webmailClient.getFolders(account.id).catch(() => ({ data: [] })))),
        webmailClient.getContacts().catch((err) => { console.error('Contacts failed:', err); return { data: [] }; }),
        webmailClient.getCalendarEvents().catch(() => ({ data: [] })),
        webmailClient.getSieveRules(primaryMailbox.id).catch(() => ({ data: [] })),
      ]);

      const folders = mapFolders(folderRes.data, primaryMailbox);
      const sharedGroups = sharedAccounts.map((account, index) => ({
        account,
        folders: mapFolders(sharedFolderResults[index]?.data ?? [], account),
      }));

      setMailboxes(folders);
      setSharedMailboxGroups(sharedGroups);

      const allContacts = ctRes.data.filter((c: Contact) => c.book === 'personal' || c.book === 'directory');
      setContacts(allContacts);

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
        setCurrentUserRoles([]);
        setIsInitializing(false);
        return;
      }

      try {
        const res = await webmailClient.me();
        const userEmail = res.data.email || '';
        setCurrentUserEmail(userEmail);
        setCurrentUserId(res.data.id ?? '');
        webmailClient.setTenantId(res.data.tenant_id ?? res.data.tenantId ?? null);
        webmailClient.setUserId(res.data.id ?? null);

        const flag = !!(res.data.must_change_password ?? res.data.mustChangePassword);
        setMustChangePassword(flag);
        setCurrentUserRoles((res.data.roles ?? []).map((role: string) => role.toLowerCase()));

        setIsAuthenticated(true);
        await loadAppData(userEmail);
      } catch (err) {
        console.error('Failed to initialize app', err);
        webmailClient.logout();
        setIsAuthenticated(false);
        setMustChangePassword(false);
        setCurrentUserRoles([]);
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

  const writableComposeMailboxId = () => {
    if (selectedMailboxAccount && canWriteMailbox(selectedMailboxAccount)) return selectedMailboxAccount;
    return mailboxAccounts.find((account) => account.kind !== 'shared')?.id ?? mailboxAccounts[0]?.id ?? '';
  };

  const escapeComposeHtml = (value: string) => value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/\n/g, '<br/>');
  const quoteMessage = (message: EmailMessage) =>
    `\n\nOn ${message.receivedAt}, ${message.from.email} wrote:\n${(message.bodyText || message.snippet || '').split('\n').map((line) => `> ${line}`).join('\n')}`;
  const quoteMessageHtml = (message: EmailMessage) => {
    const bodyHtml = message.bodyHtml?.trim() || escapeComposeHtml(message.bodyText || message.snippet || '');
    return `<div data-miautrix-quote-separator="true"><br></div><blockquote style="border-left:3px solid #cbd5e1;margin:0;padding-left:12px;color:#64748b"><div>On ${escapeComposeHtml(message.receivedAt)}, ${escapeComposeHtml(message.from.email)} wrote:</div>${bodyHtml}</blockquote>`;
  };

  const handleNewMessage = () => {
    setComposeInitialState({ mode: 'new' });
    setActiveTab('compose');
  };

  const handleReplyMessage = (message: EmailMessage) => {
    setComposeInitialState({
      mode: 'reply',
      to: message.from.email,
      cc: message.cc?.map((recipient) => recipient.email).join(', '),
      subject: /^re:/i.test(message.subject) ? message.subject : `Re: ${message.subject}`,
      body: quoteMessage(message),
      bodyHtml: `<div><br></div>${quoteMessageHtml(message)}`,
    });
    setActiveTab('compose');
  };

  const handleForwardMessage = (message: EmailMessage) => {
    setComposeInitialState({
      mode: 'forward',
      to: '',
      subject: /^fwd?:/i.test(message.subject) ? message.subject : `Fwd: ${message.subject}`,
      body: `\n\n---------- Forwarded message ----------\nFrom: ${message.from.email}\nTo: ${message.to.map((t) => t.email).join(', ')}\nDate: ${message.receivedAt}\nSubject: ${message.subject}${quoteMessage(message)}`,
      bodyHtml: `<div><br></div><div data-miautrix-quote-separator="true"><br></div><blockquote style="border-left:3px solid #cbd5e1;margin:0;padding-left:12px;color:#64748b"><div>---------- Forwarded message ----------</div><div>From: ${escapeComposeHtml(message.from.email)}</div><div>To: ${escapeComposeHtml(message.to.map((t) => t.email).join(', '))}</div><div>Date: ${escapeComposeHtml(message.receivedAt)}</div><div>Subject: ${escapeComposeHtml(message.subject)}</div><br/>${message.bodyHtml?.trim() || escapeComposeHtml(message.bodyText || message.snippet || '')}</blockquote>`,
    });
    setActiveTab('compose');
  };

  const handleEditDraft = (message: EmailMessage) => {
    setComposeInitialState({
      mode: 'draft',
      draftId: message.id,
      mailboxId: message.mailboxId,
      to: message.to.map((t) => t.email).join(', '),
      cc: message.cc?.map((recipient) => recipient.email).join(', '),
      subject: message.subject,
      body: message.bodyText ?? '',
      bodyHtml: message.bodyHtml || undefined,
      attachments: message.attachments,
    });
    setActiveTab('compose');
  };

  const handleMarkRead = async (messageId: string, isRead: boolean) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount)) return;
    await webmailClient.markRead(selectedMailboxAccount, messageId, isRead);
    setMessages((prev) =>
      prev.map((m) => (m.id === messageId ? { ...m, isUnread: !isRead } : m)),
    );

    // Keep sidebar unread counters in sync.
    await refreshFoldersForMailbox(selectedMailboxAccount);
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

      await refreshFoldersForMailbox(selectedMailboxAccount);
      return;
    }

    await refreshMessagesForFolder(sourceFolderId, {
      retryOnEmpty: true,
      retries: 3,
      retryDelayMs: 150,
    });

    await refreshFoldersForMailbox(selectedMailboxAccount);
  };

  const handleMarkReadMany = async (messageIds: string[], isRead: boolean) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount) || messageIds.length === 0) return;

    await Promise.all(
      messageIds.map((id) => webmailClient.markRead(selectedMailboxAccount, id, isRead)),
    );

    setMessages((prev) =>
      prev.map((m) => (messageIds.includes(m.id) ? { ...m, isUnread: !isRead } : m)),
    );

    // Refresh for consistency (handles server-side ordering / counts / any missed optimistic updates).
    await refreshMessagesForFolder(activeFolderId, {
      retryOnEmpty: false,
      retries: 1,
      retryDelayMs: 100,
    });

    // Keep sidebar unread counters in sync.
    await refreshFoldersForMailbox(selectedMailboxAccount);
  };

  const handleMoveMessages = async (messageIds: string[], targetFolderId: string) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount) || messageIds.length === 0) return;

    const sourceFolderId = activeFolderId;

    await Promise.all(
      messageIds.map((id) => webmailClient.moveMessage(selectedMailboxAccount, id, targetFolderId)),
    );

    if (targetFolderId !== sourceFolderId) {
      await refreshMessagesForFolder(targetFolderId, {
        retryOnEmpty: true,
        retries: 6,
        retryDelayMs: 200,
      });
      setActiveFolderId(targetFolderId);

      await refreshFoldersForMailbox(selectedMailboxAccount);
      return;
    }

    await refreshMessagesForFolder(sourceFolderId, {
      retryOnEmpty: true,
      retries: 3,
      retryDelayMs: 150,
    });

    await refreshFoldersForMailbox(selectedMailboxAccount);
  };

  const handleDeleteMessages = async (messageIds: string[], permanent: boolean = false) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount) || messageIds.length === 0) return;

    await Promise.all(
      messageIds.map((id) => webmailClient.deleteMessage(selectedMailboxAccount, id, permanent)),
    );

    setMessages((prev) => prev.filter((message) => !messageIds.includes(message.id)));

    await refreshMessagesForFolder(activeFolderId, {
      retryOnEmpty: false,
      retries: 1,
      retryDelayMs: 150,
    });

    await refreshFoldersForMailbox(selectedMailboxAccount);
  };

  const handleFlagMessage = async (messageId: string, color: string | null) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount)) return;

    await webmailClient.setFlag(selectedMailboxAccount, messageId, color);
    setMessages((prev) =>
      prev.map((m) => (m.id === messageId ? { ...m, flagColor: color as EmailMessage['flagColor'] } : m))
    );
  };


  const refreshFoldersForMailbox = async (mailboxId: string) => {
    const account = mailboxAccounts.find((a) => a.id === mailboxId);
    if (!account) return;

    const folderRes = await webmailClient.getFolders(mailboxId).catch(() => ({ data: [] }));
    const mapped: Mailbox[] = mapFolders(folderRes.data ?? [], account);

    if (account.kind === 'shared') {
      setSharedMailboxGroups((prev) =>
        prev.map((g) => (g.account.id === mailboxId ? { ...g, folders: mapped } : g)),
      );
    } else {
      setMailboxes(mapped);
    }
  };

  const handleRefreshMessages = async () => {
    if (!activeFolderId || !selectedMailboxAccount) return;

    await refreshMessagesForFolder(activeFolderId, { mailboxId: selectedMailboxAccount });
    await refreshFoldersForMailbox(selectedMailboxAccount);
  };

  const setupAutoRefresh = () => {
    if (refreshTimerRef.current) clearInterval(refreshTimerRef.current);
    refreshTimerRef.current = null;

    const intervalStr = window.localStorage.getItem('miautrix_webmail_refresh_interval') || '5m';
    if (intervalStr === 'off') return;

    const minutes = parseInt(intervalStr.replace('m', ''), 10);
    const safeMinutes = Number.isFinite(minutes) && minutes > 0 ? minutes : 5;
    const ms = safeMinutes * 60 * 1000;

    refreshTimerRef.current = setInterval(async () => {
      if (!activeFolderId || !selectedMailboxAccount) return;

      await refreshMessagesForFolder(activeFolderId, { mailboxId: selectedMailboxAccount });
      await refreshFoldersForMailbox(selectedMailboxAccount);
    }, ms);
  };

  useEffect(() => {
    const onIntervalChanged = () => {
      setupAutoRefresh();
    };

    window.addEventListener('miautrix:webmail:refresh-interval-changed', onIntervalChanged);
    setupAutoRefresh();

    return () => {
      window.removeEventListener('miautrix:webmail:refresh-interval-changed', onIntervalChanged);
      if (refreshTimerRef.current) clearInterval(refreshTimerRef.current);
      refreshTimerRef.current = null;
    };
  }, [activeFolderId, selectedMailboxAccount]);

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

  const handleDeleteFolder = async (folderId: string) => {
    if (!selectedMailboxAccount || !canWriteMailbox(selectedMailboxAccount)) return;

    try {
      await webmailClient.deleteFolder(selectedMailboxAccount, folderId);

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

      // If we just deleted the active folder, jump to Inbox.
      if (activeFolderId === folderId) {
        const inbox = folders.find((f) => f.role === 'inbox') ?? folders[0];
        if (inbox) {
          setActiveFolderId(inbox.id);
          // Load messages for the new folder.
          void refreshMessagesForFolder(inbox.id, { mailboxId: selectedMailboxAccount });
        }
      }
    } catch (err: any) {
      console.error('Folder delete failed', { folderId, err });
      alert(`Folder delete failed: ${err?.message ?? String(err)}`);
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

  const handleSendEmail = async (msgData: { mailboxId: string; from: string; to: string; cc?: string; bcc?: string; subject: string; body: string; bodyHtml?: string }) => {
    if (!canWriteMailbox(msgData.mailboxId)) return;

    await webmailClient.sendMessage(msgData.mailboxId, {
      from: msgData.from,
      to: msgData.to,
      cc: msgData.cc,
      bcc: msgData.bcc,
      subject: msgData.subject || '(No Subject)',
      body: msgData.body,
      bodyHtml: msgData.bodyHtml,
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
        onLoginSuccess={async (user) => {
          const userEmail = user.email || '';
          setCurrentUserEmail(userEmail);
          setCurrentUserId(user.id ?? '');
          webmailClient.setTenantId(user.tenant_id ?? user.tenantId ?? null);
          webmailClient.setUserId(user.id ?? null);

          const flag = !!(user.must_change_password ?? user.mustChangePassword);
          setMustChangePassword(flag);
          setCurrentUserRoles((user.roles ?? []).map((role: string) => role.toLowerCase()));

          setIsAuthenticated(true);
          await loadAppData(userEmail);
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
          setCurrentUserId(res.data.id ?? '');
          const flag = !!(res.data.must_change_password ?? res.data.mustChangePassword);
          setMustChangePassword(flag);
            setCurrentUserRoles((res.data.roles ?? []).map((role: string) => role.toLowerCase()));
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
            onClick={handleNewMessage}
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
            onComposeClick={handleNewMessage}
            onOpenRulesClick={() => setActiveTab('rules')}
            onRefreshMessages={handleRefreshMessages}
            onReplyMessage={handleReplyMessage}
            onForwardMessage={handleForwardMessage}
            onEditDraft={handleEditDraft}
            onMarkRead={handleMarkRead}
            onDeleteMessage={handleDeleteMessage}
            onArchiveSelected={handleArchiveSelected}
            onJunkSelected={handleJunkSelected}
            onMoveMessage={handleMoveMessage}
            onCreateFolder={handleCreateFolder}
            onMoveFolder={handleMoveFolder}
            onDeleteFolder={handleDeleteFolder}
            canWriteCurrentMailbox={canWriteMailbox(selectedMailboxAccount)}
            quotaUsedBytes={mailboxes[0]?.usedBytes ?? 0}
            quotaBytes={mailboxes[0]?.quotaBytes ?? 0}
            onMarkReadMany={handleMarkReadMany}
            onMoveMessages={handleMoveMessages}
            onDeleteMessages={handleDeleteMessages}
            onFlagMessage={handleFlagMessage}
          />

        )}
        {activeTab === 'compose' && (
          <ComposerView
            accounts={mailboxAccounts.filter((account) => account.address === currentUserEmail || (account.kind === 'shared' && account.accessLevel === 'write'))}
            defaultAccountId={writableComposeMailboxId()}
            initialState={composeInitialState}
            contacts={contacts}
            onDiscardClick={() => setActiveTab('inbox')}
            onSendClick={handleSendEmail}
            onSent={handleRefreshMessages}
          />
        )}
        {activeTab === 'contacts' && (
          <ContactsView
            contacts={contacts.map((contact) => contact.book === 'directory'
              ? { ...contact, canEdit: canEditCompanyDirectory }
              : contact)}
            onContactsChanged={(nextContacts) => setContacts(nextContacts)}
            onEmailContact={(email) => {
              setComposeInitialState({ mode: 'new', to: email });
              setActiveTab('compose');
            }}
          />
        )}
        {activeTab === 'calendar' && (
          <CalendarView
            events={events}
            onEventsChanged={(nextEvents) => setEvents(nextEvents)}
            currentUserEmail={currentUserEmail}
            currentUserId={currentUserId}
            currentUserRoles={currentUserRoles}
            contacts={contacts}
            mailboxAccounts={mailboxAccounts}
          />
        )}
        {activeTab === 'rules' && (
          <SieveRulesView initialRules={rules} mailboxId={selectedMailboxAccount} onRulesChanged={(nextRules) => setRules(nextRules)} />
        )}
      </div>
    </div>
  );
};

export default App;
