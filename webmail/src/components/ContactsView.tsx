import React, { useState } from 'react';
import type { Contact } from '../types';

interface ContactsViewProps {
  contacts: Contact[];
  onEmailContact: (email: string) => void;
}

export const ContactsView: React.FC<ContactsViewProps> = ({ contacts, onEmailContact }) => {
  const [selectedBook, setSelectedBook] = useState<'personal' | 'directory'>('personal');
  const [search, setSearch] = useState('');

  const filteredContacts = contacts.filter((c) => {
    const matchesBook = c.book === selectedBook;
    const matchesSearch =
      !search ||
      c.name.toLowerCase().includes(search.toLowerCase()) ||
      c.email.toLowerCase().includes(search.toLowerCase()) ||
      c.organization.toLowerCase().includes(search.toLowerCase());
    return matchesBook && matchesSearch;
  });

  return (
    <div className="webmail-layout">
      {/* Header & Ribbon */}
      <div className="wm-ribbon">
        <div className="wm-ribbon-group">
          <button type="button" className="btn btn-primary" style={{ padding: '6px 14px', fontSize: '14px' }}>
            <img src="/images/icons/webmail/contacts.png" alt="" style={{ width: '14px', filter: 'brightness(0) invert(1)' }} />
            New Contact
          </button>
        </div>

        <div className="wm-ribbon-group">
          <button type="button" className="wm-tool-btn" title="Edit Contact"><img src="/images/icons/webmail/compose-pencil.png" alt="Edit" /></button>
          <button type="button" className="wm-tool-btn" title="Delete"><img src="/images/icons/webmail/trash.png" alt="Delete" /></button>
          <button type="button" className="wm-tool-btn" title="Export vCard"><img src="/images/icons/webmail/archive-box.png" alt="Export" /></button>
        </div>
      </div>

      {/* Body */}
      <div className="wm-body">
        {/* Sidebar */}
        <aside className="wm-sidebar">
          <div className="wm-nav-section">
            <div className="wm-nav-head">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <polyline points="6 9 12 15 18 9"></polyline>
              </svg>
              Address Books
            </div>
            <a
              className={`wm-nav-item ${selectedBook === 'personal' ? 'active' : ''}`}
              href="#personal"
              onClick={(e) => {
                e.preventDefault();
                setSelectedBook('personal');
              }}
            >
              <div className="wm-nav-icon">
                <img src="/images/icons/webmail/contacts.png" alt="" />
                <span>Personal Contacts</span>
              </div>
              <span className="badge">{contacts.filter((c) => c.book === 'personal').length}</span>
            </a>
            <a
              className={`wm-nav-item ${selectedBook === 'directory' ? 'active' : ''}`}
              href="#directory"
              onClick={(e) => {
                e.preventDefault();
                setSelectedBook('directory');
              }}
            >
              <div className="wm-nav-icon">
                <img src="/images/icons/webmail/contacts.png" alt="" />
                <span>Company Directory</span>
              </div>
              <span className="badge">{contacts.filter((c) => c.book === 'directory').length}</span>
            </a>
          </div>
        </aside>

        {/* Main Contacts Table */}
        <main className="wm-content">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
            <h1 style={{ fontSize: '2rem' }}>
              {selectedBook === 'personal' ? 'Personal Contacts' : 'Company Directory'}
            </h1>
            <input
              className="input-base"
              type="text"
              placeholder="Search contacts..."
              style={{ maxWidth: '280px', fontSize: '14px', padding: '6px 12px' }}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <div style={{ background: 'var(--pure-white)', border: '1px solid var(--neutral-border)', borderRadius: 'var(--radius-lg)', overflow: 'hidden', boxShadow: 'var(--shadow-lvl1)' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '14px' }}>
              <thead>
                <tr style={{ background: 'var(--surface-canvas)', borderBottom: '1px solid var(--neutral-border)', color: 'var(--neutral-label)', fontWeight: 500 }}>
                  <th style={{ padding: '12px 16px' }}>Name</th>
                  <th style={{ padding: '12px 16px' }}>Email</th>
                  <th style={{ padding: '12px 16px' }}>Organization</th>
                  <th style={{ padding: '12px 16px' }}>Action</th>
                </tr>
              </thead>
              <tbody>
                {filteredContacts.map((contact) => (
                  <tr key={contact.id} style={{ borderBottom: '1px solid var(--neutral-border)' }}>
                    <td style={{ padding: '14px 16px', fontWeight: 500, color: 'var(--deep-navy)' }}>{contact.name}</td>
                    <td style={{ padding: '14px 16px', color: 'var(--neutral-body)', fontFamily: 'var(--font-mono)' }}>{contact.email}</td>
                    <td style={{ padding: '14px 16px', color: 'var(--neutral-body)' }}>{contact.organization}</td>
                    <td style={{ padding: '14px 16px' }}>
                      <button
                        type="button"
                        className="btn btn-outline"
                        style={{ padding: '4px 10px', fontSize: '12px' }}
                        onClick={() => onEmailContact(contact.email)}
                      >
                        Email
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
