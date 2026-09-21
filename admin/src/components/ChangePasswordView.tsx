import React, { useState } from 'react';
import { apiClient } from '../api/client';

interface ChangePasswordViewProps {
  onChanged: () => Promise<void>;
}

export const ChangePasswordView: React.FC<ChangePasswordViewProps> = ({ onChanged }) => {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentPassword || !newPassword) return;

    setLoading(true);
    setError(null);

    try {
      await apiClient.changePassword(currentPassword, newPassword);
      setCurrentPassword('');
      setNewPassword('');
      await onChanged();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Password change failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100vh', background: 'var(--page)', color: 'var(--text)' }}>
      <div className="card" style={{ width: '520px', maxWidth: '90%', padding: '32px' }}>
        <div style={{ marginBottom: '16px' }}>
          <h1 className="screen-title" style={{ textAlign: 'center', marginBottom: '8px' }}>Change Password</h1>
          <p className="screen-desc" style={{ textAlign: 'center', marginBottom: 0 }}>You must change your password before continuing.</p>
        </div>

        {error && (
          <div className="alert alert-error" style={{ marginBottom: '16px' }} role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', fontWeight: 500, color: 'var(--head-text)' }} htmlFor="currentPassword">
              Current password
            </label>
            <input
              id="currentPassword"
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              disabled={loading}
              style={{
                width: '100%',
                padding: '10px 12px',
                background: 'var(--page)',
                border: '1px solid var(--border)',
                color: 'var(--text)',
                borderRadius: '6px',
                fontFamily: 'var(--font-mono)',
                fontSize: '14px'
              }}
              required
            />
          </div>

          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', fontWeight: 500, color: 'var(--head-text)' }} htmlFor="newPassword">
              New password
            </label>
            <input
              id="newPassword"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              disabled={loading}
              style={{
                width: '100%',
                padding: '10px 12px',
                background: 'var(--page)',
                border: '1px solid var(--border)',
                color: 'var(--text)',
                borderRadius: '6px',
                fontFamily: 'var(--font-mono)',
                fontSize: '14px'
              }}
              required
            />
          </div>

          <button
            className="btn btn-primary"
            type="submit"
            style={{ width: '100%', padding: '12px', marginTop: '8px' }}
            disabled={loading}
          >
            {loading ? 'Updating...' : 'Update Password'}
          </button>
        </form>
      </div>
    </div>
  );
};
