import React, { useState } from 'react';
import type { SieveFilterRule } from '../types';

interface SieveRulesViewProps {
  initialRules: SieveFilterRule[];
}

export const SieveRulesView: React.FC<SieveRulesViewProps> = ({ initialRules }) => {
  const [rules, setRules] = useState<SieveFilterRule[]>(initialRules);
  const [newRuleName, setNewRuleName] = useState('');
  const [newRuleField, setNewRuleField] = useState<'from' | 'subject' | 'to' | 'header'>('subject');
  const [newRuleValue, setNewRuleValue] = useState('');
  const [newRuleAction, setNewRuleAction] = useState<'fileinto' | 'redirect' | 'reject' | 'addflag' | 'discard'>('fileinto');
  const [newRuleTarget, setNewRuleTarget] = useState('Archive');

  const handleAddRule = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newRuleName || !newRuleValue) return;

    const newRule: SieveFilterRule = {
      id: `rule-${Date.now()}`,
      name: newRuleName,
      field: newRuleField,
      comparator: 'contains',
      value: newRuleValue,
      action: newRuleAction,
      targetFolder: newRuleTarget,
      active: true,
    };

    setRules([...rules, newRule]);
    setNewRuleName('');
    setNewRuleValue('');
  };

  const toggleRuleActive = (id: string) => {
    setRules(rules.map((r) => (r.id === id ? { ...r, active: !r.active } : r)));
  };

  const deleteRule = (id: string) => {
    setRules(rules.filter((r) => r.id !== id));
  };

  return (
    <div className="webmail-layout">
      <div className="wm-ribbon">
        <div className="wm-ribbon-group">
          <button type="button" className="btn btn-primary" style={{ padding: '6px 14px', fontSize: '14px' }}>
            <img src="/images/icons/webmail/settings.png" alt="" style={{ width: '14px', filter: 'brightness(0) invert(1)' }} />
            ManageSieve Active Script
          </button>
        </div>
      </div>

      <div className="wm-body">
        <main className="wm-content" style={{ maxWidth: '1000px', margin: '0 auto' }}>
          <div style={{ marginBottom: '24px' }}>
            <h1 style={{ fontSize: '2rem', marginBottom: '8px' }}>ManageSieve Filter Rules</h1>
            <p style={{ color: 'var(--neutral-body)', margin: 0 }}>
              Server-side Sieve scripts execute automatically upon incoming delivery prior to mailbox storage.
            </p>
          </div>

          {/* Add Rule Form */}
          <div className="card" style={{ marginBottom: '32px' }}>
            <h3 style={{ fontSize: '1.25rem', marginBottom: '16px' }}>Create Sieve Rule</h3>
            <form onSubmit={handleAddRule}>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, color: 'var(--deep-navy)', marginBottom: '4px' }}>Rule Name</label>
                  <input
                    type="text"
                    className="input-base"
                    placeholder="e.g. Move Jira alerts"
                    value={newRuleName}
                    onChange={(e) => setNewRuleName(e.target.value)}
                    required
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, color: 'var(--deep-navy)', marginBottom: '4px' }}>Condition Field</label>
                  <select
                    className="input-base"
                    value={newRuleField}
                    onChange={(e) => setNewRuleField(e.target.value as any)}
                  >
                    <option value="subject">Subject</option>
                    <option value="from">From</option>
                    <option value="to">To / Cc</option>
                    <option value="header">Header</option>
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, color: 'var(--deep-navy)', marginBottom: '4px' }}>Contains Pattern</label>
                  <input
                    type="text"
                    className="input-base"
                    placeholder="e.g. [PROJ-"
                    value={newRuleValue}
                    onChange={(e) => setNewRuleValue(e.target.value)}
                    required
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, color: 'var(--deep-navy)', marginBottom: '4px' }}>Action</label>
                  <select
                    className="input-base"
                    value={newRuleAction}
                    onChange={(e) => setNewRuleAction(e.target.value as any)}
                  >
                    <option value="fileinto">File Into Folder</option>
                    <option value="addflag">Mark as Flagged</option>
                    <option value="reject">Reject with SMTP DSN</option>
                    <option value="discard">Discard / Drop</option>
                  </select>
                </div>
              </div>

              {newRuleAction === 'fileinto' && (
                <div style={{ marginBottom: '16px', maxWidth: '300px' }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, color: 'var(--deep-navy)', marginBottom: '4px' }}>Target Folder</label>
                  <select className="input-base" value={newRuleTarget} onChange={(e) => setNewRuleTarget(e.target.value)}>
                    <option value="Archive">Archive</option>
                    <option value="Quarantine">Quarantine / Spam</option>
                    <option value="Trash">Trash</option>
                  </select>
                </div>
              )}

              <button type="submit" className="btn btn-primary" style={{ padding: '8px 16px' }}>
                Add Filter Rule
              </button>
            </form>
          </div>

          {/* Active Rules List */}
          <div className="card">
            <h3 style={{ fontSize: '1.25rem', marginBottom: '16px' }}>Active Sieve Rules</h3>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '14px' }}>
              <thead>
                <tr style={{ background: 'var(--surface-canvas)', borderBottom: '1px solid var(--neutral-border)', color: 'var(--neutral-label)', fontWeight: 500 }}>
                  <th style={{ padding: '12px 16px' }}>Status</th>
                  <th style={{ padding: '12px 16px' }}>Name</th>
                  <th style={{ padding: '12px 16px' }}>Condition</th>
                  <th style={{ padding: '12px 16px' }}>Action</th>
                  <th style={{ padding: '12px 16px' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {rules.map((r) => (
                  <tr key={r.id} style={{ borderBottom: '1px solid var(--neutral-border)' }}>
                    <td style={{ padding: '12px 16px' }}>
                      <input
                        type="checkbox"
                        checked={r.active}
                        onChange={() => toggleRuleActive(r.id)}
                        style={{ accentColor: 'var(--iris-violet)', width: '16px', height: '16px' }}
                      />
                    </td>
                    <td style={{ padding: '12px 16px', fontWeight: 500, color: 'var(--deep-navy)' }}>{r.name}</td>
                    <td style={{ padding: '12px 16px', color: 'var(--neutral-body)', fontFamily: 'var(--font-mono)', fontSize: '13px' }}>
                      {r.field} contains "{r.value}"
                    </td>
                    <td style={{ padding: '12px 16px', color: 'var(--neutral-body)' }}>
                      {r.action === 'fileinto' ? `fileinto "${r.targetFolder}"` : r.action}
                    </td>
                    <td style={{ padding: '12px 16px' }}>
                      <button
                        type="button"
                        className="btn btn-ghost"
                        style={{ color: 'var(--accent-ruby)', padding: '4px 8px', fontSize: '13px' }}
                        onClick={() => deleteRule(r.id)}
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </main>
      </div>
    </div>
  );
};
