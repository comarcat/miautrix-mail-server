import React, { useState, useEffect, useCallback } from 'react';
import { SharedMailbox, AdminUserItem, MailboxDelegateRequest, ApiResponse } from '../types';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';
import { matchesDomain, ALL_DOMAINS } from '../utils/domainFilter';
import { MailboxDelegatePicker } from './MailboxDelegatePicker';

interface SharedMailboxesScreenProps {
  client?: AdminApiClient;
  domainFilter?: string;
}

const formatBytes = (bytes: number): string => {
  if (bytes === 0) return '0 B';
  const gb = bytes / (1024 ** 3);
  if (gb >= 1) return `${gb.toFixed(1)} GB`;
  const mb = bytes / (1024 ** 2);
  return `${mb.toFixed(0)} MB`;
};

export const SharedMailboxesScreen: React.FC<SharedMailboxesScreenProps> = ({
  client = defaultClient,
  domainFilter,
}) => {
  const [mailboxes, setMailboxes] = useState<SharedMailbox[]>([]);
  const [users, setUsers] = useState<AdminUserItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [search, setSearch] = useState<string>('');

  // Edit-delegates modal
  const [editingMailbox, setEditingMailbox] = useState<SharedMailbox | null>(null);
  const [editDelegates, setEditDelegates] = useState<MailboxDelegateRequest[]>([]);
  const [editBusy, setEditBusy] = useState<boolean>(false);
  const [editError, setEditError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [mailboxRes, userRes] = await Promise.all([
        client.getSharedMailboxes() as Promise<ApiResponse<SharedMailbox[]>>,
        client.getUsers() as Promise<ApiResponse<AdminUserItem[]>>,
      ]);
      setMailboxes(mailboxRes.data || []);
      setUsers(userRes.data || []);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load shared mailboxes');
    } finally {
      setLoading(false);
    }
  }, [client]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const openEdit = (mailbox: SharedMailbox) => {
    setEditingMailbox(mailbox);
    setEditDelegates(
      mailbox.delegates.map((d) => ({ user_id: d.user_id, access_level: d.access_level }))
    );
    setEditError(null);
  };

  const closeEdit = () => {
    setEditingMailbox(null);
    setEditDelegates([]);
    setEditError(null);
  };

  const handleSaveDelegates = async () => {
    if (!editingMailbox) return;
    setEditError(null);
    setEditBusy(true);
    try {
      const res = await client.replaceSharedMailboxDelegates(editingMailbox.id, editDelegates);
      const updated = (res as ApiResponse<SharedMailbox>).data;
      setMailboxes((prev) => prev.map((m) => (m.id === updated.id ? updated : m)));
      setActionMessage(`Delegates updated for ${updated.email}.`);
      closeEdit();
    } catch (err: unknown) {
      setEditError(err instanceof Error ? err.message : 'Failed to update delegates');
    } finally {
      setEditBusy(false);
    }
  };

  const editDomain = editingMailbox
    ? editingMailbox.email.slice(editingMailbox.email.lastIndexOf('@') + 1).toLowerCase()
    : '';

  const filtered = mailboxes.filter((m) => {
    const inDomain = !domainFilter || domainFilter === ALL_DOMAINS || matchesDomain(m.email, domainFilter);
    const inSearch =
      !search ||
      m.email.toLowerCase().includes(search.toLowerCase()) ||
      m.name.toLowerCase().includes(search.toLowerCase());
    return inDomain && inSearch;
  });

  // Group by domain
  const grouped = filtered.reduce<Record<string, SharedMailbox[]>>((acc, m) => {
    const domain = m.email.slice(m.email.lastIndexOf('@') + 1).toLowerCase();
    (acc[domain] ??= []).push(m);
    return acc;
  }, {});
  const sortedDomains = Object.keys(grouped).sort();

  return (
    <div className="screen-root">
      <div className="screen-header">
        <h1 className="screen-title">Shared Mailboxes</h1>
        <p className="screen-subtitle">
          Passwordless mailboxes shared between delegates on the same domain.
        </p>
      </div>

      {actionMessage && (
        <div className="alert alert-success" role="status" style={{ marginBottom: '12px' }}>
          <span>{actionMessage}</span>
          <button className="btn-close-inline" onClick={() => setActionMessage(null)}>✕</button>
        </div>
      )}

      {error && (
        <div className="alert alert-error" role="alert" style={{ marginBottom: '12px' }}>
          <span>{error}</span>
          <button className="btn-close-inline" onClick={() => setError(null)}>✕</button>
        </div>
      )}

      <div className="toolbar" style={{ marginBottom: '16px' }}>
        <input
          type="search"
          className="input-text"
          placeholder="Search by address or name…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{ width: '280px' }}
          data-testid="shared-mailboxes-search"
        />
        <span className="cell-dim" style={{ marginLeft: 'auto', alignSelf: 'center' }}>
          {filtered.length} mailbox{filtered.length !== 1 ? 'es' : ''}
          {domainFilter && domainFilter !== ALL_DOMAINS ? ` in ${domainFilter}` : ''}
        </span>
      </div>

      {loading ? (
        <p className="cell-dim">Loading…</p>
      ) : filtered.length === 0 ? (
        <div className="empty-state">
          <p className="cell-dim">
            {mailboxes.length === 0
              ? 'No shared mailboxes yet. Convert an orphan mailbox from the Users screen or create one there.'
              : 'No shared mailboxes match the current filter.'}
          </p>
        </div>
      ) : (
        sortedDomains.map((domain) => (
          <div key={domain} className="card" style={{ marginBottom: '20px' }}>
            <div className="card-header" style={{ marginBottom: '10px' }}>
              <h2 className="section-title">{domain}</h2>
              <span className="badge badge-info">
                {grouped[domain].length} mailbox{grouped[domain].length !== 1 ? 'es' : ''}
              </span>
            </div>
            <table className="data-table" data-testid={`shared-mailboxes-table-${domain}`}>
              <thead>
                <tr>
                  <th>Address</th>
                  <th>Display Name</th>
                  <th>Delegates</th>
                  <th>Quota</th>
                  <th>Status</th>
                  <th style={{ width: '100px' }}></th>
                </tr>
              </thead>
              <tbody>
                {grouped[domain].map((mailbox) => (
                  <tr key={mailbox.id} data-testid={`shared-mailbox-row-${mailbox.id}`}>
                    <td className="cell-mono">{mailbox.email}</td>
                    <td>{mailbox.name}</td>
                    <td>
                      {mailbox.delegates.length === 0 ? (
                        <span className="badge badge-warning">No delegates</span>
                      ) : (
                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                          {mailbox.delegates.map((d) => (
                            <span
                              key={d.user_id}
                              className={`badge ${d.access_level === 'write' ? 'badge-primary' : 'badge-secondary'}`}
                              title={`${d.access_level === 'write' ? 'Read / Write' : 'Read only'}`}
                            >
                              {d.name || d.email}
                              <span className="cell-dim" style={{ marginLeft: '4px', fontSize: '0.75em' }}>
                                {d.access_level === 'write' ? 'rw' : 'r'}
                              </span>
                            </span>
                          ))}
                        </div>
                      )}
                    </td>
                    <td className="cell-dim">
                      {formatBytes(mailbox.mailbox_used_bytes)} / {formatBytes(mailbox.mailbox_quota_bytes)}
                    </td>
                    <td>
                      <span className={`badge ${mailbox.is_active ? 'badge-success' : 'badge-secondary'}`}>
                        {mailbox.is_active ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td>
                      <button
                        className="btn btn-secondary btn-sm"
                        onClick={() => openEdit(mailbox)}
                        data-testid={`edit-delegates-${mailbox.id}`}
                      >
                        Edit Delegates
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))
      )}

      {/* Edit Delegates Modal */}
      {editingMailbox && (
        <div className="modal-backdrop" data-testid="edit-delegates-modal">
          <div className="modal-card">
            <div className="modal-header">
              <h2 className="modal-title">Edit Delegates — {editingMailbox.email}</h2>
              <button className="btn-close" onClick={closeEdit} data-testid="edit-delegates-close">✕</button>
            </div>
            <div className="modal-body">
              {editError && (
                <div className="alert alert-error" role="alert">
                  <span>{editError}</span>
                </div>
              )}

              <MailboxDelegatePicker
                users={users}
                domain={editDomain}
                excludeEmail={editingMailbox.email}
                selected={editDelegates}
                onChange={setEditDelegates}
                label="Delegates (same domain)"
                testIdPrefix="edit-delegate"
              />

              <div className="modal-footer" style={{ padding: 0, marginTop: '16px' }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={closeEdit}
                  disabled={editBusy}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={handleSaveDelegates}
                  disabled={editBusy}
                  data-testid="save-delegates-btn"
                >
                  {editBusy ? 'Saving…' : 'Save Delegates'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
