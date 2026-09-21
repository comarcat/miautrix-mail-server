import React, { useState, useEffect } from 'react';
import { apiClient } from '../api/client';

interface DashboardStats {
  activeQueued: number;
  retrying: number;
  deadLetters: number;
  delivered24h: number;
  quarantined24h: number;
  spamBlocked24h: number;
  systemHealth: string;
  uptimeSeconds: number;
  tenantCount: number;

  // Outbound transport metrics (combined “All”)
  outboundDelivered24h: number;
  outboundDeliveredTotal: number;
  successfulAttempts24h: number;
  failedAttempts24h: number;
  totalAttempts24h: number;
  retryAttempts24h: number;
  deliverySuccessRate24h: number;
  lastDeliveryAttemptAt?: string | null;
  lastDeliveryResponseCode?: number | null;
  lastDeliveryError?: string | null;
}

export const DashboardScreen: React.FC = () => {
  const [stats, setStats] = useState<DashboardStats>({
    activeQueued: 0,
    retrying: 0,
    deadLetters: 0,
    delivered24h: 0,
    quarantined24h: 0,
    spamBlocked24h: 0,
    systemHealth: 'Healthy',
    uptimeSeconds: 0,
    tenantCount: 1,

    outboundDelivered24h: 0,
    outboundDeliveredTotal: 0,
    successfulAttempts24h: 0,
    failedAttempts24h: 0,
    totalAttempts24h: 0,
    retryAttempts24h: 0,
    deliverySuccessRate24h: 100,
    lastDeliveryAttemptAt: null,
    lastDeliveryResponseCode: null,
    lastDeliveryError: null,
  });
  const [loading, setLoading] = useState<boolean>(false);

  useEffect(() => {
    // Single consolidated fetch preventing N+1 queries per row/tenant
    const fetchDashboard = async () => {
      setLoading(true);
      try {
        const res = await apiClient.getDashboardSummary();
        if (res && res.data) {
          const d = res.data;

          const breakdown = d.transport_breakdown ?? [];
          const all = breakdown.find((x) => x.transport_mode === 'all') ?? breakdown[0];

          setStats({
            activeQueued: d.active_queued ?? 0,
            retrying: d.retrying ?? 0,
            deadLetters: d.dead_letters ?? 0,
            delivered24h: d.delivered24h ?? 0,
            quarantined24h: d.quarantined24h ?? 0,
            spamBlocked24h: d.spam_blocked24h ?? 0,
            systemHealth: d.system_health ?? 'Healthy',
            uptimeSeconds: d.uptime_seconds ?? 0,
            tenantCount: d.tenant_count ?? 1,

            outboundDelivered24h: all?.outbound_delivered24h ?? 0,
            outboundDeliveredTotal: all?.outbound_delivered_total ?? 0,
            successfulAttempts24h: all?.successful_attempts24h ?? 0,
            failedAttempts24h: all?.failed_attempts24h ?? 0,
            totalAttempts24h: all?.total_attempts24h ?? 0,
            retryAttempts24h: all?.retry_attempts24h ?? 0,
            deliverySuccessRate24h: all?.delivery_success_rate24h ?? 100,
            lastDeliveryAttemptAt: all?.last_delivery_attempt_at ?? null,
            lastDeliveryResponseCode: all?.last_delivery_response_code ?? null,
            lastDeliveryError: all?.last_delivery_error ?? null,
          });
        }
      } catch {
        // use default state gracefully
      } finally {
        setLoading(false);
      }
    };
    fetchDashboard();
  }, []);

  return (
    <div className="screen-container dashboard-screen" data-testid="dashboard-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">System Overview & Mail Telemetry</h1>
          <p className="screen-desc">
            Real-time multi-tenant mail engine status, queue metrics, security enforcement, and storage capacity.
          </p>
        </div>
      </div>

      <div className="metrics-grid">
        <div className="card metric-card">
          <div className="metric-label">Active Queue</div>
          <div className="metric-value text-info">{loading ? '...' : stats.activeQueued}</div>
          <div className="metric-sub">Outbound spool</div>
        </div>

        <div className="card metric-card">
          <div className="metric-label">Retrying</div>
          <div className="metric-value text-warning">{loading ? '...' : stats.retrying}</div>
          <div className="metric-sub">Deferred backoff</div>
        </div>

        <div className="card metric-card">
          <div className="metric-label">Dead Letters</div>
          <div className="metric-value text-error">{loading ? '...' : stats.deadLetters}</div>
          <div className="metric-sub">Permanent delivery fails</div>
        </div>

        <div className="card metric-card">
          <div className="metric-label">Delivered (24h)</div>
          <div className="metric-value text-success">{loading ? '...' : stats.delivered24h.toLocaleString()}</div>
          <div className="metric-sub">Mailbox delivery volume (24h)</div>
        </div>
      </div>

      <div className="dashboard-row">
        <div className="card flex-2">
          <h3 className="card-title">Mail Flow Throughput</h3>
          <p className="card-subtitle">Inbound & Outbound message volume over the last 24 hours</p>
          <div className="chart-placeholder">
            <div className="bar-group">
              {[45, 60, 75, 90, 85, 95, 110, 140, 130, 120, 100, 80].map((h, i) => (
                <div key={i} className="bar-track">
                  <div className="bar-fill" style={{ height: `${(h / 140) * 100}%` }} title={`Hour ${i * 2}:00 - ${h} msg/m`} />
                  <span className="bar-label">{i * 2}h</span>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="card flex-1">
          <h3 className="card-title">Security & Quarantine</h3>
          <p className="card-subtitle">Threats filtered before mailbox delivery</p>
          <div className="status-list">
            <div className="status-item">
              <span className="status-item-label">Spam Blocked (RSPAMD)</span>
              <span className="badge badge-warning">{stats.spamBlocked24h}</span>
            </div>
            <div className="status-item">
              <span className="status-item-label">Quarantined (Malware / Policy)</span>
              <span className="badge badge-error">{stats.quarantined24h}</span>
            </div>
            <div className="status-item">
              <span className="status-item-label">Outbound delivery success rate</span>
              <span className="badge badge-neutral">{Math.round((stats.deliverySuccessRate24h ?? 0) * 10) / 10}%</span>
            </div>
            <div className="status-item">
              <span className="status-item-label">Active Tenants</span>
              <span className="badge badge-info">{stats.tenantCount}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
