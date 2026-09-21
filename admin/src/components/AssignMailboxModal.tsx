import React, { useState } from 'react';
import { AdminUserItem, AssignMailboxRequest, DeleteMailboxRequest, MailboxDelegateRequest, OrphanMailboxItem } from '../types';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';
import { MailboxDelegatePicker } from './MailboxDelegatePicker';
import { downloadBlob, archiveFileName } from '../utils/download';

interface AssignMailboxModalProps {
  mailbox: OrphanMailboxItem;
  users: AdminUserItem[];
  verifiedDomains: string[];
  client?: AdminApiClient;
  onClose: () => void;
  onAssigned: (message: string) => void;
  onDeleted: (message: string) => void;
}

const splitEmail = (email: string) => {
  const at = email.lastIndexOf('@');
  if (at <= 0 || at === email.length - 1) {
    return { username: email, domain: '' };
  }

  return { username: email.slice(0, at), domain: email.slice(at + 1).toLowerCase() };
};

export const AssignMailboxModal: React.FC<AssignMailboxModalProps> = ({
  mailbox,
  users,
  verifiedDomains,
  client = defaultClient,
  onClose,
  onAssigned,
  onDeleted,
}) => {
  const initial = splitEmail(mailbox.email);
  const [formUsername, setFormUsername] = useState<string>(initial.username);
  const [formDomain, setFormDomain] = useState<string>(
    verifiedDomains.includes(initial.domain) ? initial.domain : (verifiedDomains[0] || initial.domain));
  const [formName, setFormName] = useState<string>(mailbox.name);
  const [formQuotaMb, setFormQuotaMb] = useState<number>(Math.round(mailbox.mailbox_quota_bytes / (1024 * 1024)) || 5120);
  const [delegates, setDelegates] = useState<MailboxDelegateRequest[]>([]);
  const [busy, setBusy] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Two-step delete: step 1 arms it, step 2 requires the address typed verbatim.
  const [deleteArmed, setDeleteArmed] = useState<boolean>(false);
  const [confirmText, setConfirmText] = useState<string>('');

  const emailPreview = formUsername.trim() && formDomain
    ? `${formUsername.trim().toLowerCase()}@${formDomain.trim().toLowerCase()}`
    : '';

  const buildAddress = () => {
    const username = formUsername.trim().toLowerCase();
    if (!username || username.includes('@')) {
      throw new Error('Enter only the username, without @ or domain.');
    }
    if (!formDomain.trim()) {
      throw new Error('Select a verified domain.');
    }

    return `${username}@${formDomain.trim().toLowerCase()}`;
  };

  const handleAssign = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (delegates.length === 0) {
      setError('Assign at least one delegate, otherwise the mailbox stays orphaned.');
      return;
    }

    try {
      const address = buildAddress();
      const payload: AssignMailboxRequest = {
        address,
        name: formName.trim() || mailbox.name,
        delegates,
        quota_bytes: formQuotaMb * 1024 * 1024,
      };
      setBusy(true);
      await client.assignMailbox(mailbox.id, payload);
      onAssigned(`Mailbox ${mailbox.email} assigned as shared mailbox ${address}.`);
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to assign mailbox');
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async () => {
    setError(null);
    if (confirmText.trim().toLowerCase() !== mailbox.email.toLowerCase()) {
      setError('Type the mailbox address exactly to confirm deletion.');
      return;
    }

    try {
      const payload: DeleteMailboxRequest = { confirm_address: confirmText.trim() };
      setBusy(true);
      const res = await client.deleteMailbox(mailbox.id, payload);
      const counts = res.data;
      onDeleted(
        `Mailbox ${mailbox.email} deleted (${counts.messages_deleted} messages, ` +
        `${counts.attachments_deleted} attachments, ${counts.blobs_deleted} stored blobs).`);
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to delete mailbox');
    } finally {
      setBusy(false);
    }
  };

  const handleDownload = async () => {
    setError(null);
    try {
      setBusy(true);
      const blob = await client.exportMailbox(mailbox.id);
      downloadBlob(blob, archiveFileName(mailbox.email));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to export mailbox');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="modal-backdrop" data-testid="assign-mailbox-modal">
      <div className="modal-card">
        <div className="modal-header">
          <h2 className="modal-title">Manage Mailbox — {mailbox.email}</h2>
          <button className="btn-close" onClick={onClose} data-testid="assign-mailbox-close">✕</button>
        </div>

        <div className="modal-body">
          {error && (
            <div className="alert alert-error" role="alert">
              <span>{error}</span>
            </div>
          )}

          <div className="alert alert-info" role="status">
            <span>
              No user can access this mailbox today. Assigning it converts it to a shared mailbox
              and gives the delegates below access.
            </span>
          </div>

          <form onSubmit={handleAssign} data-testid="assign-mailbox-form">
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Username</label>
                <input
                  type="text"
                  className="input-text w-full"
                  value={formUsername}
                  onChange={(e) => setFormUsername(e.target.value)}
                  data-testid="assign-mailbox-username"
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Domain</label>
                <select
                  className="input-select w-full"
                  value={formDomain}
                  onChange={(e) => setFormDomain(e.target.value)}
                  data-testid="assign-mailbox-domain"
                  required
                >
                  {verifiedDomains.length === 0 ? (
                    <option value="">No verified domains</option>
                  ) : (
                    (verifiedDomains.includes(formDomain) || !formDomain
                      ? verifiedDomains
                      : [formDomain, ...verifiedDomains]).map((domain) => (
                      <option key={domain} value={domain}>{domain}</option>
                    ))
                  )}
                </select>
              </div>
            </div>
            {emailPreview && (
              <p className="cell-dim" style={{ marginTop: '-8px' }}>
                Address: <span className="cell-mono">{emailPreview}</span>
              </p>
            )}

            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Display Name</label>
                <input
                  type="text"
                  className="input-text w-full"
                  value={formName}
                  onChange={(e) => setFormName(e.target.value)}
                  data-testid="assign-mailbox-name"
                />
              </div>
              <div className="form-group">
                <label className="form-label">Mailbox Quota (MB)</label>
                <input
                  type="number"
                  className="input-text w-full"
                  value={formQuotaMb}
                  onChange={(e) => setFormQuotaMb(Number(e.target.value))}
                  min={100}
                  step={512}
                />
              </div>
            </div>

            <MailboxDelegatePicker
              users={users}
              domain={formDomain}
              excludeEmail={emailPreview}
              selected={delegates}
              onChange={setDelegates}
              label="Delegates (required, same domain)"
              testIdPrefix="assign-delegate"
            />

            <div className="modal-footer" style={{ padding: 0, marginTop: '12px' }}>
              <button type="button" className="btn btn-secondary" onClick={handleDownload} disabled={busy} data-testid="export-mailbox-btn">
                Download ZIP
              </button>
              <button type="submit" className="btn btn-primary" disabled={busy || delegates.length === 0} data-testid="assign-mailbox-submit">
                Assign Mailbox
              </button>
            </div>
          </form>

          <div className="card" style={{ marginTop: '16px', borderColor: 'var(--error)' }}>
            <h3 className="form-label" style={{ color: 'var(--error)' }}>Danger zone</h3>
            <p className="cell-dim">
              Deleting the mailbox removes its messages, attachments and folders from the database
              and deletes stored blobs no other message uses. This cannot be undone.
            </p>

            {!deleteArmed ? (
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => { setDeleteArmed(true); setConfirmText(''); setError(null); }}
                data-testid="arm-delete-mailbox"
              >
                Delete mailbox…
              </button>
            ) : (
              <div data-testid="delete-mailbox-confirm">
                <div className="alert alert-error" role="alert">
                  <span>
                    Deleting <span className="cell-mono">{mailbox.email}</span> permanently removes
                    {mailbox.message_count} message(s) and {mailbox.attachment_count} attachment(s).
                    There is no restore.
                  </span>
                </div>
                <div className="form-group">
                  <label className="form-label">
                    Type <span className="cell-mono">{mailbox.email}</span> to confirm
                  </label>
                  <input
                    type="text"
                    className="input-text w-full"
                    value={confirmText}
                    onChange={(e) => setConfirmText(e.target.value)}
                    placeholder={mailbox.email}
                    data-testid="delete-mailbox-confirm-input"
                  />
                </div>
                <div style={{ display: 'flex', gap: '8px' }}>
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => { setDeleteArmed(false); setConfirmText(''); setError(null); }}
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    className="btn btn-danger"
                    onClick={handleDelete}
                    disabled={busy || confirmText.trim().toLowerCase() !== mailbox.email.toLowerCase()}
                    data-testid="confirm-delete-mailbox"
                  >
                    Permanently delete mailbox
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
