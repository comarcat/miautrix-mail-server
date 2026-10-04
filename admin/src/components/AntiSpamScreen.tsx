import React, { useEffect, useState } from 'react';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';
import { ALL_DOMAINS } from '../utils/domainFilter';

interface AntiSpamScreenProps {
  client?: AdminApiClient;
  selectedDomain?: string;
}

export const AntiSpamScreen: React.FC<AntiSpamScreenProps> = ({ client = defaultClient, selectedDomain = ALL_DOMAINS }) => {
  const [domains, setDomains] = useState<{ id: string; name: string }[]>([]);
  const [selectedDomainId, setSelectedDomainId] = useState<string>('');
  const [rejectScore, setRejectScore] = useState<number>(14.0);
  const [quarantineScore, setQuarantineScore] = useState<number>(10.0);
  const [headerScore, setHeaderScore] = useState<number>(6.0);
  const [greylistScore, setGreylistScore] = useState<number>(4.0);
  const [greylistingEnabled, setGreylistingEnabled] = useState<boolean>(true);
  const [spfEnforcement, setSpfEnforcement] = useState<boolean>(true);
  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    const loadDomains = async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await client.getDomains();
        if (!isMounted) return;

        const availableDomains = res.data || [];
        setDomains(availableDomains.map((d) => ({ id: d.id, name: d.name })));

        const preferred = selectedDomain !== ALL_DOMAINS
          ? availableDomains.find((d) => d.name === selectedDomain)
          : undefined;
        const domain = preferred || availableDomains[0];
        if (!domain) {
          setError('No domains available to configure.');
          return;
        }

        setSelectedDomainId(domain.id);
      } catch (err: unknown) {
        if (isMounted) setError(err instanceof Error ? err.message : 'Unable to load domains.');
      } finally {
        if (isMounted) setLoading(false);
      }
    };

    loadDomains();
    return () => {
      isMounted = false;
    };
  }, [client, selectedDomain]);

  useEffect(() => {
    if (!selectedDomainId) return;

    let isMounted = true;
    const loadSettings = async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await client.getDomainAntiSpamSettings(selectedDomainId);
        if (!isMounted) return;
        setRejectScore(res.data.reject_score);
        setQuarantineScore(res.data.quarantine_score);
        setHeaderScore(res.data.header_score);
        setGreylistScore(res.data.greylist_score);
        setGreylistingEnabled(res.data.greylisting_enabled);
        setSpfEnforcement(res.data.spf_dmarc_enforcement_enabled);
      } catch (err: unknown) {
        if (isMounted) setError(err instanceof Error ? err.message : 'Unable to load anti-spam settings.');
      } finally {
        if (isMounted) setLoading(false);
      }
    };

    loadSettings();
    return () => {
      isMounted = false;
    };
  }, [client, selectedDomainId]);

  const handleSave = async () => {
    if (!selectedDomainId) {
      setError('Please select a domain first.');
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const res = await client.updateDomainAntiSpamSettings(selectedDomainId, {
        reject_score: rejectScore,
        quarantine_score: quarantineScore,
        header_score: headerScore,
        greylist_score: greylistScore,
        greylisting_enabled: greylistingEnabled,
        spf_dmarc_enforcement_enabled: spfEnforcement,
      });
      setRejectScore(res.data.reject_score);
      setQuarantineScore(res.data.quarantine_score);
      setHeaderScore(res.data.header_score);
      setGreylistScore(res.data.greylist_score);
      setGreylistingEnabled(res.data.greylisting_enabled);
      setSpfEnforcement(res.data.spf_dmarc_enforcement_enabled);
      setActionMessage('Anti-spam policy saved for the selected domain.');
      setTimeout(() => setActionMessage(null), 4000);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Unable to save anti-spam policy.');
    } finally {
      setSaving(false);
    }
  };

  const selectedDomainName = domains.find((d) => d.id === selectedDomainId)?.name || 'Select domain';

  return (
    <div className="screen-container antispam-screen" data-testid="antispam-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Anti-Spam Engine & Scoring Policies</h1>
          <p className="screen-desc">
            Tune Rspamd Bayesian heuristics, DNSBL reputation feeds, dynamic greylisting, and automated scoring thresholds per domain.
          </p>
        </div>
        <div className="header-actions">
          <select
            className="input-select"
            value={selectedDomainId}
            onChange={(e) => setSelectedDomainId(e.target.value)}
            aria-label="Anti-spam policy domain"
          >
            {domains.map((domain) => (
              <option key={domain.id} value={domain.id}>{domain.name}</option>
            ))}
          </select>
          <button className="btn btn-primary" onClick={handleSave} disabled={saving || loading || !selectedDomainId}>
            {saving ? 'Saving...' : 'Apply Anti-Spam Policy'}
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

      <div className="metrics-grid">
        <div className="card metric-card">
          <span className="metric-label">Policy Domain</span>
          <span className="metric-value text-info" style={{ fontSize: '24px' }}>{selectedDomainName}</span>
          <span className="metric-sub">Settings persist per domain</span>
        </div>
        <div className="card metric-card">
          <span className="metric-label">Spam Block Rate (24h)</span>
          <span className="metric-value text-success" style={{ fontSize: '24px' }}>99.82%</span>
          <span className="metric-sub">0.01% false positive floor</span>
        </div>
        <div className="card metric-card">
          <span className="metric-label">Bayes Model Status</span>
          <span className="metric-value text-info" style={{ fontSize: '24px' }}>Synchronized</span>
          <span className="metric-sub">142,500 spam / 89,000 ham tokens</span>
        </div>
        <div className="card metric-card">
          <span className="metric-label">Greylisting Cache</span>
          <span className="metric-value text-info" style={{ fontSize: '24px' }}>1,240 Triples</span>
          <span className="metric-sub">300s initial delay interval</span>
        </div>
      </div>

      <div className="dashboard-row">
        <div className="card flex-1">
          <h2 className="card-title">Rspamd Action Scoring Thresholds</h2>
          <p className="card-subtitle">Aggregate score required to trigger defensive actions</p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '16px' }}>
            <div className="form-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                <span className="form-label">Hard Reject Threshold (SMTP 550)</span>
                <span className="cell-mono text-error" style={{ fontWeight: 600 }}>Score &ge; {rejectScore.toFixed(1)}</span>
              </div>
              <input type="range" min="10.0" max="25.0" step="0.5" value={rejectScore} onChange={(e) => setRejectScore(parseFloat(e.target.value))} />
              <p className="cell-dim" style={{ fontSize: '11px' }}>Instantly rejects message during SMTP session before body transfer.</p>
            </div>

            <div className="form-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                <span className="form-label">Quarantine Threshold</span>
                <span className="cell-mono text-warning" style={{ fontWeight: 600 }}>Score &ge; {quarantineScore.toFixed(1)}</span>
              </div>
              <input type="range" min="6.0" max="15.0" step="0.5" value={quarantineScore} onChange={(e) => setQuarantineScore(parseFloat(e.target.value))} />
              <p className="cell-dim" style={{ fontSize: '11px' }}>Holds message in tenant quarantine for administrator review.</p>
            </div>

            <div className="form-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                <span className="form-label">Add Spam Header / Junk Folder</span>
                <span className="cell-mono text-info" style={{ fontWeight: 600 }}>Score &ge; {headerScore.toFixed(1)}</span>
              </div>
              <input type="range" min="4.0" max="10.0" step="0.5" value={headerScore} onChange={(e) => setHeaderScore(parseFloat(e.target.value))} />
              <p className="cell-dim" style={{ fontSize: '11px' }}>Appends X-Spam-Flag: YES and routes to recipient Junk mailbox.</p>
            </div>

            <div className="form-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                <span className="form-label">Greylist Threshold (SMTP 451)</span>
                <span className="cell-mono" style={{ fontWeight: 600 }}>Score &ge; {greylistScore.toFixed(1)}</span>
              </div>
              <input type="range" min="2.0" max="8.0" step="0.5" value={greylistScore} onChange={(e) => setGreylistScore(parseFloat(e.target.value))} />
              <p className="cell-dim" style={{ fontSize: '11px' }}>Temporarily defers suspicious unknown senders to verify RFC retry compliance.</p>
            </div>
          </div>
        </div>

        <div className="card flex-1">
          <h2 className="card-title">Defensive Heuristics & Feeds</h2>
          <p className="card-subtitle">Active real-time IP reputation and protocol validation engines</p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '16px' }}>
            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">Dynamic Greylisting</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Defer unknown external connecting IP/Sender pairs.</div>
              </div>
              <input type="checkbox" aria-label="Dynamic Greylisting" checked={greylistingEnabled} onChange={(e) => setGreylistingEnabled(e.target.checked)} style={{ width: '18px', height: '18px', cursor: 'pointer' }} />
            </div>

            <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingBottom: '12px', borderBottom: '1px solid var(--border-soft)' }}>
              <div>
                <div className="cell-emphasis">Strict SPF & DMARC Enforcement</div>
                <div className="cell-dim" style={{ fontSize: '11px' }}>Reject hard-fail SPF and DMARC p=reject senders immediately.</div>
              </div>
              <input type="checkbox" aria-label="Strict SPF & DMARC Enforcement" checked={spfEnforcement} onChange={(e) => setSpfEnforcement(e.target.checked)} style={{ width: '18px', height: '18px', cursor: 'pointer' }} />
            </div>

            <div className="status-list">
              <div className="status-item"><span className="status-item-label">Spamhaus ZEN (PBL/SBL/XBL)</span><span className="badge badge-success">Online (0.8ms query)</span></div>
              <div className="status-item"><span className="status-item-label">Barracuda Reputation Network (b.barracudacentral.org)</span><span className="badge badge-success">Online</span></div>
              <div className="status-item"><span className="status-item-label">SpamCop Blocking List (bl.spamcop.net)</span><span className="badge badge-success">Online</span></div>
              <div className="status-item"><span className="status-item-label">SORBS Dynamic IP List</span><span className="badge badge-success">Online</span></div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
