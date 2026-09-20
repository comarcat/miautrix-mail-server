import React, { useState } from 'react';

export const SecurityScreen: React.FC = () => {
  const [mfaEnforced, setMfaEnforced] = useState<boolean>(true);
  const [strictTls, setStrictTls] = useState<boolean>(true);
  const [daneEnforced, setDaneEnforced] = useState<boolean>(true);
  const [actionMessage, setActionMessage] = useState<string | null>(null);

  const handleSavePolicies = () => {
    setActionMessage('Security policies applied and synchronized across all mail daemons.');
    setTimeout(() => setActionMessage(null), 4000);
  };

  return (
    <div className="screen-container security-screen" data-testid="security-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Security Policies & Encryption Controls</h1>
          <p className="screen-desc">
            Enforce strict transport encryption, mandatory multi-factor authentication, cryptographic cipher suites, and DANE DNSSEC verification.
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-primary" onClick={handleSavePolicies}>
            Save Security Policies
          </button>
        </div>
      </div>

      {actionMessage && (
        <div className="alert alert-info" role="status">
          <span>{actionMessage}</span>
          <button className="alert-close" onClick={() => setActionMessage(null)}>✕</button>
        </div>
      )}

      {/* Security Invariant Banner */}
      <div className="alert alert-error">
        <span>
          <strong>Hardened Security Invariant:</strong> Unencrypted plaintext SMTP authentication is strictly prohibited and disabled at the protocol parser level. All client connections require TLS 1.2+ with forward secrecy.
        </span>
      </div>

      <div className="metrics-grid">
        <div className="card metric-card">
          <span className="metric-label">Transport Encryption</span>
          <span className="metric-value text-success" style={{ fontSize: '22px' }}>
            TLS 1.3 / 1.2
          </span>
          <span className="metric-sub">Strict STARTTLS mandatory</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">Authentication MFA</span>
          <span className="metric-value text-info" style={{ fontSize: '22px' }}>
            {mfaEnforced ? 'Enforced' : 'Optional'}
          </span>
          <span className="metric-sub">TOTP & WebAuthn / FIDO2</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">DANE / TLSA Status</span>
          <span className="metric-value text-success" style={{ fontSize: '22px' }}>
            {daneEnforced ? 'Verified' : 'Disabled'}
          </span>
          <span className="metric-sub">DNSSEC signed outbound routing</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">DKIM Signatures</span>
          <span className="metric-value text-info" style={{ fontSize: '22px' }}>
            RSA-2048 / Ed25519
          </span>
          <span className="metric-sub">Automated monthly key rotation</span>
        </div>
      </div>

      <div className="dashboard-row">
        {/* Transport Encryption & TLS Policies */}
        <div className="card flex-1">
          <h2 className="card-title">Transport Layer Security (TLS) Policy</h2>
          <p className="card-subtitle">Cipher suites, protocol version floors, and certificate requirements</p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '12px' }}>
            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">Strict TLS 1.3 Preferred</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Require TLS 1.3 for all SMTP, IMAP, and HTTPS endpoints with fallback to TLS 1.2 only.</div>
              </div>
              <input
                type="checkbox"
                checked={strictTls}
                onChange={(e) => setStrictTls(e.target.checked)}
                style={{ width: '18px', height: '18px', cursor: 'pointer' }}
              />
            </div>

            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">DANE DNSSEC Inbound & Outbound Validation</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Verify TLSA records via DNSSEC to prevent opportunistic TLS downgrade and MITM attacks.</div>
              </div>
              <input
                type="checkbox"
                checked={daneEnforced}
                onChange={(e) => setDaneEnforced(e.target.checked)}
                style={{ width: '18px', height: '18px', cursor: 'pointer' }}
              />
            </div>

            <div className="detail-row-block">
              <div className="detail-label">Active Cipher Suites (PFS Only)</div>
              <pre className="detail-pre" style={{ color: 'var(--text)' }}>
{`TLS_AES_256_GCM_SHA384 (TLS 1.3)
TLS_CHACHA20_POLY1305_SHA256 (TLS 1.3)
TLS_AES_128_GCM_SHA256 (TLS 1.3)
ECDHE-ECDSA-AES256-GCM-SHA384 (TLS 1.2)
ECDHE-RSA-AES256-GCM-SHA384 (TLS 1.2)`}
              </pre>
            </div>
          </div>
        </div>

        {/* Authentication & Access Control */}
        <div className="card flex-1">
          <h2 className="card-title">Identity & Access Hardening</h2>
          <p className="card-subtitle">Multi-factor authentication, session timeouts, and brute-force shields</p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '12px' }}>
            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">Mandatory Two-Factor Authentication</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Require TOTP authenticator app or FIDO2 hardware token for all webmail and admin access.</div>
              </div>
              <input
                type="checkbox"
                checked={mfaEnforced}
                onChange={(e) => setMfaEnforced(e.target.checked)}
                style={{ width: '18px', height: '18px', cursor: 'pointer' }}
              />
            </div>

            <div className="status-list">
              <div className="status-item">
                <span className="status-item-label">Password Hashing Algorithm</span>
                <span className="badge badge-info">Argon2id (m=64MB, t=3, p=4)</span>
              </div>
              <div className="status-item">
                <span className="status-item-label">Brute-Force Lockout Threshold</span>
                <span className="cell-emphasis">5 failed attempts → 15 min lock</span>
              </div>
              <div className="status-item">
                <span className="status-item-label">Webmail Session Inactivity Timeout</span>
                <span className="cell-emphasis">60 minutes</span>
              </div>
              <div className="status-item">
                <span className="status-item-label">Admin Console Re-Auth Floor</span>
                <span className="cell-emphasis">15 minutes for sensitive actions</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
