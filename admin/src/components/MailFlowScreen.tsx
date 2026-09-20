import React, { useState, useEffect, useCallback } from 'react';
import { MailFlowRuleItem, ApiResponse } from '../types';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';

interface MailFlowScreenProps {
  client?: AdminApiClient;
  onOpenDesigner?: () => void;
}

export const MailFlowScreen: React.FC<MailFlowScreenProps> = ({
  client = defaultClient,
  onOpenDesigner,
}) => {
  const [rules, setRules] = useState<MailFlowRuleItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const fetchRules = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res: ApiResponse<MailFlowRuleItem[]> = await client.getRules();
      setRules(res.data || []);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch mail flow rules');
    } finally {
      setLoading(false);
    }
  }, [client]);

  useEffect(() => {
    fetchRules();
  }, [fetchRules]);

  return (
    <div className="screen-container mailflow-screen" data-testid="mailflow-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Mail Flow Routing & Connectors</h1>
          <p className="screen-desc">
            Visualize message traversal across inbound listeners, milter scanners, policy filters, and outbound delivery relays.
          </p>
        </div>
        <div className="header-actions">
          {onOpenDesigner && (
            <button className="btn btn-primary" onClick={onOpenDesigner}>
              Open Rule Designer
            </button>
          )}
        </div>
      </div>

      {error && (
        <div className="alert alert-error" role="alert">
          <span>{error}</span>
          <button className="alert-close" onClick={() => setError(null)}>✕</button>
        </div>
      )}

      {/* Visual Pipeline Topology */}
      <div className="card">
        <h2 className="card-title">Message Processing Pipeline Topology</h2>
        <p className="card-subtitle">End-to-end stage traversal for inbound and outbound messages</p>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '12px', marginTop: '16px' }}>
          <div className="card" style={{ background: 'var(--page)', border: '1px solid var(--border-soft)', padding: '16px' }}>
            <span className="badge badge-info" style={{ marginBottom: '8px' }}>Stage 1</span>
            <div className="cell-emphasis" style={{ fontSize: '13px', marginBottom: '4px' }}>Inbound SMTP</div>
            <p className="cell-dim" style={{ fontSize: '11px' }}>Port 25/587, TLS negotiation, DNSBL IP checks.</p>
          </div>

          <div className="card" style={{ background: 'var(--page)', border: '1px solid var(--border-soft)', padding: '16px' }}>
            <span className="badge badge-warning" style={{ marginBottom: '8px' }}>Stage 2</span>
            <div className="cell-emphasis" style={{ fontSize: '13px', marginBottom: '4px' }}>Milter Engine</div>
            <p className="cell-dim" style={{ fontSize: '11px' }}>Rspamd spam score, ClamAV antivirus, SPF/DMARC verify.</p>
          </div>

          <div className="card" style={{ background: 'var(--page)', border: '1px solid var(--border-soft)', padding: '16px' }}>
            <span className="badge badge-info" style={{ marginBottom: '8px' }}>Stage 3</span>
            <div className="cell-emphasis" style={{ fontSize: '13px', marginBottom: '4px' }}>Mail Flow Rules</div>
            <p className="cell-dim" style={{ fontSize: '11px' }}>Custom tenant routing, disclaimer appending, rerouting.</p>
          </div>

          <div className="card" style={{ background: 'var(--page)', border: '1px solid var(--border-soft)', padding: '16px' }}>
            <span className="badge badge-success" style={{ marginBottom: '8px' }}>Stage 4</span>
            <div className="cell-emphasis" style={{ fontSize: '13px', marginBottom: '4px' }}>Delivery Store</div>
            <p className="cell-dim" style={{ fontSize: '11px' }}>Local mailbox deposit / Outbound queue dispatch.</p>
          </div>

          <div className="card" style={{ background: 'var(--page)', border: '1px solid var(--border-soft)', padding: '16px' }}>
            <span className="badge badge-info" style={{ marginBottom: '8px' }}>Stage 5</span>
            <div className="cell-emphasis" style={{ fontSize: '13px', marginBottom: '4px' }}>Outbound DKIM & MX</div>
            <p className="cell-dim" style={{ fontSize: '11px' }}>Cryptographic signing, MX lookup, DANE TLS delivery.</p>
          </div>
        </div>
      </div>

      {/* Mail Flow Rules Table */}
      <div className="card table-card">
        <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border-soft)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <h2 className="card-title" style={{ margin: 0 }}>Active Mail Flow Rules</h2>
            <p className="card-subtitle" style={{ margin: 0 }}>Evaluated in order of priority from lowest to highest</p>
          </div>
        </div>

        {loading ? (
          <div className="loading-state">
            <div className="spinner" />
            <p>Loading rules...</p>
          </div>
        ) : rules.length === 0 ? (
          <div className="empty-state">
            <p>No custom mail flow rules configured. Default delivery rules active.</p>
          </div>
        ) : (
          <div className="table-responsive">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Priority</th>
                  <th>Rule Name</th>
                  <th>Status</th>
                  <th>Conditions</th>
                  <th>Actions</th>
                  <th>Created</th>
                </tr>
              </thead>
              <tbody>
                {rules.map((rule) => (
                  <tr key={rule.id}>
                    <td>
                      <span className="attempt-counter">{rule.priority}</span>
                    </td>
                    <td className="cell-emphasis">{rule.name}</td>
                    <td>
                      <span className={`badge ${rule.is_enabled ? 'badge-success' : 'badge-neutral'}`}>
                        {rule.is_enabled ? 'Enabled' : 'Disabled'}
                      </span>
                    </td>
                    <td>
                      <span className="cell-mono cell-dim" style={{ fontSize: '11px' }}>
                        {rule.conditions_json ? rule.conditions_json.slice(0, 40) + '...' : 'All messages'}
                      </span>
                    </td>
                    <td>
                      <span className="cell-mono cell-dim" style={{ fontSize: '11px' }}>
                        {rule.actions_json ? rule.actions_json.slice(0, 40) + '...' : 'Deliver'}
                      </span>
                    </td>
                    <td className="cell-dim">{new Date(rule.created_at).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
