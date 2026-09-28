import React, { useState } from 'react';
import type { Contact } from '../types';
import { webmailClient } from './WebmailApiClient';

interface ContactsViewProps {
  contacts: Contact[];
  onContactsChanged: (contacts: Contact[]) => void;
  onEmailContact: (email: string) => void;
}

const emptyForm = { name: '', email: '', organization: '', department: '', phone: '' };

export const ContactsView: React.FC<ContactsViewProps> = ({ contacts, onContactsChanged, onEmailContact }) => {
  const [selectedBook, setSelectedBook] = useState<'personal' | 'directory'>('personal');
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Contact | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState('');

  const reloadContacts = async () => {
    const res = await webmailClient.getContacts();
    onContactsChanged(res.data);
  };

  const startCreate = () => {
    setEditing(null);
    setForm(emptyForm);
    setError('');
  };

  const startEdit = (contact: Contact) => {
    if (contact.book === 'directory' && !contact.canEdit) return;
    setEditing(contact);
    setForm({
      name: contact.name,
      email: contact.email,
      organization: contact.organization ?? '',
      department: contact.department ?? '',
      phone: contact.phone ?? '',
    });
    setError('');
  };

  const saveContact = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.email.trim()) return;
    try {
      if (editing) {
        if (editing.book === 'directory') {
          await webmailClient.updateDirectoryContact(editing.id, form);
        } else {
          await webmailClient.updateContact(editing.id, form);
        }
      } else {
        await webmailClient.createContact(form);
      }
      setForm(emptyForm);
      setEditing(null);
      await reloadContacts();
    } catch (err: any) {
      setError(err?.message ?? 'Failed to save contact.');
    }
  };

  const deleteContact = async (contact: Contact) => {
    if (contact.book === 'directory') return;
    await webmailClient.deleteContact(contact.id);
    if (editing?.id === contact.id) {
      setEditing(null);
      setForm(emptyForm);
    }
    await reloadContacts();
  };

  const filteredContacts = contacts.filter((c) => {
    const matchesBook = c.book === selectedBook;
    const matchesSearch =
      !search ||
      c.name.toLowerCase().includes(search.toLowerCase()) ||
      c.email.toLowerCase().includes(search.toLowerCase()) ||
      (c.organization ?? '').toLowerCase().includes(search.toLowerCase());
    return matchesBook && matchesSearch;
  });

  return (
    <div className="webmail-layout">
      {/* Header & Ribbon */}
      <div className="wm-ribbon">
        <div className="wm-ribbon-group">
          <button type="button" className="btn btn-primary" style={{ padding: '6px 14px', fontSize: '14px' }} onClick={startCreate}>
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

          <form onSubmit={saveContact} className="card" style={{ display: 'grid', gap: '12px', marginBottom: '20px' }}>
            <h3 style={{ margin: 0 }}>{editing ? 'Edit Contact' : 'New Contact'}</h3>
            {error && <div style={{ color: 'var(--danger-red)', fontSize: '13px' }}>{error}</div>}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: '12px' }}>
              <input className="input-base" placeholder="Name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              <input className="input-base" placeholder="Email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
              <input className="input-base" placeholder="Organization" value={form.organization} onChange={(e) => setForm({ ...form, organization: e.target.value })} />
              <input className="input-base" placeholder="Department" value={form.department} onChange={(e) => setForm({ ...form, department: e.target.value })} />
              <input className="input-base" placeholder="Phone" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
            </div>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button type="submit" className="btn btn-primary">{editing ? 'Save Contact' : 'Create Contact'}</button>
              {editing && <button type="button" className="btn btn-outline" onClick={startCreate}>Cancel</button>}
            </div>
          </form>

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
                    <td style={{ padding: '14px 16px', fontWeight: 500, color: 'var(--deep-navy)' }}>
                      {contact.name}
                      {contact.book === 'directory' && contact.kind && (
                        <span style={{ marginLeft: '8px', fontSize: '10px', padding: '2px 4px', borderRadius: '4px', background: 'var(--surface-canvas)', border: '1px solid var(--neutral-border)', color: 'var(--neutral-label)', textTransform: 'uppercase' }}>
                          {contact.kind}
                        </span>
                      )}
                    </td>
                    <td style={{ padding: '14px 16px', color: 'var(--neutral-body)', fontFamily: 'var(--font-mono)' }}>{contact.email}</td>
                    <td style={{ padding: '14px 16px', color: 'var(--neutral-body)' }}>{contact.organization}</td>
                    <td style={{ padding: '14px 16px' }}>
                      <div style={{ display: 'flex', gap: '6px' }}>
                        <button
                          type="button"
                          className="btn btn-outline"
                          style={{ padding: '4px 10px', fontSize: '12px' }}
                          onClick={() => onEmailContact(contact.email)}
                        >
                          Email
                        </button>
                        {(contact.book === 'personal' || contact.canEdit) && (
                          <button type="button" className="btn btn-outline" style={{ padding: '4px 10px', fontSize: '12px' }} onClick={() => startEdit(contact)}>Edit</button>
                        )}
                        {contact.book === 'personal' && (
                          <button type="button" className="btn btn-outline" style={{ padding: '4px 10px', fontSize: '12px' }} onClick={() => void deleteContact(contact)}>Delete</button>
                        )}
                      </div>
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
