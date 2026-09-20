import React, { useState } from 'react';

export const AntiSpamScreen: React.FC = () => {
  const [rejectScore, setRejectScore] = useState<number>(14.0);
  const [quarantineScore, setQuarantineScore] = useState<number>(10.0);
  const [headerScore, setHeaderScore] = useState<number>(6.0);
  const [greylistScore, setGreylistScore] = useState<number>(4.0);
  const [greylistingEnabled, setGreylistingEnabled] = useState<boolean>(true);
  const [spfEnforcement, setSpfEnforcement] = useState<boolean>(true);
  const [actionMessage, setActionMessage] = useState<string | null>(null);

  const handleSave = () => {
    setActionMessage('Anti-spam thresholds synchronized with Rspamd Milter daemon.');
    setTimeout(() => setActionMessage(null), 4000);
  };

  return (
    <div className="screen-container antispam-screen" data-testid="antispam-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Anti-Spam Engine & Scoring Policies</h1>
          <p className="screen-desc">
            Tune Rspamd Bayesian heuristics, DNSBL reputation feeds, dynamic greylisting, and automated scoring thresholds.
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-primary" onClick={handleSave}>
            Apply Anti-Spam Policy
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
          <span className="metric-label">Spam Block Rate (24h)</span>
          <span className="metric-value text-success" style={{ fontSize: '24px' }}>
            99.82%
          </span>
          <span className="metric-sub">0.01% false positive floor</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">Bayes Model Status</span>
          <span className="metric-value text-info" style={{ fontSize: '24px' }}>
            Synchronized
          </span>
          <span className="metric-sub">142,500 spam / 89,000 ham tokens</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">Active DNSBL Feeds</span>
          <span className="metric-value" style={{ fontSize: '24px' }}>
            5 Feeds
          </span>
          <span className="metric-sub">Spamhaus ZEN, Barracuda, SpamCop</span>
        </div>

        <div className="card metric-card">
          <span className="metric-label">Greylisting Cache</span>
          <span className="metric-value text-info" style={{ fontSize: '24px' }}>
            1,240 Triples
          </span>
          <span className="metric-sub">300s initial delay interval</span>
        </div>
      </div>

      <div className="dashboard-row">
        {/* Scoring Thresholds */}
        <div className="card flex-1">
          <h2 className="card-title">Rspamd Action Scoring Thresholds</h2>
          <p className="card-subtitle">Aggregate score required to trigger defensive actions</p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '16px' }}>
            <div className="form-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                <span className="form-label">Hard Reject Threshold (SMTP 550)</span>
                <span className="cell-mono text-error" style={{ fontWeight: 600 }}>Score &ge; {rejectScore.toFixed(1)}</span>
              </div>
              <input
                type="range"
                min="10.0"
                max="25.0"
                step="0.5"
                value={rejectScore}
                onChange={(e) => setRejectScore(parseFloat(e.target.value))}
              />
              <p className="cell-dim" style={{ fontSize: '11px' }}>Instantly rejects message during SMTP session before body transfer.</p>
            </div>

            <div className="form-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                <span className="form-label">Quarantine Threshold</span>
                <span className="cell-mono text-warning" style={{ fontWeight: 600 }}>Score &ge; {quarantineScore.toFixed(1)}</span>
              </div>
              <input
                type="range"
                min="6.0"
                max="15.0"
                step="0.5"
                value={quarantineScore}
                onChange={(e) => setQuarantineScore(parseFloat(e.target.value))}
              />
              <p className="cell-dim" style={{ fontSize: '11px' }}>Holds message in tenant quarantine for administrator review.</p>
            </div>

            <div className="form-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                <span className="form-label">Add Spam Header / Junk Folder</span>
                <span className="cell-mono text-info" style={{ fontWeight: 600 }}>Score &ge; {headerScore.toFixed(1)}</span>
              </div>
              <input
                type="range"
                min="4.0"
                max="10.0"
                step="0.5"
                value={headerScore}
                onChange={(e) => setHeaderScore(parseFloat(e.target.value))}
              />
              <p className="cell-dim" style={{ fontSize: '11px' }}>Appends X-Spam-Flag: YES and routes to recipient Junk mailbox.</p>
            </div>

            <div className="form-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                <span className="form-label">Greylist Threshold (SMTP 451)</span>
                <span className="cell-mono" style={{ fontWeight: 600 }}>Score &ge; {greylistScore.toFixed(1)}</span>
              </div>
              <input
                type="range"
                min="2.0"
                max="8.0"
                step="0.5"
                value={greylistScore}
                onChange={(e) => setGreylistScore(parseFloat(e.target.value))}
              />
              <p className="cell-dim" style={{ fontSize: '11px' }}>Temporarily defers suspicious unknown senders to verify RFC retry compliance.</p>
            </div>
          </div>
        </div>

        {/* Heuristic Modules & Reputation Lists */}
        <div className="card flex-1">
          <h2 className="card-title">Defensive Heuristics & Feeds</h2>
          <p className="card-subtitle">Active real-time IP reputation and protocol validation engines</p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '16px' }}>
            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">Dynamic Greylisting</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Defer unknown external connecting IP/Sender pairs.</div>
              </div>
              <input
                type="checkbox"
                checked={greylistingEnabled}
                onChange={(e) => setGreylistingEnabled(e.target.checked)}
                style={{ width: '18px', height: '18px', cursor: 'pointer' }}
              />
            </div>

            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">Strict SPF & DMARC Enforcement</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Reject hard-fail SPF and DMARC p=reject senders immediately.</div>
              </div>
              <input
                type="checkbox"
                checked={spfEnforcement}
                onChange={(e) => setSpfEnforcement(e.target.checked)}
                style={{ width: '18px', height: '18px', cursor: 'pointer' }}
              />
            </div>

            <div className="status-list">
              <div className="status-item">
                <span className="status-item-label">Spamhaus ZEN (PBL/SBL/XBL)</span>
                <span className="badge badge-success">Online (0.8ms query)</span>
              </div>
              <div className="status-item">
                <span className="status-item-label">Barracuda Reputation Network (b.barracudacentral.org)</span>
                <span className="badge badge-success">Online</span>
              </div>
              <div className="status-item">
                <span className="status-item-label">SpamCop Blocking List (bl.spamcop.net)</span>
                <span className="badge badge-success">Online</span>
              </div>
              <div className="status-item">
                <span className="status-item-label">SORBS Dynamic IP List</span>
                <span className="badge badge-success">Online</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
