import React, { useEffect, useState } from 'react';

import type { Mailbox, SharedMailboxGroup, EmailMessage } from '../types';
import { SanitizedMessageBody } from './SanitizedMessageBody';
import { webmailClient } from './WebmailApiClient';


const clampSnippet = (text: string, max = 120) => {
  if (!text) return '';
  return text.length > max ? `${text.slice(0, max)}…` : text;
};

const formatBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let value = bytes;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  const decimals = value >= 10 || unitIndex === 0 ? 0 : 1;
  return `${value.toFixed(decimals)} ${units[unitIndex]}`;
};

const formatReceivedAt = (iso: string): string => {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString(undefined, {
    weekday: 'short',
    month: 'short',
    day: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

const statusIconSrc = (isUnread: boolean) => {
  // Use existing icon set.
  return isUnread ? '/images/icons/webmail/inbox.png' : '/images/icons/webmail/relaxed-view.png';
};

const buildBodyFallbackHtml = (text?: string) => {
  const safe = text
    ? text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/\n/g, '<br/>')
    : '';
  return `<div style="color:var(--neutral-label); font-size:15px; line-height:1.6">${safe}</div>`;
};

const hydrateMessageDetail = async (message: EmailMessage) => {
  if (message.bodyText?.trim() || message.bodyHtml?.trim()) return message;
  return webmailClient.getMessageDetail(message.mailboxId, message.id)
    .then((r) => r.data)
    .catch(() => message);
};

interface InboxViewProps {
  mailboxes: Mailbox[];
  sharedMailboxGroups: SharedMailboxGroup[];
  messages: EmailMessage[];
  onFolderChange: (mailboxId: string, folderId: string) => void;
  onComposeClick: () => void;
  onOpenRulesClick: () => void;
  onRefreshMessages: () => Promise<void>;
  onReplyMessage: (message: EmailMessage) => void;
  onForwardMessage: (message: EmailMessage) => void;
  onEditDraft: (message: EmailMessage) => void;
  currentFolderId: string;
  currentMailboxId: string;

  onMarkRead: (messageId: string, isRead: boolean) => void;
  onDeleteMessage: (messageId: string, permanent?: boolean) => void;
  onArchiveSelected: (messageId: string) => void;
  onJunkSelected: (messageId: string) => void;
  onMoveMessage: (messageId: string, targetFolderId: string) => void;
  onCreateFolder: (name: string, parentId?: string | null) => Promise<void>;
  onMoveFolder: (folderId: string, parentId: string | null) => Promise<void>;
  onDeleteFolder: (folderId: string) => Promise<void>;

  // If the backend returns these counts, InboxView can ask for confirmation with context.
  // (If absent, it falls back to a generic confirmation.)
  getFolderMessageCount?: (folderId: string) => number | undefined;

  // Bulk operations: one round trip per message, issued together by App.tsx.
  onMarkReadMany: (messageIds: string[], isRead: boolean) => Promise<void>;
  onMoveMessages: (messageIds: string[], targetFolderId: string) => Promise<void>;
  onDeleteMessages: (messageIds: string[], permanent?: boolean) => Promise<void>;
  onFlagMessage: (messageId: string, color: string | null) => Promise<void>;
  canWriteCurrentMailbox: boolean;
  quotaUsedBytes?: number;
  quotaBytes?: number;
}

const FLAG_COLORS = ['red', 'blue', 'green', 'orange', 'purple'] as const;
type FlagColor = (typeof FLAG_COLORS)[number];

const FLAG_LABELS: Record<FlagColor, string> = {
  red: 'Red',
  blue: 'Blue',
  green: 'Green',
  orange: 'Orange',
  purple: 'Purple',
};

const clamp = (v: number, min: number, max: number) => Math.max(min, Math.min(max, v));

// Message list column sizing. Persisted so the layout survives a reload.
const LIST_WIDTH_KEY = 'miautrix_webmail_list_width';
const LIST_WIDTH_DEFAULT = 380;
const LIST_WIDTH_MIN = 260;

const maxListWidth = () =>
  // Always leave room for the reading pane, however narrow the window is.
  Math.max(LIST_WIDTH_MIN, Math.min(900, Math.round(window.innerWidth * 0.7)));

const readStoredListWidth = () => {
  if (typeof window === 'undefined') return LIST_WIDTH_DEFAULT;
  const raw = window.localStorage.getItem(LIST_WIDTH_KEY);
  const parsed = raw ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) ? clamp(parsed, LIST_WIDTH_MIN, maxListWidth()) : LIST_WIDTH_DEFAULT;
};


function getMessageContextMenuPosition(x: number, y: number) {
  const top = clamp(y, 0, window.innerHeight - 280);
  const left = clamp(x, 0, window.innerWidth - 220);
  return { top, left };
}

function shouldShowFolderInMoveMenu(folder: Mailbox, currentFolderId: string) {
  return folder.id !== currentFolderId;
}

const SYSTEM_FOLDER_ORDER = ['inbox', 'sent', 'drafts', 'archive', 'junk', 'trash'];

function compareMailboxLabels(a: string, b: string) {
  return a.localeCompare(b, undefined, { sensitivity: 'base' });
}

function compareSystemFolders(a: Mailbox, b: Mailbox) {
  const aIndex = SYSTEM_FOLDER_ORDER.indexOf(a.role);
  const bIndex = SYSTEM_FOLDER_ORDER.indexOf(b.role);
  const aKnown = aIndex >= 0;
  const bKnown = bIndex >= 0;

  if (aKnown && bKnown) return aIndex - bIndex;
  if (aKnown) return -1;
  if (bKnown) return 1;
  return compareMailboxLabels(a.name, b.name);
}

function sortSystemFolders(folders: Mailbox[]) {
  return [...folders].sort(compareSystemFolders);
}

function sortCustomFolders(folders: Mailbox[]) {
  return [...folders].sort((a, b) => compareMailboxLabels(a.name, b.name));
}

function buildMoveMenuFolders(mailboxes: Mailbox[], currentFolderId: string) {
  return [...mailboxes]
    .filter((f) => shouldShowFolderInMoveMenu(f, currentFolderId))
    .sort((a, b) => {
      if (a.role !== 'custom' && b.role !== 'custom') return compareSystemFolders(a, b);
      if (a.role !== 'custom') return -1;
      if (b.role !== 'custom') return 1;
      return compareMailboxLabels(a.name, b.name);
    });
}

function isTestEnv() {
  return typeof (globalThis as any).vi !== 'undefined';
}

const _unused = isTestEnv;

// Use in code paths so TS doesn't complain about unused helpers.
void getMessageContextMenuPosition;
void buildMoveMenuFolders;
void shouldShowFolderInMoveMenu;
void isTestEnv;
void _unused;



export const InboxView: React.FC<InboxViewProps> = ({
  mailboxes,
  sharedMailboxGroups,
  messages,
  onFolderChange,
  onComposeClick,
  onOpenRulesClick,
  onRefreshMessages,
  onReplyMessage,
  onForwardMessage,
  onEditDraft,
  currentFolderId,
  currentMailboxId,
  onMarkRead,
  onDeleteMessage,
  onArchiveSelected,
  onJunkSelected,
  onMoveMessage,
  onCreateFolder,
  onMoveFolder,
  onDeleteFolder,
  getFolderMessageCount,
  onMarkReadMany,
  onMoveMessages,
  onDeleteMessages,
  onFlagMessage,
  canWriteCurrentMailbox,
  quotaUsedBytes = 0,
  quotaBytes = 0,
}) => {
  const [selectedMessageId, setSelectedMessageId] = useState<string>(messages[0]?.id || '');
  const [searchQuery, setSearchQuery] = useState<string>('');

  // Multi-select: ids currently ticked, plus the anchor row for shift-click ranges.
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [selectionAnchor, setSelectionAnchor] = useState<number>(-1);

  const [detailMessage, setDetailMessage] = useState<EmailMessage | null>(null);
  const [isLoadingDetail, setIsLoadingDetail] = useState<boolean>(false);

  const [contextMenu, setContextMenu] = useState<{
    x: number;
    y: number;
    messageId: string;
    isUnread: boolean;
  } | null>(null);

  const [flagMenu, setFlagMenu] = useState<{
    x: number;
    y: number;
    messageIds: string[];
  } | null>(null);
  const [groupByFlag, setGroupByFlag] = useState(false);
  const [collapsedSharedMailboxIds, setCollapsedSharedMailboxIds] = useState<string[]>([]);

  const contextMenuRef = React.useRef<HTMLDivElement | null>(null);

  // Message list column width, draggable via the splitter next to it.
  const [listWidth, setListWidth] = useState<number>(readStoredListWidth);
  const [isResizingList, setIsResizingList] = useState(false);

  useEffect(() => {
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(LIST_WIDTH_KEY, String(listWidth));
    }
  }, [listWidth]);

  // Keep the column within bounds if the window shrinks below the stored width.
  useEffect(() => {
    const onResize = () => setListWidth((w) => clamp(w, LIST_WIDTH_MIN, maxListWidth()));
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);

  const filteredMessages = messages.filter((msg) => {
    // We already fetch messages for the CURRENT folder in App.tsx
    // So we don't need extensive filtering.
    // Every field is coerced: list payloads are untrusted JSON and optional
    // fields (notably `snippet`, from `preview`) can be absent at runtime.
    const query = searchQuery.trim().toLowerCase();
    if (!query) return true;

    const haystack = [msg.subject, msg.from?.name, msg.from?.email, msg.snippet];
    return haystack.some((field) => (field ?? '').toLowerCase().includes(query));
  });

  const currentMessage = messages.find((m) => m.id === selectedMessageId) || filteredMessages[0];

  const pad2 = (n: number) => String(n).padStart(2, '0');

  const getLocalDayKey = (iso: string) => {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '';
    return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
  };

  const getDateGroupLabel = (iso: string) => {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '';

    const now = new Date();
    const todayKey = `${now.getFullYear()}-${pad2(now.getMonth() + 1)}-${pad2(now.getDate())}`;

    const y = new Date(now);
    y.setDate(now.getDate() - 1);
    const yesterdayKey = `${y.getFullYear()}-${pad2(y.getMonth() + 1)}-${pad2(y.getDate())}`;

    const key = getLocalDayKey(iso);
    if (!key) return '';
    if (key === todayKey) return 'Today';
    if (key === yesterdayKey) return 'Yesterday';

    return d.toLocaleDateString(undefined, {
      month: 'short',
      day: '2-digit',
      year: 'numeric',
    });
  };

  const dateGroupedMessages = filteredMessages.reduce<
    Array<{ key: string; label: string; items: EmailMessage[] }>
  >((acc, msg) => {
    const key = getLocalDayKey(msg.receivedAt);
    if (!key) {
      // Fallback: no grouping for invalid dates.
      acc.push({ key: '', label: '', items: [msg] });
      return acc;
    }

    const last = acc[acc.length - 1];
    if (last && last.key === key) {
      last.items.push(msg);
    } else {
      acc.push({ key, label: getDateGroupLabel(msg.receivedAt), items: [msg] });
    }
    return acc;
  }, []);

  const flagGroupedMessages = [
    ...FLAG_COLORS.map((color) => ({
      key: `flag-${color}`,
      label: `${FLAG_LABELS[color]} Flag`,
      items: filteredMessages.filter((msg) => msg.flagColor === color),
    })).filter((group) => group.items.length > 0),
    {
      key: 'flag-none',
      label: 'No Flag',
      items: filteredMessages.filter((msg) => !msg.flagColor),
    },
  ].filter((group) => group.items.length > 0);

  const groupedMessages = groupByFlag ? flagGroupedMessages : dateGroupedMessages;


  const toggleSelected = (id: string) => {
    setSelectedIds((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  };

  const selectRange = (fromIndex: number, toIndex: number) => {
    const start = Math.min(fromIndex, toIndex);
    const end = Math.max(fromIndex, toIndex);
    const ids = filteredMessages.slice(start, end + 1).map((m) => m.id);
    setSelectedIds((prev) => Array.from(new Set([...prev, ...ids])));
  };

  const handleCheckboxClick = (index: number, id: string, e: React.MouseEvent) => {
    // Checkbox click: toggle selection by default (so you can multi-select without Ctrl).
    e.stopPropagation();
    if (e.shiftKey && selectionAnchor >= 0) {
      selectRange(selectionAnchor, index);
      return;
    }

    // If it's already selected, remove it; otherwise add it.
    toggleSelected(id);
    setSelectionAnchor(index);
  };

  const clearSelection = () => {
    setSelectedIds([]);
    setSelectionAnchor(-1);
  };

  // --- Message list column splitter ---
  const resizeStartRef = React.useRef<{ x: number; width: number } | null>(null);

  const handleResizerPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.currentTarget.setPointerCapture(e.pointerId);
    resizeStartRef.current = { x: e.clientX, width: listWidth };
    setIsResizingList(true);
  };

  const handleResizerPointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
    const start = resizeStartRef.current;
    if (!start) return;
    setListWidth(clamp(start.width + (e.clientX - start.x), LIST_WIDTH_MIN, maxListWidth()));
  };

  const handleResizerPointerUp = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!resizeStartRef.current) return;
    resizeStartRef.current = null;
    setIsResizingList(false);
    if (e.currentTarget.hasPointerCapture?.(e.pointerId)) {
      e.currentTarget.releasePointerCapture(e.pointerId);
    }
  };

  const handleResizerKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    const step = e.shiftKey ? 50 : 16;
    if (e.key === 'ArrowLeft') {
      e.preventDefault();
      setListWidth((w) => clamp(w - step, LIST_WIDTH_MIN, maxListWidth()));
    } else if (e.key === 'ArrowRight') {
      e.preventDefault();
      setListWidth((w) => clamp(w + step, LIST_WIDTH_MIN, maxListWidth()));
    } else if (e.key === 'Home') {
      e.preventDefault();
      setListWidth(LIST_WIDTH_DEFAULT);
    }
  };

  // A right-click on a row inside a multi-row selection acts on the whole selection.
  const actionTargetIds = (messageId: string) =>
    selectedIds.length > 1 && selectedIds.includes(messageId) ? selectedIds : [messageId];

  const allVisibleSelected =
    filteredMessages.length > 0 && filteredMessages.every((m) => selectedIds.includes(m.id));

  const toggleSelectAllVisible = () => {
    setSelectedIds(allVisibleSelected ? [] : filteredMessages.map((m) => m.id));
    setSelectionAnchor(-1);
  };

  const handleBulkMarkRead = async (isRead: boolean) => {
    if (!canWriteCurrentMailbox || selectedIds.length === 0) return;
    await onMarkReadMany(selectedIds, isRead);
    clearSelection();
  };

  const handleBulkMove = async (targetFolderId: string) => {
    if (!canWriteCurrentMailbox || !targetFolderId || selectedIds.length === 0) return;
    await onMoveMessages(selectedIds, targetFolderId);
    clearSelection();
  };

  const handleBulkDelete = async (permanent: boolean) => {
    if (!canWriteCurrentMailbox || selectedIds.length === 0) return;
    await onDeleteMessages(selectedIds, permanent);
    clearSelection();
  };

  const openFlagMenu = (messageIds: string[], x?: number, y?: number) => {
    if (!canWriteCurrentMailbox || messageIds.length === 0) return;
    setFlagMenu({
      messageIds,
      x: x ?? 180,
      y: y ?? 96,
    });
  };

  const applyFlag = async (color: FlagColor | null) => {
    if (!canWriteCurrentMailbox || !flagMenu) return;
    const ids = flagMenu.messageIds;
    setFlagMenu(null);
    await Promise.all(ids.map((id) => onFlagMessage(id, color)));
    clearSelection();
  };

  const getContextMenuTop = (y: number) => {
    const menuHeight = 330;
    return clamp(y, 8, Math.max(8, window.innerHeight - menuHeight - 8));
  };

  const getContextMenuStyle = (x: number, y: number): React.CSSProperties => {
    const menuWidth = 220;
    return {
      top: getContextMenuTop(y),
      left: clamp(x, 8, Math.max(8, window.innerWidth - menuWidth - 8)),
    };
  };

  useEffect(() => {
    // When folder changes, messages can temporarily be empty.
    // Reset selection + clear detail to avoid rendering stale detail while currentMessage is undefined.
    setSelectedMessageId(messages[0]?.id || '');
    setDetailMessage(null);
    setIsLoadingDetail(false);
    setContextMenu(null);
    setFlagMenu(null);
  }, [currentFolderId, messages]);

  // If context menu is open, keep UX stable: don't accidentally show bulk UI
  // while the menu is being used.
  useEffect(() => {
    if (contextMenu) return;
  }, [contextMenu]);

  // Switching folders drops the selection entirely; a message refresh only drops ids
  // that no longer exist, so a bulk action is not cancelled by its own refresh.
  useEffect(() => {
    clearSelection();
  }, [currentFolderId]);

  useEffect(() => {
    setSelectedIds((prev) => {
      const next = prev.filter((id) => messages.some((m) => m.id === id));
      return next.length === prev.length ? prev : next;
    });
  }, [messages]);

  // Dismiss the context menu on Escape, any outside click, or scroll/resize.
  useEffect(() => {
    if (!contextMenu && !flagMenu) return;

    const close = () => {
      setContextMenu(null);
      setFlagMenu(null);
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close();
    };

    // Capture-phase scroll fires for scrolls ANYWHERE, including the submenu's own
    // list. Closing on those would dismiss the menu while the user is scrolling
    // through folders, so ignore scrolls that originate inside the menu.
    const onScroll = (e: Event) => {
      const el = contextMenuRef.current;
      const target = e.target;
      if (el && target instanceof Node && el.contains(target)) return;
      close();
    };

    document.addEventListener('click', close);
    document.addEventListener('contextmenu', close);
    document.addEventListener('keydown', onKeyDown);
    window.addEventListener('scroll', onScroll, true);
    window.addEventListener('resize', close);

    return () => {
      document.removeEventListener('click', close);
      document.removeEventListener('contextmenu', close);
      document.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('scroll', onScroll, true);
      window.removeEventListener('resize', close);
    };
  }, [contextMenu, flagMenu]);

  useEffect(() => {
    let cancelled = false;

    const loadDetail = async () => {
      if (!selectedMessageId) return;
      if (!currentMessage) return;

      setIsLoadingDetail(true);
      try {
        // Detail fetch only if bodyHtml is missing.
        if (currentMessage.bodyHtml) {
          if (!cancelled) setDetailMessage(currentMessage);
          return;
        }

        const d = await webmailClient.getMessageDetail(currentMessage.mailboxId, selectedMessageId);
        if (!cancelled) setDetailMessage(d.data);
      } catch (e) {
        if (!cancelled) setDetailMessage(currentMessage);
      } finally {
        if (!cancelled) setIsLoadingDetail(false);
      }
    };

    loadDetail();

    return () => {
      cancelled = true;
    };
  }, [selectedMessageId]);

  const displayedMessage = detailMessage ?? currentMessage;

  const sortedSharedMailboxGroups = [...sharedMailboxGroups].sort((a, b) =>
    compareMailboxLabels(a.account.address || a.account.name || '', b.account.address || b.account.name || ''),
  );

  const toggleSharedMailboxCollapsed = (mailboxId: string) => {
    setCollapsedSharedMailboxIds((prev) =>
      prev.includes(mailboxId) ? prev.filter((id) => id !== mailboxId) : [...prev, mailboxId],
    );
  };

  const handleSharedMailboxHeaderKeyDown = (mailboxId: string, e: React.KeyboardEvent<HTMLDivElement>) => {
    if (e.key !== 'Enter' && e.key !== ' ') return;
    e.preventDefault();
    toggleSharedMailboxCollapsed(mailboxId);
  };

  const deleteFolderWithConfirm = async (folderId: string) => {
    if (!canWriteCurrentMailbox) return;
    const maybeCount = getFolderMessageCount?.(folderId);

    const hasMails = typeof maybeCount === 'number' && maybeCount > 0;
    const label = hasMails
      ? `Delete this folder and ALL messages inside?${maybeCount != null ? ` (${maybeCount} messages)` : ''}`
      : 'Delete this folder?';

    const confirmed = window.confirm(label);
    if (!confirmed) return;

    await onDeleteFolder(folderId);
  };

  const renderCustomFolderLinks = (folders: Mailbox[], mailboxId: string, parentId: string | null = null, level = 0): React.ReactNode =>
    sortCustomFolders(folders.filter((folder) => folder.role === 'custom' && (folder.parentId ?? null) === parentId))
      .map((folder) => (
        <React.Fragment key={folder.id}>
          <a
            data-folder-id={folder.id}
            className={`wm-nav-item ${currentMailboxId === mailboxId && currentFolderId === folder.id ? 'active' : ''}`}
            href={`#${folder.id}`}
            draggable={canWriteCurrentMailbox}
            onDragStart={(e) => {
              if (!canWriteCurrentMailbox) return;
              e.dataTransfer.setData('application/x-miautrix-folder-id', folder.id);
              e.dataTransfer.effectAllowed = 'move';
            }}
            onClick={(e) => {
              e.preventDefault();
              onFolderChange(mailboxId, folder.id);
            }}
            onContextMenu={(e) => {
              e.preventDefault();
              e.stopPropagation();
              if (!canWriteCurrentMailbox) return;

              const personalFolders = mailboxes.filter((f) => f.role === 'custom');
              const action = prompt(
                `Folder “${folder.name}”: type a command\n\nType:\n 1) move\n 2) delete\n\n(Then press Enter)`
              );

              if (action === null) return;

              const cmd = action.trim().toLowerCase();

              if (cmd === 'delete' || cmd === 'remove') {
                void deleteFolderWithConfirm(folder.id);
                return;
              }

              // default: move
              const parentName = prompt(
                `Move folder “${folder.name}” under which personal folder?\n\nLeave blank for root.\n\nExamples: ${personalFolders
                  .slice(0, 8)
                  .map((f) => f.name)
                  .join(', ')}`,
              );

              if (parentName === null) return;

              const trimmed = parentName.trim();
              let newParentId: string | null = null;
              if (trimmed.length > 0) {
                const match = personalFolders.find(
                  (f) => f.id !== folder.id && f.name.toLowerCase() === trimmed.toLowerCase(),
                );
                if (!match) {
                  alert('Parent folder not found.');
                  return;
                }
                newParentId = match.id;
              }

              onMoveFolder(folder.id, newParentId).catch((err) => {
                console.error('Failed to move folder', { folderId: folder.id, parentId: newParentId, err });
                alert(`Failed to move folder: ${err?.message ?? String(err)}`);
              });
            }}
            onDragOver={(e) => {
              if (!canWriteCurrentMailbox) return;
              e.preventDefault();
              e.stopPropagation();
              e.currentTarget.classList.add('drag-over');
            }}
            onDragLeave={(e) => {
              e.preventDefault();
              e.stopPropagation();
              e.currentTarget.classList.remove('drag-over');
            }}
            onDrop={(e) => {
              if (!canWriteCurrentMailbox) return;
              e.preventDefault();
              e.stopPropagation();
              e.currentTarget.classList.remove('drag-over');

              const movedFolderId = e.dataTransfer.getData('application/x-miautrix-folder-id');
              if (!movedFolderId) return;

              const targetId = (e.currentTarget as HTMLElement).closest('[data-folder-id]')?.getAttribute('data-folder-id');
              const parentId = targetId ?? folder.id;

              if (movedFolderId === parentId) {
                alert('Folder cannot be moved inside itself.');
                return;
              }

              onMoveFolder(movedFolderId, parentId).catch((err) => {
                console.error('Failed to move folder', { folderId: movedFolderId, parentId, err });
                alert(`Failed to move folder: ${err?.message ?? String(err)}`);
              });
            }}
          >
            <div className="wm-nav-icon" style={{ paddingLeft: `${20 + level * 14}px` }}>
              <img src="/images/icons/webmail/inbox.png" alt="" />
              <span>{folder.name}</span>
            </div>
          </a>
          {renderCustomFolderLinks(folders, mailboxId, folder.id, level + 1)}
        </React.Fragment>
      ));

  const handlePrint = () => {
    // Context-menu sets selectedMessageId + displayedMessage; scoped CSS in print mode
    // will ensure only the reading pane content is visible/printed.
    window.print();
  };

  return (
    <div className={`webmail-layout ${isResizingList ? 'is-resizing-col' : ''}`}>
      {/* Ribbon Action Bar for Inbox */}
      <div className="wm-ribbon">
        <div className="wm-ribbon-group">
          <button
            type="button"
            className="btn btn-primary"
            style={{ padding: '6px 14px', fontSize: '14px' }}
            onClick={onComposeClick}
          >
            <img
              src="/images/icons/webmail/compose-pencil.png"
              alt=""
              style={{ width: '14px', filter: 'brightness(0) invert(1)' }}
            />
            New Message
          </button>
        </div>

        <div className="wm-ribbon-group">
          <button
            type="button"
            className="wm-tool-btn"
            title="Refresh"
            aria-label="Refresh"
            onClick={onRefreshMessages}
          >
            <svg
              width="18"
              height="18"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path d="M21 12a9 9 0 0 1-9 9" />
              <path d="M3 12a9 9 0 0 1 9-9" />
              <polyline points="21 3 21 12 12 12" />
              <polyline points="3 21 3 12 12 12" />
            </svg>
          </button>
          <button
            type="button"
            className="wm-tool-btn"
            disabled={!canWriteCurrentMailbox}
            title="Delete"
            aria-label="Delete"
            onClick={() => {
              if (selectedIds.length > 0) {
                void handleBulkDelete(false);
                return;
              }
              displayedMessage && onDeleteMessage(displayedMessage.id);
            }}
          >
            <img src="/images/icons/webmail/trash.png" alt="Delete" />
          </button>
          <button
            type="button"
            className="wm-tool-btn"
            disabled={!canWriteCurrentMailbox}
            title="Archive"
            aria-label="Archive"
            onClick={() => displayedMessage && onArchiveSelected(displayedMessage.id)}
          >
            <img src="/images/icons/webmail/archive.png" alt="Archive" />
          </button>
          <button
            type="button"
            className="wm-tool-btn"
            disabled={!canWriteCurrentMailbox}
            title="Mark as Junk"
            aria-label="Junk"
            onClick={() => displayedMessage && onJunkSelected(displayedMessage.id)}
          >
            <img src="/images/icons/webmail/security.png" alt="Junk" />
          </button>
          <button
            type="button"
            className="wm-tool-btn"
            disabled={!canWriteCurrentMailbox}
            title="Flag"
            aria-label="Flag"
            onClick={(e) => {
              e.stopPropagation();
              const rect = e.currentTarget.getBoundingClientRect();
              const ids = selectedIds.length > 0
                ? selectedIds
                : displayedMessage
                  ? [displayedMessage.id]
                  : [];
              openFlagMenu(ids, rect.left, rect.bottom + 6);
            }}
          >
            <img src="/images/icons/webmail/flag.png" alt="Flag" />
          </button>
        </div>

        <div className="wm-ribbon-group">
          <button
            type="button"
            className="wm-tool-btn"
            title="Print"
            aria-label="Print"
            onClick={() => {
              void handlePrint();
            }}
          >
            <img src="/images/icons/webmail/print.png" alt="Print" />
          </button>
          <button
            type="button"
            className="wm-tool-btn"
            title="Filter Rules"
            aria-label="Filter Rules"
            onClick={onOpenRulesClick}
          >
            <img src="/images/icons/webmail/settings.png" alt="Rules" />
          </button>
        </div>
      </div>

      {/* 3-Column Layout: Folders Sidebar -> Message List -> Reading Pane */}
      <div className="inbox-grid">
        {/* Left Sidebar */}
        <aside className="wm-sidebar" aria-label="Folders Sidebar">
          <div className="wm-nav-section">
            <div className="wm-nav-head">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <polyline points="6 9 12 15 18 9"></polyline>
              </svg>
              Folders
            </div>

            {sortSystemFolders(mailboxes.filter((f) => f.role !== 'custom'))
              .map((mb) => (
                <a
                  key={mb.id}
                  className={`wm-nav-item ${currentFolderId === mb.id ? 'active' : ''}`}
                  href={`#${mb.id}`}
                  onClick={(e) => {
                    e.preventDefault();
                    onFolderChange(mb.mailboxId ?? currentMailboxId, mb.id);
                  }}
                  onDragOver={(e) => {
                    if (!canWriteCurrentMailbox) return;
                    e.preventDefault();
                    e.currentTarget.classList.add('drag-over');
                  }}
                  onDragLeave={(e) => {
                    e.currentTarget.classList.remove('drag-over');
                  }}
                  onDrop={(e) => {
                    if (!canWriteCurrentMailbox) return;
                    e.preventDefault();
                    e.currentTarget.classList.remove('drag-over');
                    const movedFolderId = e.dataTransfer.getData('application/x-miautrix-folder-id');
                    if (movedFolderId) {
                      if (movedFolderId === mb.id) {
                        alert('Folder cannot be moved inside itself.');
                        return;
                      }

                      onMoveFolder(movedFolderId, mb.id).catch((err) => {
                        console.error('Failed to move folder', {
                          folderId: movedFolderId,
                          parentId: mb.id,
                          err,
                        });
                        alert(`Failed to move folder: ${err?.message ?? String(err)}`);
                      });
                      return;
                    }

                    const messageId = e.dataTransfer.getData('text/plain');
                    if (messageId) onMoveMessage(messageId, mb.id);
                  }}
                >
                  <div className="wm-nav-icon">
                    <img src={`/images/icons/webmail/${mb.icon}`} alt="" />
                    <span>{mb.name}</span>
                  </div>
                  {mb.unreadEmails > 0 && <span className="badge">{mb.unreadEmails}</span>}
                </a>
              ))}
          </div>

          <div className="wm-nav-section">
            <div
              className="wm-nav-head"
              style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
            >
              <span>Fersonal Folders</span>
              <button
                type="button"
                className="btn btn-ghost"
                disabled={!canWriteCurrentMailbox}
                style={{ padding: '0 4px', cursor: 'pointer' }}
                onClick={async (e) => {
                  e.stopPropagation();
                  if (!canWriteCurrentMailbox) return;

                  const name = prompt('Enter folder name:');
                  if (!name) return;

                  // Enable nesting without changing backend contract/UI too much:
                  // parent is chosen by name from existing personal folders.
                  const personalFolders = mailboxes.filter((f) => f.role === 'custom');
                  const parentName = prompt(
                    'Optional: parent folder name (leave empty for root).\n\nExamples: ' +
                      personalFolders.slice(0, 8).map((f) => f.name).join(', '),
                  );

                  let parentId: string | null = null;
                  if (parentName && parentName.trim().length > 0) {
                    const match = personalFolders.find(
                      (f) => f.name.toLowerCase() === parentName.trim().toLowerCase(),
                    );
                    if (match) parentId = match.id;
                  }

                  await onCreateFolder(name, parentId);
                }}
              >
                +
              </button>
            </div>

            {renderCustomFolderLinks(mailboxes, currentMailboxId)}
          </div>

          {sharedMailboxGroups.length > 0 && (
            <div className="wm-nav-section">
              <div className="wm-nav-head">Shared Mailboxes</div>
              {sortedSharedMailboxGroups.map((group) => {
                const systemFolders = sortSystemFolders(group.folders.filter((f) => f.role !== 'custom'));
                const customFolders = group.folders.filter((f) => f.role === 'custom');
                const isCollapsed = collapsedSharedMailboxIds.includes(group.account.id);
                const label = group.account.address || group.account.name;
                return (
                  <div key={group.account.id}>
                    <div
                      className="wm-nav-head wm-shared-mailbox-head"
                      style={{ paddingLeft: '16px', fontSize: '13px', cursor: 'pointer' }}
                      role="button"
                      tabIndex={0}
                      aria-expanded={!isCollapsed}
                      onClick={() => toggleSharedMailboxCollapsed(group.account.id)}
                      onKeyDown={(e) => handleSharedMailboxHeaderKeyDown(group.account.id, e)}
                    >
                      <svg
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                        style={{ transform: isCollapsed ? 'rotate(-90deg)' : undefined }}
                      >
                        <polyline points="6 9 12 15 18 9"></polyline>
                      </svg>
                      <span>{label}</span>
                    </div>
                    {!isCollapsed && (
                      <>
                        {systemFolders.map((folder) => (
                          <a
                            key={folder.id}
                            className={`wm-nav-item ${currentMailboxId === group.account.id && currentFolderId === folder.id ? 'active' : ''}`}
                            href={`#${folder.id}`}
                            onClick={(e) => {
                              e.preventDefault();
                              onFolderChange(group.account.id, folder.id);
                            }}
                          >
                            <div className="wm-nav-icon">
                              <img src={`/images/icons/webmail/${folder.icon}`} alt="" />
                              <span>{folder.name}</span>
                            </div>
                            {folder.unreadEmails > 0 && <span className="badge">{folder.unreadEmails}</span>}
                          </a>
                        ))}
                        {customFolders.length > 0 && (
                          <>
                            <div className="wm-context-separator" />
                            <div className="wm-nav-head" style={{ paddingLeft: '38px', fontSize: '13px' }}>Folders</div>
                            {renderCustomFolderLinks(group.folders, group.account.id)}
                          </>
                        )}
                      </>
                    )}
                  </div>
                );
              })}
            </div>
          )}

          <div className="wm-nav-section" style={{ marginTop: 'auto', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--neutral-body)', marginBottom: '6px' }}>
              Mailbox Quota ({formatBytes(quotaUsedBytes)} / {quotaBytes > 0 ? formatBytes(quotaBytes) : 'Unlimited'})
            </div>
            <div style={{ height: '6px', background: '#e2e8f0', borderRadius: '3px', overflow: 'hidden' }}>
              <div
                style={{
                  width: quotaBytes > 0 ? `${Math.min(100, Math.max(0, (quotaUsedBytes / quotaBytes) * 100))}%` : '0%',
                  height: '100%',
                  background: 'var(--iris-violet)',
                }}
              ></div>
            </div>
          </div>
        </aside>

        {/* Center Message List */}
        <section
          className="msg-list-col"
          aria-label="Message List"
          style={{ width: listWidth, flex: `0 0 ${listWidth}px` }}
        >
          <div className="msg-search-bar">
            <button
              type="button"
              className={`btn ${groupByFlag ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '6px 10px', fontSize: '12px', whiteSpace: 'nowrap' }}
              onClick={() => setGroupByFlag((value) => !value)}
              title="Group messages by flag color"
            >
              Flags
            </button>
            <input
              type="checkbox"
              className="msg-select-box"
              title="Select all"
              checked={allVisibleSelected}
              onChange={toggleSelectAllVisible}
              onClick={(e) => e.stopPropagation()}
              aria-label="Select all messages"
            />
            <input
              className="input-base"
              type="text"
              placeholder="Search in mailbox..."
              style={{ padding: '6px 12px', fontSize: '14px', flex: 1 }}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              aria-label="Search in mailbox"
            />
          </div>

          {false && (
            <div className="msg-bulk-bar" role="toolbar" aria-label="Bulk actions">
              <span className="msg-bulk-count">{selectedIds.length} selected</span>
              <button type="button" className="btn btn-secondary" onClick={() => handleBulkMarkRead(true)}>
                Mark read
              </button>
              <button type="button" className="btn btn-secondary" onClick={() => handleBulkMarkRead(false)}>
                Mark unread
              </button>
              <select
                className="input-base"
                value=""
                onChange={(e) => handleBulkMove(e.target.value)}
                aria-label="Move selected to folder"
              >
                <option value="" disabled>
                  Move to…
                </option>
                {mailboxes
                  .filter((f) => f.id !== currentFolderId)
                  .map((f) => (
                    <option key={f.id} value={f.id}>
                      {f.role !== 'custom' ? f.name : `📁 ${f.name}`}
                    </option>
                  ))}
              </select>
              <button type="button" className="btn btn-secondary" onClick={() => handleBulkDelete(false)}>
                Move to Trash
              </button>
              <button type="button" className="btn btn-ghost" onClick={clearSelection}>
                Clear
              </button>
            </div>
          )}

          <div style={{ flex: 1, overflowY: 'auto' }} role="list">
            {filteredMessages.length === 0 ? (
              <div style={{ padding: '24px', textAlign: 'center', color: 'var(--neutral-body)' }}>
                No messages in this folder
              </div>
            ) : (
              groupedMessages.map((group) => (
                <React.Fragment key={group.key || group.items[0]?.id}>
                  {group.label && (
                    <div className="wm-date-group">
                      {group.label}
                    </div>
                  )}

                  {group.items.map((msg) => (
                    <div
                      key={msg.id}
                      className={`msg-row ${selectedIds.includes(msg.id) ? 'selected' : ''}`}
                    >
                      <input declare-hidden-index="true"
                    type="checkbox"
                    className="msg-select-box"
                    checked={selectedIds.includes(msg.id)}
                    onChange={() => {
                      // Controlled input requirement: state is updated by onClick/toggleSelected
                    }}
                    onClick={(e) => handleCheckboxClick(filteredMessages.findIndex((m) => m.id === msg.id), msg.id, e)}
                    aria-label={`Select ${msg.from.email}`}
                  />
                  <div
                    role="listitem"
                    className={`msg-item ${selectedMessageId === msg.id ? 'active' : ''} ${
                      msg.isUnread ? 'unread' : ''
                    }`}
                    draggable={canWriteCurrentMailbox}
                    onDragStart={(e) => {
                      if (!canWriteCurrentMailbox) return;
                      e.dataTransfer.setData('text/plain', msg.id);
                    }}
                    onClick={() => {
                      setSelectedMessageId(msg.id);
                      const activeFolderRole = [...mailboxes, ...sharedMailboxGroups.flatMap((group) => group.folders)]
                        .find((folder) => folder.id === currentFolderId)?.role;
                      if (activeFolderRole === 'drafts') {
                        void hydrateMessageDetail(msg).then(onEditDraft);
                        return;
                      }
                      if (canWriteCurrentMailbox && msg.isUnread) onMarkRead(msg.id, true);
                    }}
                    onContextMenu={(e) => {
                      e.preventDefault();
                      e.stopPropagation();
                      setSelectedMessageId(msg.id);
                      setContextMenu({
                        x: e.clientX,
                        y: e.clientY,
                        messageId: msg.id,
                        isUnread: msg.isUnread,
                      });
                    }}
                  >
                    <div className="msg-from">
                      <span>
                        {msg.isUnread ? '● ' : ''}
                        {msg.flagColor && (
                          <span
                            style={{
                              display: 'inline-block',
                              width: '8px',
                              height: '8px',
                              borderRadius: '50%',
                              backgroundColor: `var(--flag-${msg.flagColor})`,
                              marginRight: '6px',
                            }}
                            title={`Flag: ${msg.flagColor}`}
                          />
                        )}
                        {msg.from.email}
                      </span>
                      <span className="msg-time">{formatReceivedAt(msg.receivedAt)}</span>
                    </div>
                    <div className="msg-subj" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <img
                        src={statusIconSrc(msg.isUnread)}
                        alt=""
                        style={{ width: '14px', opacity: msg.isUnread ? 1 : 0.7 }}
                      />
                      <span>{msg.subject}</span>
                    </div>
                    <div className="msg-snippet" style={{ color: 'var(--neutral-body)', fontFamily: 'var(--font-mono)' }}>
                      {clampSnippet(msg.snippet || '', 100)}
                    </div>
                  </div>
                </div>
                  ))}
                </React.Fragment>
              ))
            )}
          </div>
        </section>

        {/* Splitter: drag to resize the message list column. */}
        <div
          className="wm-col-resizer"
          role="separator"
          aria-orientation="vertical"
          aria-label="Resize message list"
          aria-valuenow={listWidth}
          aria-valuemin={LIST_WIDTH_MIN}
          aria-valuemax={maxListWidth()}
          tabIndex={0}
          title="Drag to resize · double-click to reset"
          onPointerDown={handleResizerPointerDown}
          onPointerMove={handleResizerPointerMove}
          onPointerUp={handleResizerPointerUp}
          onPointerCancel={handleResizerPointerUp}
          onDoubleClick={() => setListWidth(LIST_WIDTH_DEFAULT)}
          onKeyDown={handleResizerKeyDown}
        />

        {/* Right Reading Pane */}
        <main id="wm-print-email" className="reading-col" aria-label="Reading Pane">
          {displayedMessage ? (
            <>
              <div className="reading-header" style={{ paddingBottom: '14px' }}>
                <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '12px' }}>
                  <h1 className="reading-title" style={{ marginBottom: '10px' }}>{displayedMessage.subject}</h1>
                  <div style={{ display: 'flex', gap: '8px', flexShrink: 0 }}>
                    <button
                      type="button"
                      className="btn btn-secondary"
                      style={{ padding: '6px 12px', fontSize: '13px' }}
                      onClick={() => {
                        void (async () => {
                          const full = await hydrateMessageDetail(displayedMessage);
                          onReplyMessage(full);
                        })();
                      }}
                    >
                      Reply
                    </button>
                    <button
                      type="button"
                      className="btn btn-secondary"
                      style={{ padding: '6px 12px', fontSize: '13px' }}
                      onClick={() => {
                        void (async () => {
                          const full = await hydrateMessageDetail(displayedMessage);
                          onForwardMessage(full);
                        })();
                      }}
                    >
                      Forward
                    </button>
                  </div>
                </div>

                <div className="sender-card">
                  <div className="sender-details">
                    <div className="sender-avatar">
                      {currentMessage.from.name
                        .split(' ')
                        .map((n) => n[0])
                        .join('')
                        .substring(0, 2)
                        .toUpperCase()}
                    </div>
                    <div>
                      <div
                        style={{
                          fontWeight: 600,
                          color: 'var(--deep-navy)',
                          fontSize: '15px',
                          display: 'flex',
                          alignItems: 'center',
                          flexWrap: 'wrap',
                          gap: '6px',
                        }}
                      >
                        {displayedMessage.from.name} &lt;{displayedMessage.from.email}&gt;
                        {displayedMessage.securityChecks.spfPass && displayedMessage.securityChecks.dkimPass && (
                          <span className="security-badge" title="Verified SPF and DKIM signatures">
                            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                              <polyline points="20 6 9 17 4 12"></polyline>
                            </svg>
                            SPF & DKIM Valid
                          </span>
                        )}
                      </div>
                      <div style={{ fontSize: '13px', color: 'var(--neutral-body)' }}>
                        To: {displayedMessage.to.map((t) => t.email).join(', ')}
                      </div>
                      {displayedMessage.cc && displayedMessage.cc.length > 0 && (
                        <div style={{ fontSize: '13px', color: 'var(--neutral-body)' }}>
                          Cc: {displayedMessage.cc.map((t) => t.email).join(', ')}
                        </div>
                      )}
                      {displayedMessage.bcc && displayedMessage.bcc.length > 0 && (
                        <div style={{ fontSize: '13px', color: 'var(--neutral-body)' }}>
                          Bcc: {displayedMessage.bcc.map((t) => t.email).join(', ')}
                        </div>
                      )}
                    </div>
                  </div>

                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: '13px', color: 'var(--neutral-body)' }}>
                    {displayedMessage.receivedAt}
                  </div>
                </div>
              </div>

              {/* Attachments Section (top) */}
              {displayedMessage.attachments.length > 0 && (
                <div style={{ marginBottom: '24px', paddingBottom: '20px', borderBottom: '1px solid var(--neutral-border)' }}>
                  <div style={{ fontWeight: 500, fontSize: '14px', color: 'var(--deep-navy)', marginBottom: '12px' }}>
                    Attachments ({displayedMessage.attachments.length} file{displayedMessage.attachments.length > 1 ? 's' : ''})
                  </div>
                  <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
                    {displayedMessage.attachments.map((att) => (
                      <div
                        key={att.id}
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '10px',
                          padding: '10px 16px',
                          border: '1px solid var(--neutral-border)',
                          borderRadius: 'var(--radius-sm)',
                          background: 'var(--surface-canvas)',
                        }}
                      >
                        <img src="/images/icons/webmail/attach-file.png" alt="" style={{ width: '18px', opacity: 0.7 }} />
                        <div>
                          <div style={{ fontSize: '14px', fontWeight: 500, color: 'var(--deep-navy)' }}>{att.name}</div>
                          <div style={{ fontSize: '12px', color: 'var(--neutral-body)', fontFamily: 'var(--font-mono)' }}>
                            {(att.size / 1024).toFixed(0)} KB · {att.contentType}
                          </div>
                        </div>
                        <button
                          type="button"
                          className="btn btn-secondary"
                          style={{ padding: '4px 10px', fontSize: '12px', marginLeft: '12px' }}
                          onClick={() => {
                            // Backend provides attachment download URL as `blobId`.
                            // Use it directly; it already includes the correct /api/v1/messages/.../attachments/... route.
                            const downloadPath = att.blobId
                              ? att.blobId
                              : `/api/v1/messages/${displayedMessage.id}/attachments/${att.id}`;

                            // Use in-app fetch download so the browser never navigates to the API URL.
                            // (Also avoids exposing the API URL in address bar.)
                            void webmailClient.downloadAttachment(downloadPath, att.name);
                            return;
                          }}
                        >
                          Download
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Sanitized Message Body */}
              {isLoadingDetail ? (
                <div className="reading-body">Loading message content…</div>
              ) : displayedMessage.bodyHtml ? (
                <div className="reading-body">
                  <SanitizedMessageBody htmlContent={displayedMessage.bodyHtml} />
                </div>
              ) : (
                <div className="reading-body"
                  dangerouslySetInnerHTML={{
                    __html: buildBodyFallbackHtml(displayedMessage.bodyText),
                  }}
                />
              )}

            </>
          ) : (
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', color: 'var(--neutral-body)' }}>
              Select a message to view its contents
            </div>
          )}
        </main>
      </div>

      {flagMenu && (
        <div
          className="wm-context-menu"
          role="menu"
          style={getContextMenuStyle(flagMenu.x, flagMenu.y)}
          onClick={(e) => e.stopPropagation()}
        >
          <div className="wm-context-empty">
            Flag {flagMenu.messageIds.length > 1 ? `${flagMenu.messageIds.length} messages` : 'message'}
          </div>
          {FLAG_COLORS.map((color) => (
            <div
              key={color}
              className="wm-context-item"
              role="menuitem"
              onClick={() => void applyFlag(color)}
            >
              <span
                style={{
                  display: 'inline-block',
                  width: '10px',
                  height: '10px',
                  borderRadius: '50%',
                  backgroundColor: `var(--flag-${color})`,
                  marginRight: '8px',
                }}
              />
              {FLAG_LABELS[color]}
            </div>
          ))}
          <div className="wm-context-separator" />
          <div className="wm-context-item" role="menuitem" onClick={() => void applyFlag(null)}>
            Clear Flag
          </div>
        </div>
      )}

      {contextMenu && (
        <div
          ref={contextMenuRef}
          className={`wm-context-menu ${getContextMenuTop(contextMenu.y) > window.innerHeight - 420 ? 'submenu-up' : ''}`}
          role="menu"
          style={getContextMenuStyle(contextMenu.x, contextMenu.y)}
          onClick={(e) => e.stopPropagation()}
        >
          <div
            className="wm-context-item"
            role="menuitem"
            onClick={() => {
              onMarkRead(contextMenu.messageId, true);
              setContextMenu(null);
            }}
          >
            Open
          </div>

          {canWriteCurrentMailbox && (
            <>
              <div className="wm-context-separator" />

              <div
                className="wm-context-item"
                role="menuitem"
                onClick={() => {
                  const ids = actionTargetIds(contextMenu.messageId);
                  const isRead = contextMenu.isUnread;
                  setContextMenu(null);
                  onMarkReadMany(ids, isRead);
                }}
              >
                Mark as {contextMenu.isUnread ? 'Read' : 'Unread'}
                {selectedIds.length > 1 && selectedIds.includes(contextMenu.messageId)
                  ? ` (${selectedIds.length})`
                  : ''}
              </div>
            </>
          )}

          {canWriteCurrentMailbox && (
            <>
              <div className="wm-context-separator" />

              <div className="wm-context-submenu-wrap">
            <div className="wm-context-item" role="menuitem">
              Move to Folder
            </div>
            <div className="wm-context-submenu" role="menu">
              {(() => {
                const systemFolders = mailboxes.filter(
                  (f) => f.id !== currentFolderId && f.role !== 'custom',
                );
                const personalFolders = mailboxes.filter(
                  (f) => f.id !== currentFolderId && f.role === 'custom',
                );

                if (systemFolders.length + personalFolders.length === 0) {
                  return <div className="wm-context-empty">No other folders</div>;
                }

                return (
                  <>
                    {systemFolders.length > 0 && (
                      <>
                        <div className="wm-context-empty">System</div>
                        {systemFolders.map((f) => (
                          <div
                            key={f.id}
                            className="wm-context-item"
                            role="menuitem"
                            onMouseDown={(e) => {
                              e.preventDefault();
                              e.stopPropagation();
                            }}
                            onClick={(e) => {
                              e.preventDefault();
                              e.stopPropagation();
                              const ids = actionTargetIds(contextMenu.messageId);
                              setContextMenu(null);
                              if (ids.length > 1) {
                                onMoveMessages(ids, f.id);
                              } else {
                                onMoveMessage(ids[0], f.id);
                              }
                            }}
                          >
                            {f.name}
                          </div>
                        ))}
                      </>
                    )}

                    {personalFolders.length > 0 && (
                      <>
                        {systemFolders.length > 0 && <div className="wm-context-separator" />}
                        <div className="wm-context-empty">Personal</div>
                        {personalFolders.map((f) => (
                          <div
                            key={f.id}
                            className="wm-context-item"
                            role="menuitem"
                            onMouseDown={(e) => {
                              e.preventDefault();
                              e.stopPropagation();
                            }}
                            onClick={(e) => {
                              e.preventDefault();
                              e.stopPropagation();
                              const ids = actionTargetIds(contextMenu.messageId);
                              setContextMenu(null);
                              if (ids.length > 1) {
                                onMoveMessages(ids, f.id);
                              } else {
                                onMoveMessage(ids[0], f.id);
                              }
                            }}
                          >
                            {f.name}
                          </div>
                        ))}
                      </>
                    )}
                  </>
                );
              })()}
            </div>
          </div>

          <div className="wm-context-separator" />

          <div
            className="wm-context-item"
            role="menuitem"
            onClick={() => {
              const ids = actionTargetIds(contextMenu.messageId);
              setContextMenu(null);
              onDeleteMessages(ids, false);
            }}
          >
            Move to Trash
          </div>

          <div
            className="wm-context-item danger"
            role="menuitem"
            onClick={() => {
              setContextMenu(null);
              const confirmed = window.confirm(
                'Permanently delete this message? This cannot be undone.',
              );
              if (confirmed) onDeleteMessages(actionTargetIds(contextMenu.messageId), true);
            }}
          >
            Delete Permanently
          </div>

          <div className="wm-context-separator" />

          <div className="wm-context-submenu-wrap">
            <div className="wm-context-item" role="menuitem">
              Flag
              {selectedIds.length > 1 && selectedIds.includes(contextMenu.messageId)
                ? ` (${selectedIds.length})`
                : ''}
            </div>
            <div className="wm-context-submenu" role="menu">
              {FLAG_COLORS.map((color) => (
                <div
                  key={color}
                  className="wm-context-item"
                  role="menuitem"
                  onMouseDown={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                  }}
                  onClick={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    const ids = actionTargetIds(contextMenu.messageId);
                    setContextMenu(null);
                    void Promise.all(ids.map((id) => onFlagMessage(id, color))).then(clearSelection);
                  }}
                >
                  <span
                    style={{
                      display: 'inline-block',
                      width: '10px',
                      height: '10px',
                      borderRadius: '50%',
                      backgroundColor: `var(--flag-${color})`,
                      marginRight: '8px',
                    }}
                  />
                  {FLAG_LABELS[color]}
                </div>
              ))}
              <div className="wm-context-separator" />
              <div
                className="wm-context-item"
                role="menuitem"
                onMouseDown={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                }}
                onClick={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                  const ids = actionTargetIds(contextMenu.messageId);
                  setContextMenu(null);
                  void Promise.all(ids.map((id) => onFlagMessage(id, null))).then(clearSelection);
                }}
              >
                Clear Flag
              </div>
            </div>
              </div>
            </>
          )}

          <div className="wm-context-separator" />

          <div
            className="wm-context-item"
            role="menuitem"
            onClick={() => {
              setContextMenu(null);
              void handlePrint();
            }}
          >
            Print
          </div>
        </div>
      )}
    </div>
  );
};
