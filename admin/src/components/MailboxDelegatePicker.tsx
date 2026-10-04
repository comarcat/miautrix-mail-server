import React from 'react';
import { AdminUserItem, MailboxDelegateRequest } from '../types';

interface MailboxDelegatePickerProps {
  users: AdminUserItem[];
  /** Mailbox domain; only active user mailboxes on this domain are eligible. */
  domain: string;
  /** The mailbox's own address, which cannot delegate to itself. */
  excludeEmail?: string;
  selected: MailboxDelegateRequest[];
  onChange: (delegates: MailboxDelegateRequest[]) => void;
  label?: string;
  testIdPrefix?: string;
}

export const MailboxDelegatePicker: React.FC<MailboxDelegatePickerProps> = ({
  users,
  domain,
  excludeEmail,
  selected,
  onChange,
  label = 'Delegates (same domain)',
  testIdPrefix = 'delegate',
}) => {
  const eligible = users.filter((user) =>
    user.mailbox_kind === 'user' &&
    user.is_active &&
    !!domain &&
    user.email.toLowerCase().endsWith(`@${domain.toLowerCase()}`) &&
    user.email.toLowerCase() !== (excludeEmail || '').toLowerCase());

  const toggle = (userId: string) => {
    onChange(selected.some((delegate) => delegate.user_id === userId)
      ? selected.filter((delegate) => delegate.user_id !== userId)
      : [...selected, { user_id: userId, access_level: 'read' }]);
  };

  const setAccess = (userId: string, accessLevel: 'read' | 'write') => {
    onChange(selected.map((delegate) =>
      delegate.user_id === userId ? { user_id: userId, access_level: accessLevel } : delegate));
  };

  return (
    <div className="form-group">
      <label className="form-label">{label}</label>
      {eligible.length === 0 ? (
        <p className="cell-dim">No active same-domain users available.</p>
      ) : eligible.map((user) => {
        const delegate = selected.find((item) => item.user_id === user.id);
        return (
          <div key={user.id} style={{ display: 'flex', gap: '8px', alignItems: 'center', marginBottom: '6px' }}>
            <input
              type="checkbox"
              checked={!!delegate}
              onChange={() => toggle(user.id)}
              data-testid={`${testIdPrefix}-${user.id}`}
            />
            <span style={{ flex: 1 }}>{user.name} ({user.email})</span>
            {delegate && (
              <select
                className="input-select"
                value={delegate.access_level}
                onChange={(e) => setAccess(user.id, e.target.value as 'read' | 'write')}
                aria-label={`Access level for ${user.email}`}
              >
                <option value="read">Read</option>
                <option value="write">Read / Write</option>
              </select>
            )}
          </div>
        );
      })}
    </div>
  );
};
