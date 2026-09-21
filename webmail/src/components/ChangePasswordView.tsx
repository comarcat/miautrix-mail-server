import React, { useState } from 'react';
import { webmailClient } from './WebmailApiClient';

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
      await webmailClient.changePassword(currentPassword, newPassword);
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
    <div className="auth-wrapper">
      <div className="card auth-card">
        <div className="auth-header">
          <h1>Change Password</h1>
          <p>You must change your password before continuing.</p>
        </div>

        {error && (
          <div className="alert alert-error" style={{ marginBottom: '16px' }} role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="currentPassword">Current password</label>
            <input
              id="currentPassword"
              className="input-base"
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              disabled={loading}
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="newPassword">New password</label>
            <input
              id="newPassword"
              className="input-base"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              disabled={loading}
              required
            />
          </div>

          <button
            className="btn btn-primary w-full"
            type="submit"
            style={{ padding: '12px', marginTop: '8px', fontSize: '16px' }}
            disabled={loading}
          >
            {loading ? 'Updating...' : 'Update Password'}
          </button>
        </form>
      </div>
    </div>
  );
};
