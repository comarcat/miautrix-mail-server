import React, { useState } from 'react';

export const IdentityScreen: React.FC = () => {
  const [ssoEnabled, setSsoEnabled] = useState<boolean>(true);
  const [jitProvisioning, setJitProvisioning] = useState<boolean>(true);
  const [enforceSsoOnly, setEnforceSsoOnly] = useState<boolean>(false);
  const [actionMessage, setActionMessage] = useState<string | null>(null);

  const handleSaveProviders = () => {
    setActionMessage('Identity Federation and Single Sign-On policies synchronized.');
    setTimeout(() => setActionMessage(null), 4000);
  };

  return (
    <div className="screen-container identity-screen" data-testid="identity-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Identity Federation & Single Sign-On (SSO)</h1>
          <p className="screen-desc">
            Federate authentication with OpenID Connect (OIDC), SAML 2.0, Microsoft Entra ID, Google Workspace, and SCIM directory sync.
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-primary" onClick={handleSaveProviders}>
            Save SSO Configuration
          </button>
        </div>
      </div>

      {actionMessage && (
        <div className="alert alert-info" role="status">
          <span>{actionMessage}</span>
          <button className="alert-close" onClick={() => setActionMessage(null)}>✕</button>
        </div>
      )}

      <div className="metrics-grid">
        <div className="card metric-card">
          <span className="metric-label">SSO Federation</span>
          <span className="metric-value text-success" style={{ fontSize: '22px' }}>
            {ssoEnabled ? 'Active (OIDC)' : 'Disabled'}
          </span>
          <span className="metric-sub">PKCE + State Verification</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">Identity Provider</span>
          <span className="metric-value text-info" style={{ fontSize: '22px' }}>
            Microsoft Entra
          </span>
          <span className="metric-sub">Tenant: miautrix.onmicrosoft.com</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">Directory Sync (SCIM)</span>
          <span className="metric-value text-success" style={{ fontSize: '22px' }}>
            Synchronized
          </span>
          <span className="metric-sub">42 active federated accounts</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">Local Auth Fallback</span>
          <span className="metric-value" style={{ fontSize: '22px' }}>
            {enforceSsoOnly ? 'Disabled (SSO Only)' : 'Enabled (Admin Only)'}
          </span>
          <span className="metric-sub">Argon2id emergency break-glass</span>
        </div>
      </div>

      <div className="dashboard-row">
        {/* OpenID Connect & SAML Settings */}
        <div className="card flex-1">
          <h2 className="card-title">Configured Identity Providers</h2>
          <p className="card-subtitle">Connect enterprise identity directories for seamless unified login</p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '16px' }}>
            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">Enable OpenID Connect / SAML SSO</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Allow tenant users to authenticate via external identity provider.</div>
              </div>
              <input
                type="checkbox"
                checked={ssoEnabled}
                onChange={(e) => setSsoEnabled(e.target.checked)}
                style={{ width: '18px', height: '18px', cursor: 'pointer' }}
              />
            </div>

            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">Just-in-Time (JIT) Mailbox Provisioning</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Automatically provision local mailbox and user account upon initial successful SSO assertion.</div>
              </div>
              <input
                type="checkbox"
                checked={jitProvisioning}
                onChange={(e) => setJitProvisioning(e.target.checked)}
                style={{ width: '18px', height: '18px', cursor: 'pointer' }}
              />
            </div>

            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">Enforce SSO Authentication (No Local Passwords)</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Prohibit password logins for standard users (break-glass admin exempt).</div>
              </div>
              <input
                type="checkbox"
                checked={enforceSsoOnly}
                onChange={(e) => setEnforceSsoOnly(e.target.checked)}
                style={{ width: '18px', height: '18px', cursor: 'pointer' }}
              />
            </div>
          </div>
        </div>

        {/* Group to Role Mappings */}
        <div className="card flex-1">
          <h2 className="card-title">Directory Role Mappings</h2>
          <p className="card-subtitle">Map IdP security groups to Miautrix tenant membership roles</p>

          <div className="status-list" style={{ marginTop: '16px' }}>
            <div className="status-item">
              <div>
                <div className="cell-emphasis">AAD-Mail-Administrators</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Claims group: 8e5f2c41-9a1b-4d7e...</div>
              </div>
              <span className="badge badge-error">Tenant Owner / Admin</span>
            </div>

            <div className="status-item">
              <div>
                <div className="cell-emphasis">AAD-Security-Auditors</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Claims group: 3b1a9c82-4f6d-4a1e...</div>
              </div>
              <span className="badge badge-warning">Audit &amp; Compliance Officer</span>
            </div>

            <div className="status-item">
              <div>
                <div className="cell-emphasis">AAD-All-Employees</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Default user directory group</div>
              </div>
              <span className="badge badge-info">Standard Mailbox User</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
