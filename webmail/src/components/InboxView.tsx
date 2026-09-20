import React, { useState } from 'react';

import type { Mailbox, EmailMessage } from '../types';
import { SanitizedMessageBody } from './SanitizedMessageBody';

interface InboxViewProps {
  mailboxes: Mailbox[];
  messages: EmailMessage[];
  onComposeClick: () => void;
  onOpenRulesClick: () => void;
}

export const InboxView: React.FC<InboxViewProps> = ({
  mailboxes,
  messages,
  onComposeClick,
  onOpenRulesClick,
}) => {
  const [selectedMailboxId, setSelectedMailboxId] = useState<string>('inbox');
  const [selectedMessageId, setSelectedMessageId] = useState<string>(messages[0]?.id || '');
  const [searchQuery, setSearchQuery] = useState<string>('');

  const filteredMessages = messages.filter((msg) => {
    const matchesMailbox = selectedMailboxId === 'all' || msg.mailboxId === selectedMailboxId;
    const matchesSearch =
      !searchQuery ||
      msg.subject.toLowerCase().includes(searchQuery.toLowerCase()) ||
      msg.from.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      msg.from.email.toLowerCase().includes(searchQuery.toLowerCase()) ||
      msg.snippet.toLowerCase().includes(searchQuery.toLowerCase());
    return matchesMailbox && matchesSearch;
  });

  const currentMessage = messages.find((m) => m.id === selectedMessageId) || filteredMessages[0];

  return (
    <div className="webmail-layout">
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
          <button type="button" className="wm-tool-btn" title="Delete" aria-label="Delete">
            <img src="/images/icons/webmail/trash.png" alt="Delete" />
          </button>
          <button type="button" className="wm-tool-btn" title="Archive" aria-label="Archive">
            <img src="/images/icons/webmail/archive.png" alt="Archive" />
          </button>
          <button type="button" className="wm-tool-btn" title="Mark as Junk" aria-label="Junk">
            <img src="/images/icons/webmail/security.png" alt="Junk" />
          </button>
          <button type="button" className="wm-tool-btn" title="Flag" aria-label="Flag">
            <img src="/images/icons/webmail/flag.png" alt="Flag" />
          </button>
        </div>

        <div className="wm-ribbon-group">
          <button type="button" className="wm-tool-btn" title="Print" aria-label="Print">
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

            {mailboxes.map((mb) => (
              <a
                key={mb.id}
                className={`wm-nav-item ${selectedMailboxId === mb.id ? 'active' : ''}`}
                href={`#${mb.id}`}
                onClick={(e) => {
                  e.preventDefault();
                  setSelectedMailboxId(mb.id);
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

          <div className="wm-nav-section" style={{ marginTop: 'auto', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--neutral-body)', marginBottom: '6px' }}>
              Mailbox Quota (1.2 GB / 5 GB)
            </div>
            <div style={{ height: '6px', background: '#e2e8f0', borderRadius: '3px', overflow: 'hidden' }}>
              <div style={{ width: '24%', height: '100%', background: 'var(--iris-violet)' }}></div>
            </div>
          </div>
        </aside>

        {/* Center Message List */}
        <section className="msg-list-col" aria-label="Message List">
          <div className="msg-search-bar">
            <input
              className="input-base"
              type="text"
              placeholder="Search in mailbox..."
              style={{ padding: '6px 12px', fontSize: '14px' }}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              aria-label="Search in mailbox"
            />
          </div>

          <div style={{ flex: 1, overflowY: 'auto' }} role="list">
            {filteredMessages.length === 0 ? (
              <div style={{ padding: '24px', textAlign: 'center', color: 'var(--neutral-body)' }}>
                No messages in this folder
              </div>
            ) : (
              filteredMessages.map((msg) => (
                <div
                  key={msg.id}
                  role="listitem"
                  className={`msg-item ${selectedMessageId === msg.id ? 'active' : ''} ${
                    msg.isUnread ? 'unread' : ''
                  }`}
                  onClick={() => setSelectedMessageId(msg.id)}
                >
                  <div className="msg-from">
                    <span>{msg.from.name}</span>
                    <span className="msg-time">{msg.receivedAt}</span>
                  </div>
                  <div className="msg-subj">{msg.subject}</div>
                  <div className="msg-snippet">{msg.snippet}</div>
                </div>
              ))
            )}
          </div>
        </section>

        {/* Right Reading Pane */}
        <main className="reading-col" aria-label="Reading Pane">
          {currentMessage ? (
            <>
              <div className="reading-header">
                <h1 className="reading-title">{currentMessage.subject}</h1>

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
                        {currentMessage.from.name} &lt;{currentMessage.from.email}&gt;
                        {currentMessage.securityChecks.spfPass && currentMessage.securityChecks.dkimPass && (
                          <span className="security-badge" title="Verified SPF and DKIM signatures">
                            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                              <polyline points="20 6 9 17 4 12"></polyline>
                            </svg>
                            SPF & DKIM Valid
                          </span>
                        )}
                      </div>
                      <div style={{ fontSize: '13px', color: 'var(--neutral-body)' }}>
                        To: {currentMessage.to.map((t) => t.email).join(', ')}
                      </div>
                    </div>
                  </div>

                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: '13px', color: 'var(--neutral-body)' }}>
                    {currentMessage.receivedAt}
                  </div>
                </div>
              </div>

              {/* Sanitized Message Body */}
              <SanitizedMessageBody htmlContent={currentMessage.bodyHtml} />

              {/* Attachments Section */}
              {currentMessage.attachments.length > 0 && (
                <div style={{ marginTop: '40px', paddingTop: '20px', borderTop: '1px solid var(--neutral-border)' }}>
                  <div style={{ fontWeight: 500, fontSize: '14px', color: 'var(--deep-navy)', marginBottom: '12px' }}>
                    Attachments ({currentMessage.attachments.length} file{currentMessage.attachments.length > 1 ? 's' : ''})
                  </div>
                  <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
                    {currentMessage.attachments.map((att) => (
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
                        <button type="button" className="btn btn-secondary" style={{ padding: '4px 10px', fontSize: '12px', marginLeft: '12px' }}>
                          Download
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </>
          ) : (
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', color: 'var(--neutral-body)' }}>
              Select a message to view its contents
            </div>
          )}
        </main>
      </div>
    </div>
  );
};
