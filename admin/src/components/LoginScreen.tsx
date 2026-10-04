import React, { useState } from 'react';
import { apiClient } from '../api/client';

interface LoginScreenProps {
  onLoginSuccess?: (email: string) => void;
}

export const LoginScreen: React.FC<LoginScreenProps> = ({ onLoginSuccess }) => {
  const [email, setEmail] = useState('admin@miautrix.org');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      await apiClient.login(email, password);
      if (onLoginSuccess) {
        onLoginSuccess(email);
      } else {
        window.location.href = '/';
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Login failed');
      setLoading(false);
    }
  };

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      height: '100vh',
      background: 'var(--page)',
      color: 'var(--text)'
    }}>
      <div className="card" style={{ width: '400px', maxWidth: '90%', padding: '32px' }}>
        <h1 className="screen-title" style={{ textAlign: 'center', marginBottom: '8px' }}>
          Miautrix Console
        </h1>
        <p className="screen-desc" style={{ textAlign: 'center', marginBottom: '32px' }}>
          Sign in with administrator credentials
        </p>

        {error && (
          <div className="alert alert-error" style={{ marginBottom: '16px' }} role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div>
            <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', fontWeight: 500, color: 'var(--head-text)' }}>
              Email Address
            </label>
            <input
              type="text"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
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
            <label style={{ display: 'block', marginBottom: '8px', fontSize: '13px', fontWeight: 500, color: 'var(--head-text)' }}>
              Password
            </label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={loading}
              style={{
                width: '100%',
                padding: '10px 12px',
                background: 'var(--page)',
                border: '1px solid var(--border)',
                color: 'var(--text)',
                borderRadius: '6px',
                fontFamily: 'var(--font-body)',
                fontSize: '14px'
              }}
              required
            />
          </div>

          <button
            type="submit"
            className="btn btn-primary"
            style={{ width: '100%', padding: '12px', marginTop: '8px' }}
            disabled={loading}
          >
            {loading ? 'Authenticating...' : 'Sign In'}
          </button>
        </form>
      </div>
    </div>
  );
};
