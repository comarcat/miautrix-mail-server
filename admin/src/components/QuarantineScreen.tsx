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
  const today = new Date().toISOString().slice(0, 10);
  const [fromDate, setFromDate] = useState<string>(today);
  const [toDate, setToDate] = useState<string>(today);
  const [selectedItem, setSelectedItem] = useState<QuarantineItem | null>(null);

  const fetchQuarantine = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const from = fromDate ? new Date(`${fromDate}T00:00:01`).toISOString() : undefined;
      const to = toDate ? new Date(`${toDate}T23:59:59`).toISOString() : undefined;

      const res: ApiResponse<QuarantineItem[]> = await client.getQuarantine({
        status: statusFilter !== 'all' ? statusFilter : undefined,
        search: search.trim() || undefined,
        from,
        to,
      });
      setItems(res.data || []);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch quarantine records');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [client, statusFilter, search, fromDate, toDate]);

  useEffect(() => {
    fetchQuarantine();
  }, [fetchQuarantine]);

  const handleRelease = async (item: QuarantineItem) => {
    try {
      const res = await client.releaseQuarantine(item.id);
      setActionMessage(res.message || `Message from ${item.sender} released to recipient.`);
      setSelectedItem(null);

      // If the user was filtering to "Quarantined", releasing removes the row from that view.
      // Jump back to "All Items" so they can see the updated lifecycle.
      setStatusFilter('all');
      // Keep modal open for visibility in the current list.


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

      // The item now becomes `Discarded` (not removed), so route user to that view.
      setStatusFilter('discarded');

      fetchQuarantine();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Delete failed');
    }
  };

  const handleDeliverAndDelete = async (item: QuarantineItem) => {
    if (!window.confirm(`Deliver message from ${item.sender} and remove it from quarantine?`)) return;

    try {
      const res = await client.deliverAndDeleteQuarantine(item.id);
      setActionMessage(res.message || 'Message released and removed from quarantine.');
      setSelectedItem(null);
      fetchQuarantine();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Deliver and remove failed');
    }
  };

  const senderDomain = (sender: string): string | null => {
    const at = sender.lastIndexOf('@');
    if (at < 0 || at === sender.length - 1) return null;
    return sender.slice(at + 1).replace(/[<>]/g, '').trim().toLowerCase();
  };

  const handleBlockSenderDomain = async (item: QuarantineItem) => {
    const domain = senderDomain(item.sender);
    if (!domain) {
      setError('Sender does not contain a valid domain to block.');
      return;
    }

    if (!window.confirm(`Block all future mail from sender domain ${domain}?`)) return;

    try {
      const res = await client.blockQuarantineSenderDomain(item.id);
      setActionMessage(`Sender domain blocked with rule: ${res.data.name}`);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Block sender domain failed');
    }
  };

  const parseReasons = (jsonStr: string): string[] => {
    const formatReason = (reason: unknown): string => {
      if (typeof reason === 'string') return reason;
      if (reason && typeof reason === 'object') {
        const record = reason as Record<string, unknown>;
        const name = record.RuleName ?? record.ruleName ?? record.rule_name ?? record.name ?? 'Rule';
        const score = record.Score ?? record.score;
        const detail = record.Reason ?? record.reason;
        return [name, score !== undefined ? `+${score}` : undefined, detail]
          .filter((part) => part !== undefined && part !== null && String(part).trim() !== '')
          .map(String)
          .join(': ');
      }
      return String(reason);
    };

    try {
      const parsed = JSON.parse(jsonStr);
      if (Array.isArray(parsed)) return parsed.map(formatReason);
      if (parsed && typeof parsed === 'object') return Object.entries(parsed).map(([key, value]) => {
        if (typeof value === 'string') return `${key}: ${value}`;
        if (typeof value === 'number') return `${key}: +${value}`;
        if (value && typeof value === 'object') return formatReason({ RuleName: key, ...(value as Record<string, unknown>) });
        return key;
      });
      return [formatReason(parsed)];
    } catch {
      return jsonStr ? [jsonStr] : ['SPAM_THRESHOLD_EXCEEDED'];
    }
  };

  const renderReason = (reason: string, index: number) => {
    const [name, score, ...detailParts] = reason.split(': ');
    const detail = detailParts.join(': ');

    return (
      <div key={index} className="badge badge-neutral" style={{ display: 'block', whiteSpace: 'normal', lineHeight: 1.35, padding: '6px 8px' }}>
        <div style={{ fontWeight: 700 }}>{name}</div>
        {(score || detail) && (
          <div className="cell-dim" style={{ fontSize: '10px', marginTop: '2px' }}>
            {[score, detail].filter(Boolean).join(' · ')}
          </div>
        )}
      </div>
    );
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
        <div className="filter-context" style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap', width: '100%' }}>
          <span className="cell-dim" style={{ fontSize: '12px', fontWeight: 700, textTransform: 'uppercase' }}>Domain</span>
          <span className="badge badge-neutral">{domainFilter && domainFilter !== '*' ? domainFilter : 'All Domains'}</span>
          <label className="tenant-picker" style={{ marginLeft: 'auto' }}>
            <span className="tenant-picker-label">From</span>
            <input
              type="date"
              className="input-select input-select-sm"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              aria-label="Quarantine from date"
              data-testid="quarantine-from-date"
            />
          </label>
          <label className="tenant-picker">
            <span className="tenant-picker-label">To</span>
            <input
              type="date"
              className="input-select input-select-sm"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
              aria-label="Quarantine to date"
              data-testid="quarantine-to-date"
            />
          </label>
        </div>

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
                  <th style={{ minWidth: '300px', width: '28%' }}>Detection Reasons</th>
                  <th>Quarantined At</th>
                  <th className="quarantine-actions-col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {visibleItems.map((item) => {
                  const reasons = parseReasons(item.reasons_json);
                  const isHighRisk = item.spam_score >= 12.0;
                  const statusLower = item.status.toLowerCase();

                  return (
                    <tr
                      key={item.id}
                      data-testid={`quarantine-row-${item.id}`}
                      onClick={() => setSelectedItem(item)}
                      style={{ cursor: 'pointer' }}
                    >
                      <td>
                        <span className={`badge ${statusLower === 'released' ? 'badge-success' : statusLower === 'discarded' ? 'badge-neutral' : 'badge-warning'}`}>
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
                        <div style={{ display: 'grid', gap: '6px', minWidth: '280px' }}>
                          {reasons.slice(0, 3).map(renderReason)}
                          {reasons.length > 3 && (
                            <span className="badge badge-neutral" style={{ fontSize: '10px', width: 'fit-content' }}>
                              +{reasons.length - 3} more
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="cell-dim">{new Date(item.quarantined_at).toLocaleString()}</td>
                      <td className="quarantine-actions-cell">
                        <div className="action-buttons quarantine-row-actions">
                          {statusLower === 'quarantined' && (
                            <>
                              <button
                                className="btn-link"
                                onClick={(e) => { e.stopPropagation(); setSelectedItem(item); }}
                                title="Inspect message metadata"
                              >
                                Inspect
                              </button>
                              <button
                                className="btn-link text-success"
                                onClick={(e) => { e.stopPropagation(); handleRelease(item); }}
                              >
                                Release
                              </button>
                              <button
                                className="btn-link text-error"
                                onClick={(e) => { e.stopPropagation(); handleDelete(item); }}
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
                <span className={`badge ${selectedItem.status.toLowerCase() === 'released' ? 'badge-success' : 'badge-warning'}`}>
                  {selectedItem.status}
                </span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Sender</span>
                <span className="detail-value cell-mono">{selectedItem.sender}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Recipient</span>
                <span className="detail-value cell-mono">{selectedItem.recipient}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Subject / Recipient</span>
                <span className="detail-value">{selectedItem.subject || '(None)'}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Spam Score</span>
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
                <div className="detail-label">Detection Reasons</div>
                <div style={{ display: 'grid', gap: '6px' }}>
                  {parseReasons(selectedItem.reasons_json).map(renderReason)}
                </div>
              </div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn btn-secondary" onClick={() => setSelectedItem(null)}>
                Close
              </button>
              {selectedItem.status.toLowerCase() === 'quarantined' && (
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', width: '100%' }}>
                  <button
                    type="button"
                    className="btn btn-secondary text-error"
                    onClick={() => handleBlockSenderDomain(selectedItem)}
                  >
                    Block Sender Domain
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary text-error"
                    onClick={() => handleDelete(selectedItem)}
                  >
                    Discard Message
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary text-success"
                    onClick={() => handleDeliverAndDelete(selectedItem)}
                  >
                    False Positive: Deliver & Remove
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary"
                    onClick={() => handleRelease(selectedItem)}
                  >
                    Release to Inbox
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
