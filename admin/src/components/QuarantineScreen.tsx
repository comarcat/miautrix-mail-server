import React, { useState, useEffect, useCallback } from 'react';
import { QuarantineItem, ApiResponse } from '../types';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';
import { matchesDomain } from '../utils/domainFilter';

interface QuarantineScreenProps {
  client?: AdminApiClient;
  domainFilter?: string;
}

export const QuarantineScreen: React.FC<QuarantineScreenProps> = ({ client = defaultClient, domainFilter }) => {
  const [items, setItems] = useState<QuarantineItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [search, setSearch] = useState<string>('');
  const [selectedItem, setSelectedItem] = useState<QuarantineItem | null>(null);

  const fetchQuarantine = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res: ApiResponse<QuarantineItem[]> = await client.getQuarantine({
        status: statusFilter !== 'all' ? statusFilter : undefined,
        search: search.trim() || undefined,
      });
      setItems(res.data || []);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch quarantine records');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [client, statusFilter, search]);

  useEffect(() => {
    fetchQuarantine();
  }, [fetchQuarantine]);

  const handleRelease = async (item: QuarantineItem) => {
    try {
      const res = await client.releaseQuarantine(item.id);
      setActionMessage(res.message || `Message from ${item.sender} released to recipient.`);
      setSelectedItem(null);
      fetchQuarantine();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Release failed');
    }
  };

  const handleDelete = async (item: QuarantineItem) => {
    if (!window.confirm(`Permanently discard quarantined message from ${item.sender}?`)) return;

    try {
      await client.deleteQuarantine(item.id);
      setActionMessage('Quarantine item deleted.');
      setSelectedItem(null);
      fetchQuarantine();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Delete failed');
    }
  };

  const parseReasons = (jsonStr: string): string[] => {
    try {
      const parsed = JSON.parse(jsonStr);
      if (Array.isArray(parsed)) return parsed;
      if (typeof parsed === 'object') return Object.keys(parsed);
      return [String(parsed)];
    } catch {
      return jsonStr ? [jsonStr] : ['SPAM_THRESHOLD_EXCEEDED'];
    }
  };

  const visibleItems = items.filter((i) => matchesDomain(domainFilter, i.recipient, i.sender));

  return (
    <div className="screen-container quarantine-screen" data-testid="quarantine-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Quarantine & Threat Inspection</h1>
          <p className="screen-desc">
            Review, release, or discard suspicious messages flagged by Rspamd, ClamAV, and custom mail flow policies.
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-secondary" onClick={fetchQuarantine} title="Refresh quarantine">
            Refresh
          </button>
        </div>
      </div>

      {actionMessage && (
        <div className="alert alert-info" role="status">
          <span>{actionMessage}</span>
          <button className="alert-close" onClick={() => setActionMessage(null)}>✕</button>
        </div>
      )}

      {error && (
        <div className="alert alert-error" role="alert">
          <span>{error}</span>
          <button className="alert-close" onClick={() => setError(null)}>✕</button>
        </div>
      )}

      <div className="card filter-bar">
        <div className="chip-group" role="tablist" aria-label="Quarantine status filter">
          {['all', 'quarantined', 'released', 'discarded'].map((status) => (
            <button
              key={status}
              role="tab"
              aria-selected={statusFilter === status}
              className={`chip ${statusFilter === status ? 'chip-active' : ''}`}
              onClick={() => setStatusFilter(status)}
              data-testid={`filter-quarantine-${status}`}
            >
              {status === 'all' ? 'All Items' : status.charAt(0).toUpperCase() + status.slice(1)}
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
              placeholder="Search sender, recipient, or subject..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search quarantine"
            />
          </div>
        </div>
      </div>

      <div className="card table-card">
        {loading ? (
          <div className="loading-state" data-testid="quarantine-loading">
            <div className="spinner" />
            <p>Loading quarantined items...</p>
          </div>
        ) : visibleItems.length === 0 ? (
          <div className="empty-state" data-testid="quarantine-empty">
            <p>No messages found in quarantine matching criteria.</p>
          </div>
        ) : (
          <div className="table-responsive">
            <table className="data-table" data-testid="quarantine-table">
              <thead>
                <tr>
                  <th>Status</th>
                  <th>Spam Score</th>
                  <th>Subject / Recipient</th>
                  <th>Sender</th>
                  <th>Detection Reasons</th>
                  <th>Quarantined At</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {visibleItems.map((item) => {
                  const reasons = parseReasons(item.reasons_json);
                  const isHighRisk = item.spam_score >= 12.0;

                  return (
                    <tr key={item.id}>
                      <td>
                        <span className={`badge ${item.status === 'released' ? 'badge-success' : item.status === 'discarded' ? 'badge-neutral' : 'badge-warning'}`}>
                          {item.status}
                        </span>
                      </td>
                      <td>
                        <span className={`attempt-counter ${isHighRisk ? 'text-error' : 'text-warning'}`} style={{ fontWeight: 600 }}>
                          {item.spam_score.toFixed(1)} / {item.threshold.toFixed(1)}
                        </span>
                      </td>
                      <td>
                        <div className="cell-emphasis">{item.subject || '(No Subject)'}</div>
                        <div className="cell-mono cell-dim" style={{ fontSize: '11px' }}>To: {item.recipient}</div>
                      </td>
                      <td>
                        <span className="cell-mono cell-dim">{item.sender}</span>
                      </td>
                      <td>
                        <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap', maxWidth: '240px' }}>
                          {reasons.slice(0, 2).map((r, i) => (
                            <span key={i} className="badge badge-neutral" style={{ fontSize: '10px' }}>
                              {r}
                            </span>
                          ))}
                          {reasons.length > 2 && (
                            <span className="badge badge-neutral" style={{ fontSize: '10px' }}>
                              +{reasons.length - 2} more
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="cell-dim">{new Date(item.quarantined_at).toLocaleString()}</td>
                      <td>
                        <div className="action-buttons">
                          <button
                            className="btn-link"
                            onClick={() => setSelectedItem(item)}
                            title="Inspect message metadata"
                          >
                            Inspect
                          </button>
                          {item.status === 'quarantined' && (
                            <>
                              <button
                                className="btn-link text-success"
                                onClick={() => handleRelease(item)}
                              >
                                Release
                              </button>
                              <button
                                className="btn-link text-error"
                                onClick={() => handleDelete(item)}
                              >
                                Discard
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Message Inspection Modal */}
      {selectedItem && (
        <div className="modal-backdrop" data-testid="quarantine-inspect-modal">
          <div className="modal-card" style={{ maxWidth: '600px' }}>
            <div className="modal-header">
              <h2 className="modal-title">Quarantine Inspection: {selectedItem.subject || 'Message'}</h2>
              <button className="btn-close" onClick={() => setSelectedItem(null)}>✕</button>
            </div>
            <div className="modal-body">
              <div className="detail-row">
                <span className="detail-label">Status</span>
                <span className={`badge ${selectedItem.status === 'released' ? 'badge-success' : 'badge-warning'}`}>
                  {selectedItem.status}
                </span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Sender (Envelope)</span>
                <span className="detail-value cell-mono">{selectedItem.sender}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Recipient</span>
                <span className="detail-value cell-mono">{selectedItem.recipient}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Subject</span>
                <span className="detail-value">{selectedItem.subject || '(None)'}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Spam Score / Threshold</span>
                <span className="detail-value" style={{ fontWeight: 600 }}>
                  {selectedItem.spam_score.toFixed(2)} / {selectedItem.threshold.toFixed(2)}
                </span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Quarantined At</span>
                <span className="detail-value">{new Date(selectedItem.quarantined_at).toLocaleString()}</span>
              </div>
              {selectedItem.released_at && (
                <div className="detail-row">
                  <span className="detail-label">Released At</span>
                  <span className="detail-value">{new Date(selectedItem.released_at).toLocaleString()}</span>
                </div>
              )}

              <div className="detail-row-block">
                <div className="detail-label">Triggered Detection Symbols</div>
                <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                  {parseReasons(selectedItem.reasons_json).map((r, i) => (
                    <span key={i} className="badge badge-warning" style={{ fontSize: '11px', padding: '2px 8px' }}>
                      {r}
                    </span>
                  ))}
                </div>
              </div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn btn-secondary" onClick={() => setSelectedItem(null)}>
                Close
              </button>
              {selectedItem.status === 'quarantined' && (
                <>
                  <button
                    type="button"
                    className="btn btn-secondary text-error"
                    onClick={() => handleDelete(selectedItem)}
                  >
                    Discard Message
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary"
                    onClick={() => handleRelease(selectedItem)}
                  >
                    Release to Inbox
                  </button>
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
