import React, { useState, useEffect, useCallback } from 'react';
import { AuditLogItem, ApiResponse } from '../types';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';

interface LogsScreenProps {
  client?: AdminApiClient;
}

export const LogsScreen: React.FC<LogsScreenProps> = ({ client = defaultClient }) => {
  const [logs, setLogs] = useState<AuditLogItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [actionFilter, setActionFilter] = useState<string>('all');
  const [search, setSearch] = useState<string>('');
  const [selectedLog, setSelectedLog] = useState<AuditLogItem | null>(null);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res: ApiResponse<AuditLogItem[]> = await client.getAuditLogs({
        action: actionFilter !== 'all' ? actionFilter : undefined,
        search: search.trim() || undefined,
      });
      setLogs(res.data || []);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch audit logs');
      setLogs([]);
    } finally {
      setLoading(false);
    }
  }, [client, actionFilter, search]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const getActionBadgeClass = (action: string) => {
    const act = action.toLowerCase();
    if (act.includes('delete') || act.includes('fail') || act.includes('block')) return 'badge-error';
    if (act.includes('create') || act.includes('provision') || act.includes('verified') || act.includes('login')) return 'badge-success';
    if (act.includes('update') || act.includes('modify') || act.includes('retry')) return 'badge-warning';
    return 'badge-info';
  };

  const formatJson = (jsonStr?: string) => {
    if (!jsonStr) return '{}';
    try {
      return JSON.stringify(JSON.parse(jsonStr), null, 2);
    } catch {
      return jsonStr;
    }
  };

  return (
    <div className="screen-container logs-screen" data-testid="logs-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Audit & Observability Logs</h1>
          <p className="screen-desc">
            Immutable audit trail of administrative actions, authentication attempts, configuration changes, and system events.
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-secondary" onClick={fetchLogs} title="Refresh audit log stream">
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

      <div className="card filter-bar">
        <div className="chip-group" role="tablist" aria-label="Action filter">
          {['all', 'auth.login', 'user.create', 'domain.verify', 'mailbox.update', 'quarantine.release', 'system.backup'].map((act) => (
            <button
              key={act}
              role="tab"
              aria-selected={actionFilter === act}
              className={`chip ${actionFilter === act ? 'chip-active' : ''}`}
              onClick={() => setActionFilter(act)}
              data-testid={`filter-log-${act}`}
            >
              {act === 'all' ? 'All Events' : act}
            </button>
          ))}
        </div>

        <div className="search-form">
          <div className="input-search-wrapper">
            <svg className="search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              type="search"
              className="input-search"
              placeholder="Search actor, target, IP, or details..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search logs"
            />
          </div>
        </div>
      </div>

      <div className="card table-card">
        {loading ? (
          <div className="loading-state" data-testid="logs-loading">
            <div className="spinner" />
            <p>Loading audit trail...</p>
          </div>
        ) : logs.length === 0 ? (
          <div className="empty-state" data-testid="logs-empty">
            <p>No audit events found matching criteria.</p>
          </div>
        ) : (
          <div className="table-responsive">
            <table className="data-table" data-testid="logs-table">
              <thead>
                <tr>
                  <th>Timestamp</th>
                  <th>Action</th>
                  <th>Actor</th>
                  <th>Target Type</th>
                  <th>Target ID</th>
                  <th>Source IP</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {logs.map((log) => (
                  <tr key={log.id}>
                    <td className="cell-dim cell-mono" style={{ fontSize: '11px', whiteSpace: 'nowrap' }}>
                      {new Date(log.created_at).toLocaleString()}
                    </td>
                    <td>
                      <span className={`badge ${getActionBadgeClass(log.action)}`}>
                        {log.action}
                      </span>
                    </td>
                    <td>
                      <span className="cell-emphasis cell-mono" style={{ fontSize: '12px' }}>
                        {log.actor_email || log.actor_id || 'system'}
                      </span>
                    </td>
                    <td>
                      <span className="cell-dim">{log.target_type}</span>
                    </td>
                    <td>
                      <span className="cell-mono cell-dim" style={{ fontSize: '11px' }}>
                        {log.target_id ? log.target_id.slice(0, 8) + '...' : '—'}
                      </span>
                    </td>
                    <td>
                      <span className="cell-mono cell-dim" style={{ fontSize: '11px' }}>
                        {log.ip_address || '127.0.0.1'}
                      </span>
                    </td>
                    <td>
                      <button
                        className="btn-link"
                        onClick={() => setSelectedLog(log)}
                        title="View payload details"
                      >
                        Inspect
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Log Details Modal */}
      {selectedLog && (
        <div className="modal-backdrop" data-testid="log-inspect-modal">
          <div className="modal-card" style={{ maxWidth: '640px' }}>
            <div className="modal-header">
              <h2 className="modal-title">Audit Event: {selectedLog.action}</h2>
              <button className="btn-close" onClick={() => setSelectedLog(null)}>✕</button>
            </div>
            <div className="modal-body">
              <div className="detail-row">
                <span className="detail-label">Event ID</span>
                <span className="detail-value cell-mono">{selectedLog.id}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Timestamp</span>
                <span className="detail-value">{new Date(selectedLog.created_at).toISOString()}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Actor</span>
                <span className="detail-value cell-mono">{selectedLog.actor_email || selectedLog.actor_id || 'System Daemon'}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Target</span>
                <span className="detail-value cell-mono">{selectedLog.target_type} {selectedLog.target_id && `(${selectedLog.target_id})`}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Client IP</span>
                <span className="detail-value cell-mono">{selectedLog.ip_address || 'Internal Transport'}</span>
              </div>

              <div className="detail-row-block">
                <div className="detail-label">Structured Event Metadata</div>
                <pre className="detail-pre" style={{ color: 'var(--text)', maxHeight: '220px' }}>
                  {formatJson(selectedLog.details_json)}
                </pre>
              </div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn btn-primary" onClick={() => setSelectedLog(null)}>
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
