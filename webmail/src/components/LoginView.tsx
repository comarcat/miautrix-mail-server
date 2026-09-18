import React, { useState } from 'react';

interface LoginViewProps {
  onLoginSuccess: (email: string) => void;
}

export const LoginView: React.FC<LoginViewProps> = ({ onLoginSuccess }) => {
  const [email, setEmail] = useState('alex.vance@miautrix.org');
  const [password, setPassword] = useState('password123');
  const [remember, setRemember] = useState(true);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (email && password) {
      onLoginSuccess(email);
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
              required
            />
          </div>

          <div className="form-group" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <input
              type="checkbox"
              id="remember"
              checked={remember}
              onChange={(e) => setRemember(e.target.checked)}
              style={{ accentColor: 'var(--iris-violet)', width: '16px', height: '16px', borderRadius: '4px' }}
            />
            <label htmlFor="remember" style={{ margin: 0, fontSize: '14px', fontWeight: 400, color: 'var(--neutral-body)', cursor: 'pointer' }}>
              Remember this device
            </label>
          </div>

          <button
            className="btn btn-primary w-full"
            type="submit"
            style={{ padding: '12px', marginTop: '8px', fontSize: '16px' }}
          >
            Sign In
          </button>
        </form>

        <div className="auth-footer">
          <span>Protected by Miautrix Security · Argon2id & TOTP</span>
        </div>
      </div>
    </div>
  );
};
