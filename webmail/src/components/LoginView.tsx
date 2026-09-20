import React, { useState } from 'react';
import { webmailClient } from './WebmailApiClient';

interface LoginViewProps {
  onLoginSuccess: (email: string) => void;
}

export const LoginView: React.FC<LoginViewProps> = ({ onLoginSuccess }) => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !password) return;

    setLoading(true);
    setError(null);

    try {
      await webmailClient.login(email, password);
      onLoginSuccess(email);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Login failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-wrapper">
      <div className="card auth-card">
        <div className="auth-header">
          <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '20px' }}>
            <img
              src="/images/LogoIcon.png"
              alt="Miautrix Webmail Logo"
              style={{ height: '64px', objectFit: 'contain' }}
            />
          </div>
          <h1>Miautrix Webmail</h1>
          <p>Sign in to your mailbox</p>
        </div>

        {error && (
          <div className="alert alert-error" style={{ marginBottom: '16px' }} role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="email">Email address</label>
            <input
              id="email"
              className="input-base"
              type="email"
              placeholder="user@yourdomain.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={loading}
              required
            />
          </div>

          <div className="form-group">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
              <label htmlFor="password" style={{ marginBottom: 0 }}>Password</label>
              <a href="#forgot" style={{ color: 'var(--iris-violet)', fontSize: '13px', textDecoration: 'none', fontWeight: 500 }}>
                Forgot password?
              </a>
            </div>
            <input
              id="password"
              className="input-base"
              type="password"
              placeholder="••••••••••••"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
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
            {loading ? 'Signing in...' : 'Sign In'}
          </button>
        </form>

        <div className="auth-footer">
          <span>Protected by Miautrix Security · Argon2id & TOTP</span>
        </div>
      </div>
    </div>
  );
};
