import React, { useState, useEffect, useCallback } from 'react';
import { LicensingInfo, ApiResponse } from '../types';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';

interface LicensingScreenProps {
  client?: AdminApiClient;
}

export const LicensingScreen: React.FC<LicensingScreenProps> = ({ client = defaultClient }) => {
  const [licensing, setLicensing] = useState<LicensingInfo | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const fetchLicensing = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res: ApiResponse<LicensingInfo> = await client.getLicensing();
      setLicensing(res.data);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch licensing');
    } finally {
      setLoading(false);
    }
  }, [client]);

  useEffect(() => {
    fetchLicensing();
  }, [fetchLicensing]);

  return (
    <div className="screen-container licensing-screen" data-testid="licensing-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Subscription & Licensing Entitlements</h1>
          <p className="screen-desc">
            Review edition tier, active mailbox capacity, core feature entitlements, and operational guarantees.
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-secondary" onClick={fetchLicensing} title="Refresh license status">
            Refresh
          </button>
        </div>
      </div>

      {error && (
        <div className="alert alert-error" role="alert">
          <span>{error}</span>
          <button className="alert-close" onClick={() => setError(null)}>✕</button>
        </div>
      )}

      {loading && !licensing ? (
        <div className="loading-state" data-testid="licensing-loading">
          <div className="spinner" />
          <p>Validating licensing status...</p>
        </div>
      ) : (
        <>
          {/* License Invariant Guarantees */}
          <div className="alert alert-info">
            <span>
              <strong>Miautrix Guarantee:</strong> MFA, automated backups, and local administrator access are included free in every edition. If the license authority is unreachable, mail continues flowing uninterrupted. Exceeding mailbox limits restricts new mailbox provisioning only — existing data is never locked or degraded.
            </span>
          </div>

          <div className="metrics-grid">
            <div className="card metric-card">
              <span className="metric-label">License Edition</span>
              <span className="metric-value text-info" style={{ fontSize: '24px' }}>
                {licensing?.edition || 'Enterprise'}
              </span>
              <span className="metric-sub">Multi-tenant production cluster</span>
            </div>

            <div className="card metric-card">
              <span className="metric-label">License Status</span>
              <span className="metric-value text-success" style={{ fontSize: '24px' }}>
                {licensing?.status || 'Active & Valid'}
              </span>
              <span className="metric-sub">
                Valid until {licensing?.valid_until ? new Date(licensing.valid_until).toLocaleDateString() : 'Perpetual'}
              </span>
            </div>

            <div className="card metric-card">
              <span className="metric-label">Active Mailboxes</span>
              <span className="metric-value" style={{ fontSize: '24px' }}>
                {licensing?.active_mailboxes || 1} / {licensing?.max_mailboxes || 1000}
              </span>
              <span className="metric-sub">
                {Math.max(0, (licensing?.max_mailboxes || 1000) - (licensing?.active_mailboxes || 1))} seats available
              </span>
            </div>
          </div>

          <div className="dashboard-row">
            {/* Feature Entitlements */}
            <div className="card flex-1">
              <h2 className="card-title">Feature Entitlements</h2>
              <p className="card-subtitle">Capabilities enabled under the current license key</p>

              <div className="status-list">
                <div className="status-item">
                  <span className="status-item-label">Two-Factor Authentication (TOTP / WebAuthn)</span>
                  <span className="badge badge-success">Included (Free)</span>
                </div>
                <div className="status-item">
                  <span className="status-item-label">Automated Backups & Point-in-Time Restore</span>
                  <span className="badge badge-success">Included (Free)</span>
                </div>
                <div className="status-item">
                  <span className="status-item-label">Local Admin Access & CLI Management</span>
                  <span className="badge badge-success">Included (Free)</span>
                </div>
                <div className="status-item">
                  <span className="status-item-label">Custom Accepted Domains</span>
                  <span className="badge badge-success">Unlimited</span>
                </div>
                <div className="status-item">
                  <span className="status-item-label">Multi-Tenant Isolation Engine</span>
                  <span className="badge badge-success">Enabled</span>
                </div>
                <div className="status-item">
                  <span className="status-item-label">JMAP & Modern Sync Protocol</span>
                  <span className="badge badge-success">Enabled</span>
                </div>
              </div>
            </div>

            {/* Quota Gauge */}
            <div className="card flex-1">
              <h2 className="card-title">Mailbox Capacity Meter</h2>
              <p className="card-subtitle">Provisioned tenant mailboxes vs license quota ceiling</p>

              {licensing && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '16px' }}>
                  <div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                      <span className="form-label">Total Provisioned Seats</span>
                      <span className="cell-mono cell-emphasis">
                        {licensing.active_mailboxes} of {licensing.max_mailboxes} seats ({Math.round((licensing.active_mailboxes / licensing.max_mailboxes) * 100)}%)
                      </span>
                    </div>
                    <div style={{ width: '100%', height: '12px', background: 'var(--page)', borderRadius: '6px', overflow: 'hidden' }}>
                      <div
                        style={{
                          width: `${Math.min(100, Math.max(2, (licensing.active_mailboxes / licensing.max_mailboxes) * 100))}%`,
                          height: '100%',
                          background: 'var(--success)',
                          borderRadius: '6px',
                        }}
                      />
                    </div>
                  </div>

                  <div className="empty-state-card" style={{ padding: '24px', background: 'var(--page)', borderRadius: '6px' }}>
                    <p style={{ fontSize: '12px', color: 'var(--text-dim)', marginBottom: '8px' }}>
                      Need to expand mailbox allocation or request custom compliance plugins?
                    </p>
                    <button className="btn btn-secondary btn-sm" onClick={() => alert('Contacting Miautrix License Portal')}>
                      Manage Subscription
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
};
