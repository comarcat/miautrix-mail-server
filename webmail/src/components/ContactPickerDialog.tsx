import React, { useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import type { Contact } from '../types';

type RecipientField = 'to' | 'cc' | 'bcc';

interface ContactPickerDialogProps {
  open: boolean;
  target: RecipientField;
  contacts: Contact[];
  currentValue: string;
  onApply: (emails: string[]) => void;
  onClose: () => void;
  title?: string;
  description?: string;
  contactFilter?: (contact: Contact) => boolean;
  noBackdrop?: boolean;
}

const fieldLabel = { to: 'To', cc: 'Cc', bcc: 'Bcc' } satisfies Record<RecipientField, string>;

const isUserMailboxContact = (contact: Contact) => !contact.isService;

const parseEmails = (value: string) => new Set(
  value
    .split(',')
    .map((item) => item.trim().toLowerCase())
    .filter(Boolean),
);

export const ContactPickerDialog: React.FC<ContactPickerDialogProps> = ({
  open,
  target,
  contacts,
  currentValue,
  onApply,
  onClose,
  title,
  description,
  contactFilter,
  noBackdrop,
}) => {
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (open) {
      setSearch('');
      setSelected(parseEmails(currentValue));
    }
  }, [currentValue, open]);

  const uniqueContacts = useMemo(() => {
    const byEmail = new Map<string, Contact>();
    contacts.forEach((contact) => {
      if (!isUserMailboxContact(contact)) return;
      if (contactFilter && !contactFilter(contact)) return;
      const email = contact.email.trim().toLowerCase();
      if (email && !byEmail.has(email)) byEmail.set(email, { ...contact, email });
    });
    return [...byEmail.values()].sort((a, b) => a.name.localeCompare(b.name) || a.email.localeCompare(b.email));
  }, [contacts, contactFilter]);

  const filteredContacts = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return uniqueContacts;
    return uniqueContacts.filter((contact) => [
      contact.name,
      contact.email,
      contact.organization,
      contact.department,
      contact.book,
      contact.kind ?? '',
    ].some((value) => (value ?? '').toLowerCase().includes(q)));
  }, [search, uniqueContacts]);

  if (!open) return null;

  const toggle = (email: string) => {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(email)) next.delete(email);
      else next.add(email);
      return next;
    });
  };

  const selectedCount = selected.size;

  const content = (
    <div className="wm-modal-panel contact-picker-dialog" role="dialog" aria-modal="true" aria-label={`Select ${fieldLabel[target]} recipients`} onMouseDown={(e) => e.stopPropagation()}>
        <div className="wm-modal-header">
          <div>
            <h2>{title ?? `Select contacts for ${fieldLabel[target]}`}</h2>
            <p>{description ?? 'Choose from Personal Contacts and Company Directory.'}</p>
          </div>
          <button type="button" className="btn btn-ghost" onClick={onClose}>Close</button>
        </div>

        <input
          className="input-base contact-picker-search"
          type="text"
          placeholder="Search name, email, organization..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          autoFocus
        />

        <div className="contact-picker-list">
          {filteredContacts.length === 0 && <div className="contact-picker-empty">No contacts found.</div>}
          {filteredContacts.map((contact) => {
            const email = contact.email.trim().toLowerCase();
            const checked = selected.has(email);
            return (
              <label key={email} className={`contact-picker-row ${checked ? 'selected' : ''}`}>
                <input type="checkbox" checked={checked} onChange={() => toggle(email)} />
                <div className="contact-picker-main">
                  <div className="contact-picker-name">{contact.name || contact.email}</div>
                  <div className="contact-picker-email">{contact.email}</div>
                  {(contact.organization || contact.department) && (
                    <div className="contact-picker-meta">{[contact.organization, contact.department].filter(Boolean).join(' · ')}</div>
                  )}
                </div>
                <div className="contact-picker-badges">
                  <span className="contact-picker-badge">{contact.book}</span>
                  {contact.kind && <span className="contact-picker-badge muted">{contact.kind}</span>}
                </div>
              </label>
            );
          })}
        </div>

        <div className="wm-modal-footer">
          <span>{selectedCount} selected</span>
          <div style={{ display: 'flex', gap: 8 }}>
            <button type="button" className="btn btn-outline" onClick={onClose}>Cancel</button>
            <button type="button" className="btn btn-primary" onClick={() => onApply([...selected])}>Add selected</button>
          </div>
        </div>
      </div>
  );

  if (noBackdrop) {
      return createPortal(
          <div style={{ position: 'fixed', inset: 0, zIndex: 1200, display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
            {content}
          </div>,
          document.body
      );
  }

  return createPortal(
    <div className="wm-modal-backdrop" role="presentation" onMouseDown={onClose}>
      {content}
    </div>,
    document.body
  );
};
