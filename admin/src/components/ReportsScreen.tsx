import React, { useState } from 'react';

export const ReportsScreen: React.FC = () => {
  const [timeframe, setTimeframe] = useState<string>('7d');
  const [exportMessage, setExportMessage] = useState<string | null>(null);

  const handleExport = (format: 'csv' | 'json') => {
    setExportMessage(`Generated and downloaded mail_flow_report_${timeframe}.${format}`);
    setTimeout(() => setExportMessage(null), 4000);
  };

  return (
    <div className="screen-container reports-screen" data-testid="reports-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Traffic Analytics & Delivery Reports</h1>
          <p className="screen-desc">
            Analyze tenant message volume, delivery latency percentiles, bounce classifications, and security filtering trends.
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-secondary" onClick={() => handleExport('csv')}>
            Export CSV
          </button>
          <button className="btn btn-primary" onClick={() => handleExport('json')}>
            Export JSON
          </button>
        </div>
      </div>

      {exportMessage && (
        <div className="alert alert-info" role="status">
          <span>{exportMessage}</span>
          <button className="alert-close" onClick={() => setExportMessage(null)}>✕</button>
        </div>
      )}

      <div className="card filter-bar">
        <div className="chip-group" role="tablist" aria-label="Timeframe selector">
          {[
            { id: '24h', label: 'Last 24 Hours' },
            { id: '7d', label: 'Last 7 Days' },
            { id: '30d', label: 'Last 30 Days' },
            { id: '90d', label: 'Last 90 Days' },
          ].map((tf) => (
            <button
              key={tf.id}
              role="tab"
              aria-selected={timeframe === tf.id}
              className={`chip ${timeframe === tf.id ? 'chip-active' : ''}`}
              onClick={() => setTimeframe(tf.id)}
            >
              {tf.label}
            </button>
          ))}
        </div>
      </div>

      <div className="metrics-grid">
        <div className="card metric-card">
          <span className="metric-label">Total Messages Processed</span>
          <span className="metric-value" style={{ fontSize: '24px' }}>
            {timeframe === '24h' ? '12,450' : timeframe === '7d' ? '86,210' : '362,800'}
          </span>
          <span className="metric-sub">Inbound + Outbound</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">Successful Deliveries</span>
          <span className="metric-value text-success" style={{ fontSize: '24px' }}>
            {timeframe === '24h' ? '98.9%' : '99.1%'}
          </span>
          <span className="metric-sub">&lt; 1.2s median delivery latency</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">Spam Blocked / Filtered</span>
          <span className="metric-value text-warning" style={{ fontSize: '24px' }}>
            {timeframe === '24h' ? '1,840' : timeframe === '7d' ? '12,650' : '54,200'}
          </span>
          <span className="metric-sub">Heuristics & DNSBL feeds</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">Hard Bounce / Rejected</span>
          <span className="metric-value text-error" style={{ fontSize: '24px' }}>
            {timeframe === '24h' ? '42' : timeframe === '7d' ? '310' : '1,280'}
          </span>
          <span className="metric-sub">Invalid recipient / SPF failures</span>
        </div>
      </div>

      <div className="dashboard-row">
        {/* Visual Volume Distribution */}
        <div className="card flex-2">
          <h2 className="card-title">Daily Delivery Throughput</h2>
          <p className="card-subtitle">Volume distribution across successful, retrying, and blocked messages</p>

          <div className="chart-placeholder">
            <div className="bar-group">
              {[
                { day: 'Mon', h: '65%' },
                { day: 'Tue', h: '85%' },
                { day: 'Wed', h: '92%' },
                { day: 'Thu', h: '78%' },
                { day: 'Fri', h: '88%' },
                { day: 'Sat', h: '42%' },
                { day: 'Sun', h: '35%' },
              ].map((bar, idx) => (
                <div key={idx} className="bar-track">
                  <div className="bar-fill" style={{ height: bar.h }} />
                  <span className="bar-label">{bar.day}</span>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Security Summary Breakdown */}
        <div className="card flex-1">
          <h2 className="card-title">Security & Quarantine Breakdown</h2>
          <p className="card-subtitle">Classification of defensive interventions</p>

          <div className="status-list" style={{ marginTop: '12px' }}>
            <div className="status-item">
              <span className="status-item-label">Clean Inbound Delivery</span>
              <span className="cell-emphasis cell-mono">82.4%</span>
            </div>
            <div className="status-item">
              <span className="status-item-label">Rspamd Quarantine</span>
              <span className="cell-emphasis cell-mono text-warning">14.6%</span>
            </div>
            <div className="status-item">
              <span className="status-item-label">DNSBL Blacklist Reject</span>
              <span className="cell-emphasis cell-mono text-error">2.8%</span>
            </div>
            <div className="status-item">
              <span className="status-item-label">ClamAV Malware Block</span>
              <span className="cell-emphasis cell-mono text-error">0.2%</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
