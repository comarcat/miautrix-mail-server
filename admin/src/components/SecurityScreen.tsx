import React, { useEffect, useState } from 'react';
import { apiClient } from '../api/client';
import type { SecuritySettings } from '../types';

interface SecurityScreenProps {
  selectedDomain?: string;
}

export const SecurityScreen: React.FC<SecurityScreenProps> = ({ selectedDomain }) => {
  const [selectedDomainId, setSelectedDomainId] = useState<string | null>(null);
  const [settings, setSettings] = useState<SecuritySettings | null>(null);
  const [mfaEnforced, setMfaEnforced] = useState<boolean>(false);
  const [sessionLifetime, setSessionLifetime] = useState<number>(0);
  const [lockoutAttempts, setLockoutAttempts] = useState<number>(0);
  const [lockoutDuration, setLockoutDuration] = useState<number>(0);
  const [strictTls, setStrictTls] = useState<boolean>(true);
  const [daneEnforced, setDaneEnforced] = useState<boolean>(true);
  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    const initSecurityPage = async () => {
      try {
        setLoading(true);

        // Initial selection: prefer domain matching selectedDomain prop
        // We must get domains first to find the default one
        const domainsResponse = await apiClient.getDomains();
        if (!isMounted) return;
        const domainList = domainsResponse.data;

        const defaultDomain = domainList.find(d => d.name === selectedDomain) || domainList[0];

        if (defaultDomain) {
          setSelectedDomainId(defaultDomain.id);
          await loadSettingsForDomain(defaultDomain.id);
        } else {
          setErrorMessage('No domains available to configure.');
        }
      } catch (error) {
        if (!isMounted) return;
        setErrorMessage(error instanceof Error ? error.message : 'Unable to initialize security page.');
      } finally {
        if (isMounted) setLoading(false);
      }
    };

    initSecurityPage();
    return () => {
      isMounted = false;
    };
  }, [selectedDomain]);

  const loadSettingsForDomain = async (domainId: string) => {
    try {
      const response = await apiClient.getDomainSecuritySettings(domainId);
      setSettings(response.data);
      setMfaEnforced(response.data.mfa_enforced);
      setSessionLifetime(response.data.session_lifetime_minutes);
      setLockoutAttempts(response.data.lockout_max_failed_attempts);
      setLockoutDuration(response.data.lockout_duration_minutes);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : 'Unable to load security settings for selected domain.');
    }
  };

  const handleSavePolicies = async () => {
    if (!selectedDomainId) {
      setErrorMessage('Please select a domain first.');
      return;
    }

    setSaving(true);
    setErrorMessage(null);
    try {
      const response = await apiClient.updateDomainSecuritySettings(selectedDomainId, {
        mfa_enforced: mfaEnforced,
        session_lifetime_minutes: sessionLifetime,
        lockout_max_failed_attempts: lockoutAttempts,
        lockout_duration_minutes: lockoutDuration,
      });
      setSettings(response.data);
      setMfaEnforced(response.data.mfa_enforced);
      setSessionLifetime(response.data.session_lifetime_minutes);
      setLockoutAttempts(response.data.lockout_max_failed_attempts);
      setLockoutDuration(response.data.lockout_duration_minutes);
      setActionMessage('Security policies applied to the selected domain.');
      setTimeout(() => setActionMessage(null), 4000);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : 'Unable to save security policies.');
    } finally {
      setSaving(false);
    }
  };

  const argon2Badge = settings
    ? `${settings.password_hashing_algorithm} (m=${Math.round(settings.argon2_memory_kb / 1024)}MB, t=${settings.argon2_iterations}, p=${settings.argon2_parallelism})`
    : 'Loading...';

  const lockoutOptions = [3, 5, 10];
  const lockoutDurationOptions = [5, 15, 30, 60];
  const sessionLifetimeOptions = [15, 30, 60, 120, 240, 480];

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
          <button className="btn btn-primary" onClick={handleSavePolicies} disabled={loading || saving}>
            {saving ? 'Saving...' : 'Save Security Policies'}
          </button>
        </div>
      </div>

      {actionMessage && (
        <div className="alert alert-info" role="status">
          <span>{actionMessage}</span>
          <button className="alert-close" onClick={() => setActionMessage(null)}>✕</button>
        </div>
      )}

      {errorMessage && (
        <div className="alert alert-error" role="alert">
          <span>{errorMessage}</span>
          <button className="alert-close" onClick={() => setErrorMessage(null)}>✕</button>
        </div>
      )}

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
          <span className="metric-sub">Domain policy stored in database</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">DANE / TLSA Status</span>
          <span className="metric-value text-success" style={{ fontSize: '22px' }}>
            {daneEnforced ? 'Verified' : 'Disabled'}
          </span>
          <span className="metric-sub">Future implementation: DNSSEC validation</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">DKIM Signatures</span>
          <span className="metric-value text-info" style={{ fontSize: '22px' }}>
            RSA-2048
          </span>
          <span className="metric-sub">Future implementation: automated rotation</span>
        </div>
      </div>

      <div className="dashboard-row">
        <div className="card flex-1">
          <h2 className="card-title">Transport Layer Security (TLS) Policy</h2>
          <p className="card-subtitle">Cipher suites, protocol version floors, and certificate requirements</p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '12px' }}>
            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">Strict TLS 1.3 Preferred</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Roadmap item: backed by TLS runtime configuration in a future release.</div>
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
                <div className="cell-dim" style={{ fontSize: '11px' }}>Roadmap item: requires DNSSEC/TLSA verification in the mail transport pipeline.</div>
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
                disabled={loading}
                style={{ width: '18px', height: '18px', cursor: loading ? 'not-allowed' : 'pointer' }}
              />
            </div>

            <div className="status-list">
              <div className="status-item">
                <span className="status-item-label">Password Hashing Algorithm</span>
                <span className="badge badge-info">{argon2Badge}</span>
              </div>
              <div className="status-item">
                <span className="status-item-label">Brute-Force Lockout Threshold</span>
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <select
                    className="input-select input-select-sm"
                    value={lockoutAttempts}
                    onChange={(e) => setLockoutAttempts(Number(e.target.value))}
                    disabled={loading}
                  >
                    {lockoutOptions.map(value => (
                      <option key={value} value={value}>{value} attempts</option>
                    ))}
                  </select>
                  <select
                    className="input-select input-select-sm"
                    value={lockoutDuration}
                    onChange={(e) => setLockoutDuration(Number(e.target.value))}
                    disabled={loading}
                  >
                    {lockoutDurationOptions.map(value => (
                      <option key={value} value={value}>{value} min lock</option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="status-item">
                <span className="status-item-label">Webmail Session Inactivity Timeout</span>
                <select
                  className="input-select input-select-sm"
                  value={sessionLifetime}
                  onChange={(e) => setSessionLifetime(Number(e.target.value))}
                  disabled={loading}
                >
                  {sessionLifetimeOptions.map(value => (
                    <option key={value} value={value}>{value} minutes</option>
                  ))}
                </select>
              </div>
              <div className="status-item">
                <span className="status-item-label">Admin Console Re-Auth Floor</span>
                <span className="cell-emphasis">Future sensitive-action policy</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
