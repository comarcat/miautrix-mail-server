import React, { useState, useEffect, useCallback } from 'react';
import { QueueItem, ApiResponse, QueueQueryParams } from '../types';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';
import { matchesDomain } from '../utils/domainFilter';

interface QueueScreenProps {
  client?: AdminApiClient;
  pageSize?: number;
  domainFilter?: string;
}

export const QueueScreen: React.FC<QueueScreenProps> = ({
  client = defaultClient,
  pageSize = 20,
  domainFilter,
}) => {
  const [items, setItems] = useState<QueueItem[]>([]);
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [searchInput, setSearchInput] = useState<string>('');
  const [activeSearch, setActiveSearch] = useState<string>('');
  const [currentCursor, setCurrentCursor] = useState<string | null>(null);
  const [cursorHistory, setCursorHistory] = useState<string[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [hasMore, setHasMore] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedItem, setSelectedItem] = useState<QueueItem | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);

  const fetchQueuePage = useCallback(
    async (cursor: string | null, status: string, search: string) => {
      setLoading(true);
      setError(null);
      try {
        const params: QueueQueryParams = {
          limit: pageSize,
          cursor: cursor || undefined,
          status: status !== 'all' ? status : undefined,
          search: search.trim() || undefined,
        };
        const response: ApiResponse<QueueItem[]> = await client.getQueue(params);
        setItems(response.data || []);
        setNextCursor(response.meta?.next_cursor || null);
        setHasMore(response.meta?.has_more ?? false);
      } catch (err: unknown) {
        setError(err instanceof Error ? err.message : 'Unknown error fetching queue');
        setItems([]);
      } finally {
        setLoading(false);
      }
    },
    [client, pageSize]
  );

  useEffect(() => {
    fetchQueuePage(currentCursor, statusFilter, activeSearch);
  }, [fetchQueuePage, currentCursor, statusFilter, activeSearch]);

  const handleStatusChange = (status: string) => {
    setStatusFilter(status);
    setCurrentCursor(null);
    setCursorHistory([]);
  };

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setActiveSearch(searchInput);
    setCurrentCursor(null);
    setCursorHistory([]);
  };

  const handleNextPage = () => {
    if (nextCursor) {
      setCursorHistory((prev) => [...prev, currentCursor || '']);
      setCurrentCursor(nextCursor);
    }
  };

  const handlePreviousPage = () => {
    if (cursorHistory.length > 0) {
      const prev = [...cursorHistory];
      const prevCursor = prev.pop() || null;
      setCursorHistory(prev);
      setCurrentCursor(prevCursor === '' ? null : prevCursor);
    }
  };

  const handleRetry = async (id: string) => {
    try {
      const res = await client.retryQueueItem(id, 'Manual retry from Admin UI');
      setActionMessage(res.message || 'Queued for immediate retry');
      fetchQueuePage(currentCursor, statusFilter, activeSearch);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Retry failed');
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Are you sure you want to discard this message from queue?')) return;
    try {
      await client.deleteQueueItem(id);
      setActionMessage('Message removed from queue');
      fetchQueuePage(currentCursor, statusFilter, activeSearch);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Delete failed');
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  };

  const getStatusBadgeClass = (status: string) => {
    switch (status) {
      case 'queued':
        return 'badge-info';
      case 'retrying':
        return 'badge-warning';
      case 'dead_letter':
        return 'badge-error';
      case 'delivered':
        return 'badge-success';
      default:
        return 'badge-neutral';
    }
  };

  const visibleItems = items.filter((i) => matchesDomain(domainFilter, i.recipient, i.sender));

  return (
    <div className="screen-container queue-screen" data-testid="queue-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Outbound Mail Queue</h1>
          <p className="screen-desc">
            Monitor and control deferred, retrying, and dead-letter message delivery across all tenant domains.
          </p>
        </div>
        <div className="header-actions">
          <button
            className="btn btn-secondary"
            onClick={() => fetchQueuePage(currentCursor, statusFilter, activeSearch)}
            title="Refresh queue view"
          >
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
        <div className="chip-group" role="tablist" aria-label="Queue status filter">
          {['all', 'queued', 'retrying', 'dead_letter', 'delivered'].map((status) => (
            <button
              key={status}
              role="tab"
              aria-selected={statusFilter === status}
              className={`chip ${statusFilter === status ? 'chip-active' : ''}`}
              onClick={() => handleStatusChange(status)}
              data-testid={`filter-${status}`}
            >
              {status === 'all'
                ? 'All Messages'
                : status.replace('_', ' ').replace(/\b\w/g, (c) => c.toUpperCase())}
            </button>
          ))}
        </div>

        <form onSubmit={handleSearchSubmit} className="search-form">
          <div className="input-search-wrapper">
            <svg className="search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              type="search"
              className="input-search"
              placeholder="Search sender, recipient, or msg-id..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              aria-label="Search queue items"
            />
          </div>
        </form>
      </div>

      <div className="card table-card">
        {loading ? (
          <div className="loading-state" data-testid="queue-loading">
            <div className="spinner" />
            <p>Loading queue messages...</p>
          </div>
        ) : visibleItems.length === 0 ? (
          <div className="empty-state" data-testid="queue-empty">
            <p>No messages found in queue matching criteria.</p>
          </div>
        ) : (
          <div className="table-responsive">
            <table className="data-table" data-testid="queue-table">
              <thead>
                <tr>
                  <th>Status</th>
                  <th>Recipient</th>
                  <th>Sender</th>
                  <th>Attempts</th>
                  <th>Next Retry</th>
                  <th>Size</th>
                  <th>Created</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {visibleItems.map((item) => (
                  <tr key={item.id} className="table-row">
                    <td>
                      <span className={`badge ${getStatusBadgeClass(item.status)}`}>
                        {item.status.replace('_', ' ')}
                      </span>
                    </td>
                    <td className="cell-emphasis cell-mono">{item.recipient}</td>
                    <td className="cell-dim cell-mono">{item.sender}</td>
                    <td>
                      <span className="attempt-counter">{item.attempts}</span>
                    </td>
                    <td className="cell-dim">
                      {item.next_retry_at ? new Date(item.next_retry_at).toLocaleTimeString() : '—'}
                    </td>
                    <td className="cell-dim cell-mono">{formatBytes(item.size_bytes)}</td>
                    <td className="cell-dim">{new Date(item.created_at).toLocaleTimeString()}</td>
                    <td>
                      <div className="action-buttons">
                        <button
                          className="btn-link"
                          onClick={() => setSelectedItem(item)}
                          title="View diagnostic details"
                        >
                          Details
                        </button>
                        {item.status !== 'delivered' && (
                          <button
                            className="btn-link text-info"
                            onClick={() => handleRetry(item.id)}
                            title="Retry now"
                          >
                            Retry
                          </button>
                        )}
                        <button
                          className="btn-link text-error"
                          onClick={() => handleDelete(item.id)}
                          title="Discard message"
                        >
                          Discard
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="pagination-bar" data-testid="pagination-bar">
          <div className="pagination-info">
            <span>
              Showing {visibleItems.length} items {currentCursor ? '(cursor paged)' : '(page 1)'}
            </span>
          </div>
          <div className="pagination-controls">
            <button
              className="btn btn-secondary btn-sm"
              disabled={cursorHistory.length === 0 || loading}
              onClick={handlePreviousPage}
              data-testid="pagination-prev"
            >
              Previous
            </button>
            <button
              className="btn btn-secondary btn-sm"
              disabled={!hasMore || loading}
              onClick={handleNextPage}
              data-testid="pagination-next"
            >
              Next
            </button>
          </div>
        </div>
      </div>

      {selectedItem && (
        <div className="modal-backdrop" onClick={() => setSelectedItem(null)}>
          <div className="modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3 className="modal-title">Queue Message Details</h3>
              <button className="btn-close" onClick={() => setSelectedItem(null)}>✕</button>
            </div>
            <div className="modal-body">
              <div className="detail-row">
                <span className="detail-label">Queue ID:</span>
                <span className="detail-value cell-mono">{selectedItem.id}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Message ID:</span>
                <span className="detail-value cell-mono">{selectedItem.message_id}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Sender:</span>
                <span className="detail-value cell-mono">{selectedItem.sender}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Recipient:</span>
                <span className="detail-value cell-mono">{selectedItem.recipient}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Status:</span>
                <span className={`badge ${getStatusBadgeClass(selectedItem.status)}`}>
                  {selectedItem.status}
                </span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Attempts:</span>
                <span className="detail-value">{selectedItem.attempts}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Size:</span>
                <span className="detail-value">{formatBytes(selectedItem.size_bytes)}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Created At:</span>
                <span className="detail-value">{new Date(selectedItem.created_at).toLocaleString()}</span>
              </div>
              {selectedItem.error_message && (
                <div className="detail-row-block">
                  <span className="detail-label">Last Error Diagnostic:</span>
                  <pre className="detail-pre">{selectedItem.error_message}</pre>
                </div>
              )}
            </div>
            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setSelectedItem(null)}>
                Close
              </button>
              {selectedItem.status !== 'delivered' && (
                <button
                  className="btn btn-primary"
                  onClick={() => {
                    handleRetry(selectedItem.id);
                    setSelectedItem(null);
                  }}
                >
                  Retry Now
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
