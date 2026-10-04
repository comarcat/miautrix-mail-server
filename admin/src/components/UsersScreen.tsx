import React, { useState, useEffect, useCallback } from 'react';
import { AdminUserItem, CreateUserRequest, UpdateUserRequest, ResetPasswordRequest, ApiResponse, DomainItem, MailboxDelegateRequest, OrphanMailboxItem } from '../types';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';
import { matchesDomain } from '../utils/domainFilter';
import { downloadBlob, archiveFileName } from '../utils/download';
import { MailboxDelegatePicker } from './MailboxDelegatePicker';
import { AssignMailboxModal } from './AssignMailboxModal';

interface UsersScreenProps {
  client?: AdminApiClient;
  domainFilter?: string;
}

export const UsersScreen: React.FC<UsersScreenProps> = ({ client = defaultClient, domainFilter }) => {
  const [users, setUsers] = useState<AdminUserItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [search, setSearch] = useState<string>('');
  const [roleFilter, setRoleFilter] = useState<string>('all');
  const [mailboxScope, setMailboxScope] = useState<string>('all');
  const [orphanMailboxes, setOrphanMailboxes] = useState<OrphanMailboxItem[]>([]);
  const [orphansLoading, setOrphansLoading] = useState<boolean>(false);
  const [managingMailbox, setManagingMailbox] = useState<OrphanMailboxItem | null>(null);
  const [verifiedDomains, setVerifiedDomains] = useState<string[]>([]);
  const [domainsLoading, setDomainsLoading] = useState<boolean>(true);

  // Modal states
  const [isCreateOpen, setIsCreateOpen] = useState<boolean>(false);
  const [editingUser, setEditingUser] = useState<AdminUserItem | null>(null);
  const [resettingUser, setResettingUser] = useState<AdminUserItem | null>(null);
  const [generatedPassword, setGeneratedPassword] = useState<string | null>(null);

  // Form states
  const [formUsername, setFormUsername] = useState<string>('');
  const [formDomain, setFormDomain] = useState<string>('');
  const [currentEditDomain, setCurrentEditDomain] = useState<string>('');
  const [formName, setFormName] = useState<string>('');
  const [formPassword, setFormPassword] = useState<string>('');
  const [formRole, setFormRole] = useState<string>('member');
  const [formQuotaMb, setFormQuotaMb] = useState<number>(5120); // 5GB default
  const [formIsActive, setFormIsActive] = useState<boolean>(true);
  const [formMustChangePassword, setFormMustChangePassword] = useState<boolean>(false);
  const [formIsService, setFormIsService] = useState<boolean>(false);
  const [formMailboxKind, setFormMailboxKind] = useState<string>('user');
  const [formDelegates, setFormDelegates] = useState<MailboxDelegateRequest[]>([]);
  const [resetPassword, setResetPassword] = useState<string>('');
  const [resetMustChangePassword, setResetMustChangePassword] = useState<boolean>(true);
  const [generatePassword, setGeneratePassword] = useState<boolean>(true);

  const fetchUsers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res: ApiResponse<AdminUserItem[]> = await client.getUsers();
      setUsers(res.data || []);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch users');
      setUsers([]);
    } finally {
      setLoading(false);
    }
  }, [client]);

  const fetchOrphans = useCallback(async () => {
    setOrphansLoading(true);
    setError(null);
    try {
      const res: ApiResponse<OrphanMailboxItem[]> = await client.getOrphanMailboxes();
      setOrphanMailboxes(res.data || []);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch mailboxes without users');
      setOrphanMailboxes([]);
    } finally {
      setOrphansLoading(false);
    }
  }, [client]);

  const handleExportMailbox = async (mailbox: OrphanMailboxItem) => {
    try {
      const blob = await client.exportMailbox(mailbox.id);
      downloadBlob(blob, archiveFileName(mailbox.email));
      setActionMessage(`Downloading archive of ${mailbox.email}...`);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to export mailbox');
    }
  };

  const fetchDomains = useCallback(async () => {
    setDomainsLoading(true);
    try {
      const res: ApiResponse<DomainItem[]> = await client.getDomains();
      const verified = (res.data || [])
        .filter((domain) => domain.is_verified)
        .map((domain) => domain.name.toLowerCase())
        .sort((a, b) => a.localeCompare(b));
      setVerifiedDomains(verified);
      setFormDomain((current) => current || verified[0] || '');
    } catch {
      setVerifiedDomains([]);
      setFormDomain('');
    } finally {
      setDomainsLoading(false);
    }
  }, [client]);

  useEffect(() => {
    fetchUsers();
    fetchDomains();
  }, [fetchUsers, fetchDomains]);

  useEffect(() => {
    if (mailboxScope === 'orphans') {
      fetchOrphans();
    }
  }, [mailboxScope, fetchOrphans]);

  const splitEmail = (email: string) => {
    const at = email.lastIndexOf('@');
    if (at <= 0 || at === email.length - 1) {
      return { username: email, domain: '' };
    }

    return {
      username: email.slice(0, at),
      domain: email.slice(at + 1).toLowerCase(),
    };
  };

  const preferredCreateDomain = () => {
    if (domainFilter && verifiedDomains.includes(domainFilter.toLowerCase())) {
      return domainFilter.toLowerCase();
    }

    return verifiedDomains[0] || '';
  };

  const buildFormEmail = () => {
    const username = formUsername.trim().toLowerCase();
    const domain = formDomain.trim().toLowerCase();
    if (!username || username.includes('@')) {
      throw new Error('Enter only the username, without @ or domain.');
    }
    if (!domain) {
      throw new Error('Select a verified domain.');
    }

    return `${username}@${domain}`;
  };

  const handleOpenCreate = () => {
    setFormUsername('');
    setFormDomain(preferredCreateDomain());
    setCurrentEditDomain('');
    setFormName('');
    setFormPassword('');
    setFormRole('member');
    setFormQuotaMb(5120);
    setFormIsActive(true);
    setFormMustChangePassword(false);
    setFormIsService(false);
    setFormMailboxKind('user');
    setFormDelegates([]);
    setIsCreateOpen(true);
  };

  const handleOpenEdit = (user: AdminUserItem) => {
    const emailParts = splitEmail(user.email);
    setEditingUser(user);
    setFormUsername(emailParts.username);
    setFormDomain(emailParts.domain || preferredCreateDomain());
    setCurrentEditDomain(emailParts.domain);
    setFormName(user.name);
    setFormRole(user.role);
    setFormQuotaMb(Math.round(user.mailbox_quota_bytes / (1024 * 1024)));
    setFormIsActive(user.is_active);
    setFormMustChangePassword(user.must_change_password);
    setFormIsService(user.is_service);
  };

  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formUsername.trim() || !formName.trim() || (formMailboxKind === 'user' && !formPassword.trim())) {
      setError(formMailboxKind === 'user'
        ? 'Username, Name, and Password are required.'
        : 'Username and Name are required.');
      return;
    }
    if (verifiedDomains.length === 0) {
      setError('Add and verify a domain before creating users.');
      return;
    }

    try {
      const email = buildFormEmail();
      if (formMailboxKind === 'shared') {
        await client.createSharedMailbox({
          email,
          name: formName.trim(),
          quota_bytes: formQuotaMb * 1024 * 1024,
          delegates: formDelegates,
        });
        setIsCreateOpen(false);
        setActionMessage(`Shared mailbox ${email} created successfully.`);
        fetchUsers();
        return;
      }

      const payload: CreateUserRequest = {
        email,
        name: formName.trim(),
        password: formPassword,
        role: formRole,
        quota_bytes: formQuotaMb * 1024 * 1024,
        must_change_password: formMustChangePassword,
        is_service: formIsService,
        mailbox_kind: formMailboxKind,
      };
      await client.createUser(payload);
      setIsCreateOpen(false);
      setActionMessage(`User ${payload.email} created successfully.`);
      fetchUsers();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create user');
    }
  };

  const handleUpdateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingUser) return;

    try {
      const payload: UpdateUserRequest = {
        email: buildFormEmail(),
        name: formName.trim(),
        role: formRole,
        is_active: formIsActive,
        quota_bytes: formQuotaMb * 1024 * 1024,
        must_change_password: formMustChangePassword,
        is_service: formIsService,
      };
      await client.updateUser(editingUser.id, payload);
      setEditingUser(null);
      setActionMessage(`User ${editingUser.email} updated successfully.`);
      fetchUsers();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update user');
    }
  };

  const handleOpenResetPassword = (user: AdminUserItem) => {
    setResettingUser(user);
    setResetPassword('');
    setResetMustChangePassword(true);
    setGeneratePassword(true);
    setGeneratedPassword(null);
  };

  const handleResetPasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!resettingUser) return;
    if (!generatePassword && !resetPassword.trim()) {
      setError('Enter a password or enable password generation.');
      return;
    }

    try {
      const payload: ResetPasswordRequest = {
        password: generatePassword ? undefined : resetPassword,
        must_change_password: resetMustChangePassword,
        generate_password: generatePassword,
      };
      const res = await client.resetPassword(resettingUser.id, payload);
      if (res.data.generated_password) {
        setGeneratedPassword(res.data.generated_password);
      } else {
        setResettingUser(null);
        setActionMessage(`Password for ${resettingUser.email} reset successfully.`);
      }
      fetchUsers();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to reset password');
    }
  };

  const handleDelete = async (user: AdminUserItem) => {
    if (!window.confirm(
      `Delete the account ${user.email}?\n\n` +
      'This removes the user, its memberships and its credentials. The mailbox and its ' +
      'messages are kept: it appears under "Without User" so you can reassign or delete it.')) {
      return;
    }

    try {
      await client.deleteUser(user.id);
      setActionMessage(`User ${user.email} deleted. Its mailbox is now listed under "Without User".`);
      fetchUsers();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to delete user');
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 MB';
    const mb = bytes / (1024 * 1024);
    if (mb >= 1024) return `${(mb / 1024).toFixed(1)} GB`;
    return `${mb.toFixed(0)} MB`;
  };

  const roleOptions = ['owner', 'admin', 'operator', 'member'];
  const domainSelectOptions = editingUser && currentEditDomain && !verifiedDomains.includes(currentEditDomain)
    ? [currentEditDomain, ...verifiedDomains]
    : verifiedDomains;
  const emailPreview = formUsername.trim() && formDomain
    ? `${formUsername.trim().toLowerCase()}@${formDomain}`
    : '';
  const filteredUsers = users.filter((u) => {
    const matchesSearch =
      u.email.toLowerCase().includes(search.toLowerCase()) ||
      u.name.toLowerCase().includes(search.toLowerCase());
    const matchesRole = roleFilter === 'all' || u.role.toLowerCase() === roleFilter.toLowerCase();
    return matchesSearch && matchesRole && matchesDomain(domainFilter, u.email);
  });

  const filteredOrphans = orphanMailboxes.filter((m) =>
    (m.email.toLowerCase().includes(search.toLowerCase()) ||
      m.name.toLowerCase().includes(search.toLowerCase())) &&
    matchesDomain(domainFilter, m.email));

  return (
    <div className="screen-container users-screen" data-testid="users-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">User Accounts & Mailboxes</h1>
          <p className="screen-desc">
            Manage tenant identities, mailbox storage quotas, membership roles, and security policies.
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-secondary" onClick={fetchUsers} title="Refresh users">
            Refresh
          </button>
          <button className="btn btn-primary" onClick={handleOpenCreate} data-testid="add-user-btn">
            + Add User
          </button>
        </div>
      </div>

      {actionMessage && (
        <div className="alert alert-info" role="status">
          <span>{actionMessage}</span>
          <button className="alert-close" onClick={() => setActionMessage(null)}>✕</button>
        </div>
      )}

      {error && (
        <div className="alert alert-error" role="alert">
          <span>{error}</span>
          <button className="alert-close" onClick={() => setError(null)}>✕</button>
        </div>
      )}

      <div className="card filter-bar">
        <div className="chip-group" role="tablist" aria-label="Mailbox scope filter">
          <button
            role="tab"
            aria-selected={mailboxScope === 'all'}
            className={`chip ${mailboxScope === 'all' ? 'chip-active' : ''}`}
            onClick={() => setMailboxScope('all')}
            data-testid="filter-mailbox-all"
          >
            All Mailboxes
          </button>
          <button
            role="tab"
            aria-selected={mailboxScope === 'orphans'}
            className={`chip ${mailboxScope === 'orphans' ? 'chip-active' : ''}`}
            onClick={() => setMailboxScope('orphans')}
            data-testid="filter-mailbox-orphans"
          >
            Without User
          </button>
        </div>

        {mailboxScope === 'all' && (
          <div className="chip-group" role="tablist" aria-label="User role filter">
            {['all', ...roleOptions].map((role) => (
              <button
                key={role}
                role="tab"
                aria-selected={roleFilter === role}
                className={`chip ${roleFilter === role ? 'chip-active' : ''}`}
                onClick={() => setRoleFilter(role)}
                data-testid={`filter-role-${role}`}
              >
                {role === 'all' ? 'All Roles' : role.charAt(0).toUpperCase() + role.slice(1)}
              </button>
            ))}
          </div>
        )}

        <div className="search-form">
          <div className="input-search-wrapper">
            <svg className="search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              type="search"
              className="input-search"
              placeholder="Search by name or email..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search users"
            />
          </div>
        </div>
      </div>

      <div className="card table-card">
        {mailboxScope === 'orphans' ? (
          orphansLoading ? (
            <div className="loading-state" data-testid="orphans-loading">
              <div className="spinner" />
              <p>Loading mailboxes without users...</p>
            </div>
          ) : filteredOrphans.length === 0 ? (
            <div className="empty-state" data-testid="orphans-empty">
              <p>No orphaned mailboxes found. Every mailbox has an active user or delegate.</p>
            </div>
          ) : (
            <div className="table-responsive">
              <table className="data-table" data-testid="orphans-table">
                <thead>
                  <tr>
                    <th>Kind</th>
                    <th>Name / Address</th>
                    <th>Storage</th>
                    <th>Messages</th>
                    <th>Attachments</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredOrphans.map((m) => (
                    <tr key={m.id}>
                      <td>
                        <span className={`badge ${m.kind === 'shared' ? 'badge-info' : 'badge-neutral'}`}>
                          {m.kind}
                        </span>
                      </td>
                      <td>
                        <div className="cell-emphasis">{m.name}</div>
                        <div className="cell-mono cell-dim" style={{ fontSize: '11px' }}>{m.email}</div>
                      </td>
                      <td style={{ minWidth: '150px' }}>
                        <div style={{ fontSize: '11px' }}>
                          <span>{formatBytes(m.mailbox_used_bytes)} used</span>
                          <span className="cell-dim"> of {formatBytes(m.mailbox_quota_bytes)}</span>
                        </div>
                      </td>
                      <td className="cell-dim">{m.message_count}</td>
                      <td className="cell-dim">{m.attachment_count}</td>
                      <td>
                        <div className="action-buttons">
                          <button
                            className="btn-link"
                            onClick={() => setManagingMailbox(m)}
                            data-testid={`manage-mailbox-${m.id}`}
                          >
                            Manage
                          </button>
                          <button
                            className="btn-link"
                            onClick={() => handleExportMailbox(m)}
                            data-testid={`export-mailbox-${m.id}`}
                          >
                            Download ZIP
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : loading ? (
          <div className="loading-state" data-testid="users-loading">
            <div className="spinner" />
            <p>Loading user accounts...</p>
          </div>
        ) : filteredUsers.length === 0 ? (
          <div className="empty-state" data-testid="users-empty">
            <p>No user accounts found matching criteria.</p>
          </div>
        ) : (
          <div className="table-responsive">
            <table className="data-table" data-testid="users-table">
              <thead>
                <tr>
                  <th>Status</th>
                  <th>Name / Email</th>
                  <th>Role</th>
                  <th>Mailbox Quota</th>
                  <th>Created</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredUsers.map((u) => {
                  const quotaPercent = u.mailbox_quota_bytes > 0
                    ? Math.min(100, Math.round((u.mailbox_used_bytes / u.mailbox_quota_bytes) * 100))
                    : 0;

                  return (
                    <tr key={u.id}>
                      <td>
                        <span className={`badge ${u.is_active ? 'badge-success' : 'badge-neutral'}`}>
                          {u.is_active ? 'Active' : 'Disabled'}
                        </span>
                      </td>
                      <td>
                        <div className="cell-emphasis">{u.name}</div>
                        <div className="cell-mono cell-dim" style={{ fontSize: '11px' }}>{u.email}</div>
                      </td>
                      <td>
                        <span className={`badge ${u.role === 'admin' ? 'badge-info' : 'badge-neutral'}`}>
                          {u.role}
                        </span>
                      </td>
                      <td style={{ minWidth: '180px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '11px', marginBottom: '4px' }}>
                          <span>{formatBytes(u.mailbox_used_bytes)} used</span>
                          <span className="cell-dim">{formatBytes(u.mailbox_quota_bytes)}</span>
                        </div>
                        <div style={{ width: '100%', height: '6px', background: 'var(--page)', borderRadius: '3px', overflow: 'hidden' }}>
                          <div
                            style={{
                              width: `${quotaPercent}%`,
                              height: '100%',
                              background: quotaPercent > 90 ? 'var(--error)' : quotaPercent > 75 ? 'var(--warning)' : 'var(--info-btn)',
                              borderRadius: '3px',
                            }}
                          />
                        </div>
                      </td>
                      <td className="cell-dim">{new Date(u.created_at).toLocaleDateString()}</td>
                      <td>
                        <div className="action-buttons">
                          <button
                            className="btn-link"
                            onClick={() => handleOpenEdit(u)}
                            data-testid={`edit-user-${u.id}`}
                          >
                            Edit
                          </button>
                          <button
                            className="btn-link"
                            onClick={() => handleOpenResetPassword(u)}
                            data-testid={`reset-password-user-${u.id}`}
                          >
                            Reset Password
                          </button>
                          <button
                            className="btn-link text-error"
                            onClick={() => handleDelete(u)}
                            data-testid={`delete-user-${u.id}`}
                          >
                            Delete
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Create User Modal */}
      {isCreateOpen && (
        <div className="modal-backdrop" data-testid="create-user-modal">
          <div className="modal-card">
            <div className="modal-header">
              <h2 className="modal-title">Provision New User Account</h2>
              <button className="btn-close" onClick={() => setIsCreateOpen(false)}>✕</button>
            </div>
            <form onSubmit={handleCreateSubmit}>
              <div className="modal-body">
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Username</label>
                    <input
                      type="text"
                      className="input-text w-full"
                      placeholder="cristobal"
                      value={formUsername}
                      onChange={(e) => setFormUsername(e.target.value)}
                      required
                    />
                  </div>
                  <div className="form-group">
                    <label className="form-label">Domain</label>
                    <select
                      className="input-select w-full"
                      value={formDomain}
                      onChange={(e) => setFormDomain(e.target.value)}
                      disabled={domainsLoading || verifiedDomains.length === 0}
                      required
                    >
                      {verifiedDomains.length === 0 ? (
                        <option value="">No verified domains</option>
                      ) : (
                        verifiedDomains.map((domain) => (
                          <option key={domain} value={domain}>{domain}</option>
                        ))
                      )}
                    </select>
                  </div>
                </div>
                {verifiedDomains.length === 0 ? (
                  <div className="alert alert-error" role="alert">
                    Add and verify a domain before creating users.
                  </div>
                ) : emailPreview && (
                  <p className="cell-dim" style={{ marginTop: '-8px' }}>
                    Email: <span className="cell-mono">{emailPreview}</span>
                  </p>
                )}
                <div className="form-group">
                  <label className="form-label">Full Name</label>
                  <input
                    type="text"
                    className="input-text w-full"
                    placeholder="Jane Doe"
                    value={formName}
                    onChange={(e) => setFormName(e.target.value)}
                    required
                  />
                </div>
                {formMailboxKind === 'user' && <>
                  <div className="form-group">
                    <label className="form-label">Initial Password</label>
                    <input
                      type="password"
                      className="input-text w-full"
                      placeholder="••••••••••••"
                      value={formPassword}
                      onChange={(e) => setFormPassword(e.target.value)}
                      required
                    />
                  </div>
                  <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px' }}>
                    <input
                      type="checkbox"
                      id="create-must-change-password-checkbox"
                      checked={formMustChangePassword}
                      onChange={(e) => setFormMustChangePassword(e.target.checked)}
                    />
                    <label htmlFor="create-must-change-password-checkbox" className="detail-value" style={{ cursor: 'pointer' }}>
                      Change password next time
                    </label>
                  </div>
                </>}
                {formMailboxKind === 'shared' && (
                  <MailboxDelegatePicker
                    users={users}
                    domain={formDomain}
                    excludeEmail={emailPreview}
                    selected={formDelegates}
                    onChange={setFormDelegates}
                    testIdPrefix="create-delegate"
                  />
                )}
                {formMailboxKind === 'user' && <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Membership Role</label>
                    <select
                      className="input-select w-full"
                      value={formRole}
                      onChange={(e) => setFormRole(e.target.value)}
                    >
                      {roleOptions.map((role) => (
                        <option key={role} value={role}>{role.charAt(0).toUpperCase() + role.slice(1)}</option>
                      ))}
                    </select>
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
                </div>}
                <div className="form-row">
                  {formMailboxKind === 'user' && <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px' }}>
                    <input
                      type="checkbox"
                      id="create-is-service-checkbox"
                      checked={formIsService}
                      onChange={(e) => setFormIsService(e.target.checked)}
                    />
                    <label htmlFor="create-is-service-checkbox" className="detail-value" style={{ cursor: 'pointer' }}>
                      Service account
                    </label>
                  </div>}
                  <div className="form-group">
                    <label className="form-label">Mailbox Kind</label>
                    <select
                      className="input-select w-full"
                      value={formMailboxKind}
                      onChange={(e) => setFormMailboxKind(e.target.value)}
                    >
                      <option value="user">User mailbox</option>
                      <option value="shared">Shared mailbox</option>
                    </select>
                  </div>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setIsCreateOpen(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary" disabled={domainsLoading || verifiedDomains.length === 0}>
                  {formMailboxKind === 'shared' ? 'Create Shared Mailbox' : 'Create User'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Edit User Modal */}
      {editingUser && (
        <div className="modal-backdrop" data-testid="edit-user-modal">
          <div className="modal-card">
            <div className="modal-header">
              <h2 className="modal-title">Edit User — {editingUser.email}</h2>
              <button className="btn-close" onClick={() => setEditingUser(null)}>✕</button>
            </div>
            <form onSubmit={handleUpdateSubmit}>
              <div className="modal-body">
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Username</label>
                    <input
                      type="text"
                      className="input-text w-full"
                      value={formUsername}
                      onChange={(e) => setFormUsername(e.target.value)}
                      required
                    />
                  </div>
                  <div className="form-group">
                    <label className="form-label">Domain</label>
                    <select
                      className="input-select w-full"
                      value={formDomain}
                      onChange={(e) => setFormDomain(e.target.value)}
                      disabled={domainsLoading || domainSelectOptions.length === 0}
                      required
                    >
                      {domainSelectOptions.length === 0 ? (
                        <option value="">No verified domains</option>
                      ) : (
                        domainSelectOptions.map((domain) => (
                          <option key={domain} value={domain}>
                            {domain}{domain === currentEditDomain && !verifiedDomains.includes(domain) ? ' (current, not verified)' : ''}
                          </option>
                        ))
                      )}
                    </select>
                  </div>
                </div>
                {currentEditDomain && !verifiedDomains.includes(currentEditDomain) && (
                  <div className="alert alert-info" role="status">
                    This user currently uses a domain that is not verified. Select a verified domain before changing it.
                  </div>
                )}
                {emailPreview && (
                  <p className="cell-dim" style={{ marginTop: '-8px' }}>
                    Email: <span className="cell-mono">{emailPreview}</span>
                  </p>
                )}
                <div className="form-group">
                  <label className="form-label">Full Name</label>
                  <input
                    type="text"
                    className="input-text w-full"
                    value={formName}
                    onChange={(e) => setFormName(e.target.value)}
                    required
                  />
                </div>
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Membership Role</label>
                    <select
                      className="input-select w-full"
                      value={formRole}
                      onChange={(e) => setFormRole(e.target.value)}
                    >
                      {roleOptions.map((role) => (
                        <option key={role} value={role}>{role.charAt(0).toUpperCase() + role.slice(1)}</option>
                      ))}
                    </select>
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
                <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
                  <input
                    type="checkbox"
                    id="user-active-checkbox"
                    checked={formIsActive}
                    onChange={(e) => setFormIsActive(e.target.checked)}
                  />
                  <label htmlFor="user-active-checkbox" className="detail-value" style={{ cursor: 'pointer' }}>
                    Account Active (permit SMTP/IMAP/Webmail access)
                  </label>
                </div>
                <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
                  <input
                    type="checkbox"
                    id="edit-must-change-password-checkbox"
                    checked={formMustChangePassword}
                    onChange={(e) => setFormMustChangePassword(e.target.checked)}
                  />
                  <label htmlFor="edit-must-change-password-checkbox" className="detail-value" style={{ cursor: 'pointer' }}>
                    Change password next time
                  </label>
                </div>
                <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
                  <input
                    type="checkbox"
                    id="edit-is-service-checkbox"
                    checked={formIsService}
                    onChange={(e) => setFormIsService(e.target.checked)}
                  />
                  <label htmlFor="edit-is-service-checkbox" className="detail-value" style={{ cursor: 'pointer' }}>
                    Service account
                  </label>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setEditingUser(null)}>
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary">
                  Save Changes
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Reset Password Modal */}
      {resettingUser && (
        <div className="modal-backdrop" data-testid="reset-password-modal">
          <div className="modal-card">
            <div className="modal-header">
              <h2 className="modal-title">Reset Password — {resettingUser.email}</h2>
              <button
                className="btn-close"
                onClick={() => {
                  setResettingUser(null);
                  setGeneratedPassword(null);
                  setResetPassword('');
                  setGeneratePassword(true);
                }}
              >✕</button>
            </div>
            <form onSubmit={handleResetPasswordSubmit}>
              <div className="modal-body">
                <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
                  <input
                    type="checkbox"
                    id="reset-generate-password-checkbox"
                    checked={generatePassword}
                    onChange={(e) => setGeneratePassword(e.target.checked)}
                  />
                  <label htmlFor="reset-generate-password-checkbox" className="detail-value" style={{ cursor: 'pointer' }}>
                    Generate strong password
                  </label>
                </div>

                {!generatePassword && (
                  <div className="form-group">
                    <label className="form-label">New Password</label>
                    <input
                      type="password"
                      className="input-text w-full"
                      placeholder="Enter a new password"
                      value={resetPassword}
                      onChange={(e) => setResetPassword(e.target.value)}
                      required={generatePassword ? false : true}
                    />
                  </div>
                )}

                <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
                  <input
                    type="checkbox"
                    id="reset-must-change-password-checkbox"
                    checked={resetMustChangePassword}
                    onChange={(e) => setResetMustChangePassword(e.target.checked)}
                  />
                  <label htmlFor="reset-must-change-password-checkbox" className="detail-value" style={{ cursor: 'pointer' }}>
                    Change password next time
                  </label>
                </div>

                {generatedPassword && (
                  <div className="form-group" style={{ marginTop: '12px' }}>
                    <label className="form-label">Generated Password (one-time)</label>
                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                      <input
                        type="text"
                        className="input-text w-full"
                        value={generatedPassword}
                        readOnly
                      />
                      <button
                        type="button"
                        className="btn btn-secondary"
                        onClick={async () => {
                          try {
                            await navigator.clipboard.writeText(generatedPassword);
                            setActionMessage('Password copied to clipboard.');
                          } catch {
                            setError('Failed to copy password.');
                          }
                        }}
                      >
                        Copy
                      </button>
                    </div>
                    <p className="cell-dim" style={{ marginTop: '8px' }}>
                      For security, keep this password only for the intended user.
                    </p>
                  </div>
                )}
              </div>
              <div className="modal-footer">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => {
                    setResettingUser(null);
                    setGeneratedPassword(null);
                    setResetPassword('');
                    setGeneratePassword(true);
                  }}
                >
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary">
                  Reset Password
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Orphan mailbox — assign / delete / export */}
      {managingMailbox && (
        <AssignMailboxModal
          mailbox={managingMailbox}
          users={users}
          verifiedDomains={verifiedDomains}
          client={client}
          onClose={() => setManagingMailbox(null)}
          onAssigned={(message) => {
            setActionMessage(message);
            setManagingMailbox(null);
            fetchUsers();
            fetchOrphans();
          }}
          onDeleted={(message) => {
            setActionMessage(message);
            setManagingMailbox(null);
            fetchUsers();
            fetchOrphans();
          }}
        />
      )}

    </div>
  );
};
