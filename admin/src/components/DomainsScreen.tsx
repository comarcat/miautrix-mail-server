import React, { useState, useEffect, useCallback } from 'react';
import { DomainItem, CreateDomainRequest, UpdateDomainRequest, VerifyDomainResult, ApiResponse } from '../types';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';

interface DomainsScreenProps {
  client?: AdminApiClient;
}

export const DomainsScreen: React.FC<DomainsScreenProps> = ({ client = defaultClient }) => {
  const [domains, setDomains] = useState<DomainItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [search, setSearch] = useState<string>('');

  // Modals
  const [isAddOpen, setIsAddOpen] = useState<boolean>(false);
  const [verifyingDomain, setVerifyingDomain] = useState<string | null>(null);
  const [verifyResult, setVerifyResult] = useState<{ domain: string; result: VerifyDomainResult } | null>(null);
  const [inspectingDomain, setInspectingDomain] = useState<DomainItem | null>(null);

  // Form
  const [formName, setFormName] = useState<string>('');
  const [formIsPrimary, setFormIsPrimary] = useState<boolean>(false);

  // Edit modal state
  const [isEditOpen, setIsEditOpen] = useState<boolean>(false);
  const [editingDomain, setEditingDomain] = useState<DomainItem | null>(null);
  const [formDkimSelector, setFormDkimSelector] = useState<string>('');
  const [formSpfRecord, setFormSpfRecord] = useState<string>('');
  const [formDmarcRecord, setFormDmarcRecord] = useState<string>('');

  const fetchDomains = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res: ApiResponse<DomainItem[]> = await client.getDomains();
      setDomains(res.data || []);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch domains');
      setDomains([]);
    } finally {
      setLoading(false);
    }
  }, [client]);

  useEffect(() => {
    fetchDomains();
  }, [fetchDomains]);

  const handleAddSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formName.trim()) {
      setError('Domain name is required.');
      return;
    }

    try {
      const payload: CreateDomainRequest = {
        name: formName.trim().toLowerCase(),
        is_primary: formIsPrimary,
      };
      await client.createDomain(payload);
      setIsAddOpen(false);
      setFormName('');
      setFormIsPrimary(false);
      setActionMessage(`Domain ${payload.name} added successfully. Please configure DNS records.`);
      fetchDomains();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to add domain');
    }
  };

  const openEdit = (domain: DomainItem) => {
    setEditingDomain(domain);
    setFormName(domain.name);
    setFormIsPrimary(domain.is_primary);
    setFormDkimSelector(domain.dkim_selector || '');
    setFormSpfRecord(domain.spf_record || '');
    setFormDmarcRecord(domain.dmarc_record || '');
    setIsEditOpen(true);
  };

  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingDomain) return;

    try {
      const payload: UpdateDomainRequest = {
        name: formName.trim().toLowerCase(),
        is_primary: formIsPrimary,
        dkim_selector: formDkimSelector.trim() || undefined,
        spf_record: formSpfRecord.trim() || undefined,
        dmarc_record: formDmarcRecord.trim() || undefined,
      };
      await client.updateDomain(editingDomain.id, payload);
      setIsEditOpen(false);
      setEditingDomain(null);
      setActionMessage('Domain updated successfully.');
      fetchDomains();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update domain');
    }
  };

  const handleVerify = async (domain: DomainItem) => {
    setVerifyingDomain(domain.id);
    setError(null);
    try {
      const res = await client.verifyDomain(domain.id);
      setVerifyResult({ domain: domain.name, result: res.data });
      fetchDomains();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : `DNS verification failed for ${domain.name}`);
    } finally {
      setVerifyingDomain(null);
    }
  };

  const handleDelete = async (domain: DomainItem) => {
    if (domain.is_primary) {
      setError('Cannot delete the primary tenant domain.');
      return;
    }

    if (!window.confirm(`Are you sure you want to delete domain ${domain.name}? Mail delivery for this domain will immediately cease.`)) {
      return;
    }

    try {
      await client.deleteDomain(domain.id);
      setActionMessage(`Domain ${domain.name} deleted.`);
      fetchDomains();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to delete domain');
    }
  };

  const filteredDomains = domains.filter((d) =>
    d.name.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="screen-container domains-screen" data-testid="domains-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Accepted Mail Domains & DNS</h1>
          <p className="screen-desc">
            Configure inbound recipient routing, cryptographic DKIM keys, SPF policies, and DMARC enforcement.
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-secondary" onClick={fetchDomains} title="Refresh domains">
            Refresh
          </button>
          <button className="btn btn-primary" onClick={() => setIsAddOpen(true)} data-testid="add-domain-btn">
            + Add Domain
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
        <div className="search-form">
          <div className="input-search-wrapper">
            <svg className="search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              type="search"
              className="input-search"
              placeholder="Search domains..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search domains"
            />
          </div>
        </div>
        <div className="cell-dim" style={{ fontSize: '12px' }}>
          {domains.length} configured {domains.length === 1 ? 'domain' : 'domains'}
        </div>
      </div>

      <div className="card table-card">
        {loading ? (
          <div className="loading-state" data-testid="domains-loading">
            <div className="spinner" />
            <p>Loading domain records...</p>
          </div>
        ) : filteredDomains.length === 0 ? (
          <div className="empty-state" data-testid="domains-empty">
            <p>No domains found matching criteria.</p>
          </div>
        ) : (
          <div className="table-responsive">
            <table className="data-table" data-testid="domains-table">
              <thead>
                <tr>
                  <th>Status</th>
                  <th>Domain Name</th>
                  <th>DKIM Selector</th>
                  <th>SPF Status</th>
                  <th>DMARC Status</th>
                  <th>Primary</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredDomains.map((d) => (
                  <tr key={d.id}>
                    <td>
                      <span className={`badge ${d.is_verified ? 'badge-success' : 'badge-warning'}`}>
                        {d.is_verified ? 'Verified' : 'Pending DNS'}
                      </span>
                    </td>
                    <td>
                      <span className="cell-emphasis cell-mono">{d.name}</span>
                    </td>
                    <td>
                      <span className="cell-mono cell-dim">{d.dkim_selector || 'miautrix'}</span>
                    </td>
                    <td>
                      <span className="badge badge-info">{d.spf_record ? 'Configured' : 'Recommended'}</span>
                    </td>
                    <td>
                      <span className="badge badge-info">{d.dmarc_record ? 'Configured' : 'Recommended'}</span>
                    </td>
                    <td>
                      {d.is_primary ? (
                        <span className="badge badge-info">Primary</span>
                      ) : (
                        <span className="cell-dim">—</span>
                      )}
                    </td>
                    <td>
                      <div className="action-buttons">
                        <button
                          className="btn-link"
                          onClick={() => setInspectingDomain(d)}
                          title="View DNS Records"
                        >
                          DNS Config
                        </button>
                        <button
                          className="btn-link text-info"
                          onClick={() => handleVerify(d)}
                          disabled={verifyingDomain === d.id}
                        >
                          {verifyingDomain === d.id ? 'Checking...' : 'Verify DNS'}
                        </button>
                        <button
                          className="btn-link"
                          onClick={() => openEdit(d)}
                          title="Edit domain"
                        >
                          Edit
                        </button>
                        {!d.is_primary && (
                          <button
                            className="btn-link text-error"
                            onClick={() => handleDelete(d)}
                          >
                            Delete
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Add Domain Modal */}
      {isAddOpen && (
        <div className="modal-backdrop" data-testid="add-domain-modal">
          <div className="modal-card">
            <div className="modal-header">
              <h2 className="modal-title">Add Accepted Domain</h2>
              <button className="btn-close" onClick={() => setIsAddOpen(false)}>✕</button>
            </div>
            <form onSubmit={handleAddSubmit}>
              <div className="modal-body">
                <div className="form-group">
                  <label className="form-label">Domain Name (FQDN)</label>
                  <input
                    type="text"
                    className="input-text w-full cell-mono"
                    placeholder="example.com"
                    value={formName}
                    onChange={(e) => setFormName(e.target.value)}
                    required
                  />
                  <p className="cell-dim" style={{ fontSize: '11px', marginTop: '4px' }}>
                    Must be a valid Fully Qualified Domain Name configured with MX records pointing to this mail server.
                  </p>
                </div>
                <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
                  <input
                    type="checkbox"
                    id="domain-primary-checkbox"
                    checked={formIsPrimary}
                    onChange={(e) => setFormIsPrimary(e.target.checked)}
                  />
                  <label htmlFor="domain-primary-checkbox" className="detail-value" style={{ cursor: 'pointer' }}>
                    Set as Primary Tenant Domain
                  </label>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setIsAddOpen(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary">
                  Add Domain & Generate Keys
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Edit Domain Modal */}
      {isEditOpen && editingDomain && (
        <div className="modal-backdrop" data-testid="edit-domain-modal">
          <div className="modal-card" style={{ maxWidth: '640px' }}>
            <div className="modal-header">
              <h2 className="modal-title">Edit Domain</h2>
              <button className="btn-close" onClick={() => { setIsEditOpen(false); setEditingDomain(null); }}>✕</button>
            </div>
            <form onSubmit={handleEditSubmit}>
              <div className="modal-body">
                <div className="form-group">
                  <label className="form-label">Domain Name (FQDN)</label>
                  <input
                    type="text"
                    className="input-text w-full cell-mono"
                    placeholder="example.com"
                    value={formName}
                    onChange={(e) => setFormName(e.target.value)}
                    required
                  />
                  <p className="cell-dim" style={{ fontSize: '11px', marginTop: '4px' }}>
                    Changing DNS-relevant fields marks the domain unverified until re-verified.
                  </p>
                </div>
                <div className="form-group">
                  <label className="form-label">DKIM Selector</label>
                  <input
                    type="text"
                    className="input-text w-full cell-mono"
                    placeholder="m1"
                    value={formDkimSelector}
                    onChange={(e) => setFormDkimSelector(e.target.value)}
                  />
                </div>
                <div className="form-group">
                  <label className="form-label">SPF Record</label>
                  <input
                    type="text"
                    className="input-text w-full cell-mono"
                    placeholder="v=spf1 mx -all"
                    value={formSpfRecord}
                    onChange={(e) => setFormSpfRecord(e.target.value)}
                  />
                </div>
                <div className="form-group">
                  <label className="form-label">DMARC Record</label>
                  <input
                    type="text"
                    className="input-text w-full cell-mono"
                    placeholder="v=DMARC1; p=reject; ..."
                    value={formDmarcRecord}
                    onChange={(e) => setFormDmarcRecord(e.target.value)}
                  />
                </div>
                <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '8px', marginTop: '8px' }}>
                  <input
                    type="checkbox"
                    id="domain-edit-primary-checkbox"
                    checked={formIsPrimary}
                    onChange={(e) => setFormIsPrimary(e.target.checked)}
                  />
                  <label htmlFor="domain-edit-primary-checkbox" className="detail-value" style={{ cursor: 'pointer' }}>
                    Set as Primary Tenant Domain
                  </label>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => { setIsEditOpen(false); setEditingDomain(null); }}>
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary">
                  Save Changes
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* DNS Configuration Inspector Modal */}
      {inspectingDomain && (
        <div className="modal-backdrop" data-testid="dns-inspect-modal">
          <div className="modal-card" style={{ maxWidth: '640px' }}>
            <div className="modal-header">
              <h2 className="modal-title">DNS Records for {inspectingDomain.name}</h2>
              <button className="btn-close" onClick={() => setInspectingDomain(null)}>✕</button>
            </div>
            <div className="modal-body">
              <div className="detail-row-block">
                <div className="detail-label">1. MX Record (Inbound Delivery)</div>
                <pre className="detail-pre" style={{ color: 'var(--text)' }}>
{`${inspectingDomain.name}.  300  IN  MX  10  mail.${inspectingDomain.name}.`}
                </pre>
              </div>

              <div className="detail-row-block">
                <div className="detail-label">2. SPF TXT Record (Sender Policy Framework)</div>
                <pre className="detail-pre" style={{ color: 'var(--text)' }}>
{inspectingDomain.spf_record || `v=spf1 mx a:mail.${inspectingDomain.name} -all`}
                </pre>
              </div>

              <div className="detail-row-block">
                <div className="detail-label">3. DKIM TXT Record (Selector: {inspectingDomain.dkim_selector || 'miautrix'})</div>
                <pre className="detail-pre" style={{ color: 'var(--text)' }}>
{`${inspectingDomain.dkim_selector || 'miautrix'}._domainkey.${inspectingDomain.name}.  TXT  "v=DKIM1; k=rsa; p=${inspectingDomain.dkim_public_key || 'MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAy0k8p7+v41...KEY'}"`}
                </pre>
              </div>

              <div className="detail-row-block">
                <div className="detail-label">4. DMARC TXT Record (Enforcement Policy)</div>
                <pre className="detail-pre" style={{ color: 'var(--text)' }}>
{inspectingDomain.dmarc_record || `_dmarc.${inspectingDomain.name}.  TXT  "v=DMARC1; p=reject; rua=mailto:dmarc-reports@${inspectingDomain.name}; pct=100"`}
                </pre>
              </div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn btn-secondary" onClick={() => setInspectingDomain(null)}>
                Close
              </button>
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => {
                  handleVerify(inspectingDomain);
                  setInspectingDomain(null);
                }}
              >
                Verify Records Now
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Verify Result Modal */}
      {verifyResult && (
        <div className="modal-backdrop" data-testid="verify-result-modal">
          <div className="modal-card">
            <div className="modal-header">
              <h2 className="modal-title">DNS Verification: {verifyResult.domain}</h2>
              <button className="btn-close" onClick={() => setVerifyResult(null)}>✕</button>
            </div>
            <div className="modal-body">
              <div className={`alert ${verifyResult.result.is_verified ? 'alert-info' : 'alert-error'}`}>
                <span>{verifyResult.result.message}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">DKIM Status</span>
                <span className="badge badge-success">{verifyResult.result.dkim_status}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">SPF Status</span>
                <span className="badge badge-success">{verifyResult.result.spf_status}</span>
              </div>
              <div className="detail-row">
                <span className="detail-label">DMARC Status</span>
                <span className="badge badge-success">{verifyResult.result.dmarc_status}</span>
              </div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn btn-primary" onClick={() => setVerifyResult(null)}>
                Done
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

