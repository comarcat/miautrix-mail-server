import React, { useState, useEffect, useCallback } from 'react';
import { AdminUserItem, CreateUserRequest, UpdateUserRequest, ApiResponse } from '../types';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';
import { matchesDomain } from '../utils/domainFilter';

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

  // Modal states
  const [isCreateOpen, setIsCreateOpen] = useState<boolean>(false);
  const [editingUser, setEditingUser] = useState<AdminUserItem | null>(null);

  // Form states
  const [formEmail, setFormEmail] = useState<string>('');
  const [formName, setFormName] = useState<string>('');
  const [formPassword, setFormPassword] = useState<string>('');
  const [formRole, setFormRole] = useState<string>('user');
  const [formQuotaMb, setFormQuotaMb] = useState<number>(5120); // 5GB default
  const [formIsActive, setFormIsActive] = useState<boolean>(true);

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

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  const handleOpenCreate = () => {
    setFormEmail('');
    setFormName('');
    setFormPassword('');
    setFormRole('user');
    setFormQuotaMb(5120);
    setFormIsActive(true);
    setIsCreateOpen(true);
  };

  const handleOpenEdit = (user: AdminUserItem) => {
    setEditingUser(user);
    setFormName(user.name);
    setFormRole(user.role);
    setFormQuotaMb(Math.round(user.mailbox_quota_bytes / (1024 * 1024)));
    setFormIsActive(user.is_active);
  };

  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formEmail.trim() || !formName.trim() || !formPassword.trim()) {
      setError('Email, Name, and Password are required.');
      return;
    }

    try {
      const payload: CreateUserRequest = {
        email: formEmail.trim(),
        name: formName.trim(),
        password: formPassword,
        role: formRole,
        quota_bytes: formQuotaMb * 1024 * 1024,
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
        name: formName.trim(),
        role: formRole,
        is_active: formIsActive,
        quota_bytes: formQuotaMb * 1024 * 1024,
      };
      await client.updateUser(editingUser.id, payload);
      setEditingUser(null);
      setActionMessage(`User ${editingUser.email} updated successfully.`);
      fetchUsers();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update user');
    }
  };

  const handleDelete = async (user: AdminUserItem) => {
    if (!window.confirm(`Are you sure you want to permanently delete user ${user.email}? This will remove their mailbox and all messages.`)) {
      return;
    }

    try {
      await client.deleteUser(user.id);
      setActionMessage(`User ${user.email} deleted.`);
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

  const filteredUsers = users.filter((u) => {
    const matchesSearch =
      u.email.toLowerCase().includes(search.toLowerCase()) ||
      u.name.toLowerCase().includes(search.toLowerCase());
    const matchesRole = roleFilter === 'all' || u.role.toLowerCase() === roleFilter.toLowerCase();
    return matchesSearch && matchesRole && matchesDomain(domainFilter, u.email);
  });

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
        <div className="chip-group" role="tablist" aria-label="User role filter">
          {['all', 'admin', 'user', 'service'].map((role) => (
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
        {loading ? (
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
                <div className="form-group">
                  <label className="form-label">Email Address</label>
                  <input
                    type="email"
                    className="input-text w-full"
                    placeholder="user@internal.domain"
                    value={formEmail}
                    onChange={(e) => setFormEmail(e.target.value)}
                    required
                  />
                </div>
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
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Membership Role</label>
                    <select
                      className="input-select w-full"
                      value={formRole}
                      onChange={(e) => setFormRole(e.target.value)}
                    >
                      <option value="user">User</option>
                      <option value="admin">Admin</option>
                      <option value="service">Service Principal</option>
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
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setIsCreateOpen(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary">
                  Create User
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
                      <option value="user">User</option>
                      <option value="admin">Admin</option>
                      <option value="service">Service Principal</option>
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
    </div>
  );
};
