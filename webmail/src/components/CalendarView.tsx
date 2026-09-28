import React, { useMemo, useState } from 'react';
import type { CalendarEvent, CalendarInvitee, Contact, MailboxAccount } from '../types';
import { webmailClient } from './WebmailApiClient';

interface CalendarViewProps {
  events: CalendarEvent[];
  onEventsChanged: (events: CalendarEvent[]) => void;
  currentUserEmail?: string;
  contacts?: Contact[];
  mailboxAccounts?: MailboxAccount[];
}

interface EventFormState {
  title: string;
  startTime: string;
  endTime: string;
  location: string;
  organizer: string;
  status: 'confirmed' | 'tentative' | 'cancelled';
  visibility: 'private' | 'public';
  showAs: 'busy' | 'tentative' | 'free' | 'out_of_office';
  invitees: CalendarInvitee[];
  sendInvitations: boolean;
}

const emptyEvent: EventFormState = {
  title: '',
  startTime: '',
  endTime: '',
  location: '',
  organizer: '',
  status: 'confirmed',
  visibility: 'private',
  showAs: 'busy',
  invitees: [],
  sendInvitations: true,
};

export const CalendarView: React.FC<CalendarViewProps> = ({
  events,
  onEventsChanged,
  currentUserEmail = '',
  contacts = [],
  mailboxAccounts = [],
}) => {
  const [month, setMonth] = useState(() => new Date());
  const [form, setForm] = useState<EventFormState>({
    ...emptyEvent,
    organizer: currentUserEmail,
  });
  const [inviteeInput, setInviteeInput] = useState('');
  const [error, setError] = useState('');
  const [selectedEvent, setSelectedEvent] = useState<CalendarEvent | null>(null);

  const daysInMonth = useMemo(() => {
    const count = new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate();
    return Array.from({ length: count }, (_, i) => i + 1);
  }, [month]);

  const monthLabel = month.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });

  const reloadEvents = async () => {
    const res = await webmailClient.getCalendarEvents();
    onEventsChanged(res.data);
  };

  const addInvitee = (emailToAdd?: string) => {
    const email = (emailToAdd ?? inviteeInput).trim().toLowerCase();
    if (!email) return;
    if (form.invitees.some((i) => i.email.toLowerCase() === email)) {
      setInviteeInput('');
      return;
    }
    const matchedContact = contacts.find((c) => c.email.toLowerCase() === email);
    const displayName = matchedContact ? matchedContact.name : email.split('@')[0];
    setForm({
      ...form,
      invitees: [...form.invitees, { email, displayName, role: 'required' }],
    });
    setInviteeInput('');
  };

  const removeInvitee = (email: string) => {
    setForm({
      ...form,
      invitees: form.invitees.filter((i) => i.email.toLowerCase() !== email.toLowerCase()),
    });
  };

  const handleInviteeKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault();
      addInvitee();
    }
  };

  const saveEvent = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.title || !form.startTime || !form.endTime) return;
    try {
      await webmailClient.createCalendarEvent({
        ...form,
        organizer: form.organizer || currentUserEmail,
      });
      setForm({ ...emptyEvent, organizer: currentUserEmail });
      setInviteeInput('');
      setError('');
      await reloadEvents();
    } catch (err: any) {
      setError(err?.message ?? 'Failed to save calendar event.');
    }
  };

  const handleAcceptProposal = async (event: CalendarEvent, attendeeId: string) => {
    try {
      await webmailClient.acceptRescheduleProposal(event.id, attendeeId);
      await reloadEvents();
      setSelectedEvent(null);
    } catch (err: any) {
      setError(err?.message ?? 'Failed to accept reschedule proposal.');
    }
  };

  return (
    <div className="webmail-layout">
      {/* Calendar Ribbon */}
      <div className="wm-ribbon">
        <div className="wm-ribbon-group">
          <button
            type="button"
            className="btn btn-primary"
            style={{ padding: '6px 14px', fontSize: '14px' }}
            onClick={() =>
              setForm({
                ...emptyEvent,
                organizer: currentUserEmail,
                startTime: new Date().toISOString().slice(0, 16),
                endTime: new Date(Date.now() + 3600000).toISOString().slice(0, 16),
              })
            }
          >
            <img src="/images/icons/webmail/calendar.png" alt="" style={{ width: '14px', filter: 'brightness(0) invert(1)' }} />
            New Meeting
          </button>
        </div>
        <div className="wm-ribbon-group">
          <button type="button" className="wm-tool-btn" title="Month View"><img src="/images/icons/webmail/grid-view.png" alt="Month" /></button>
          <button type="button" className="wm-tool-btn" title="Week View"><img src="/images/icons/webmail/list-view.png" alt="Week" /></button>
          <button type="button" className="wm-tool-btn" title="Day View"><img src="/images/icons/webmail/list-view-bullets.png" alt="Day" /></button>
        </div>
      </div>

      {/* Main Calendar Content */}
      <div className="wm-body">
        {/* Sidebar */}
        <aside className="wm-sidebar">
          <div className="wm-nav-section" style={{ padding: '0 16px' }}>
            <div className="wm-nav-head" style={{ padding: '8px 0' }}>My Calendars</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', color: 'var(--deep-navy)', cursor: 'pointer' }}>
                <input type="checkbox" defaultChecked style={{ accentColor: 'var(--iris-violet)' }} />
                Primary (Work)
              </label>
              {mailboxAccounts.filter(a => a.kind === 'shared').map(sa => (
                <label key={sa.id} style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', color: 'var(--deep-navy)', cursor: 'pointer' }}>
                  <input type="checkbox" defaultChecked style={{ accentColor: 'var(--accent-ruby)' }} />
                  {sa.name || sa.address}
                </label>
              ))}
            </div>
          </div>
        </aside>

        <main className="wm-content">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
            <h1 style={{ fontSize: '2rem' }}>
              {monthLabel}
            </h1>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button className="btn btn-secondary" style={{ padding: '6px 12px', fontSize: '14px' }} onClick={() => setMonth(new Date())}>Today</button>
              <button className="btn btn-secondary" style={{ padding: '6px 12px', fontSize: '14px' }} onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() - 1, 1))}>&lt;</button>
              <button className="btn btn-secondary" style={{ padding: '6px 12px', fontSize: '14px' }} onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() + 1, 1))}>&gt;</button>
            </div>
          </div>

          <form onSubmit={saveEvent} className="card" style={{ display: 'grid', gap: '12px', marginBottom: '20px' }}>
            <h3 style={{ margin: 0 }}>Create Meeting</h3>
            {error && <div style={{ color: 'var(--danger-red)', fontSize: '13px' }}>{error}</div>}

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: '12px' }}>
              <input className="input-base" placeholder="Title" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
              <input className="input-base" placeholder="Location" value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} />
              <input className="input-base" type="datetime-local" value={form.startTime} onChange={(e) => setForm({ ...form, startTime: e.target.value })} />
              <input className="input-base" type="datetime-local" value={form.endTime} onChange={(e) => setForm({ ...form, endTime: e.target.value })} />
              <select className="input-base" value={form.visibility} onChange={(e) => setForm({ ...form, visibility: e.target.value as any })}>
                <option value="private">Private details</option>
                <option value="public">Public details</option>
              </select>
              <select className="input-base" value={form.showAs} onChange={(e) => setForm({ ...form, showAs: e.target.value as any })}>
                <option value="busy">Busy</option>
                <option value="tentative">Tentative</option>
                <option value="free">Free</option>
                <option value="out_of_office">Out of office</option>
              </select>
            </div>

            {/* Invitees section */}
            <div>
              <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px', color: 'var(--neutral-body)' }}>
                Invitees (Tenant members or external emails)
              </label>
              <div style={{ display: 'flex', gap: '8px', marginBottom: '8px' }}>
                <input
                  className="input-base"
                  list="contacts-datalist"
                  placeholder="Enter email and press Enter or click Add"
                  value={inviteeInput}
                  onChange={(e) => setInviteeInput(e.target.value)}
                  onKeyDown={handleInviteeKeyDown}
                  style={{ flex: 1 }}
                />
                <datalist id="contacts-datalist">
                  {contacts.map((c) => (
                    <option key={c.id} value={c.email}>
                      {c.name} ({c.book === 'directory' ? 'Directory' : 'Personal'})
                    </option>
                  ))}
                </datalist>
                <button type="button" className="btn btn-secondary" onClick={() => addInvitee()}>
                  Add Invitee
                </button>
              </div>

              {form.invitees.length > 0 && (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px', marginTop: '6px' }}>
                  {form.invitees.map((inv) => (
                    <span
                      key={inv.email}
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '6px',
                        padding: '3px 8px',
                        background: 'var(--surface-canvas)',
                        border: '1px solid var(--neutral-border)',
                        borderRadius: '16px',
                        fontSize: '12px',
                      }}
                    >
                      <span>{inv.displayName || inv.email}</span>
                      <button
                        type="button"
                        onClick={() => removeInvitee(inv.email)}
                        style={{
                          background: 'none',
                          border: 'none',
                          cursor: 'pointer',
                          color: 'var(--neutral-label)',
                          fontWeight: 'bold',
                          padding: 0,
                          lineHeight: 1,
                        }}
                      >
                        ×
                      </button>
                    </span>
                  ))}
                </div>
              )}
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '4px' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={form.sendInvitations}
                  onChange={(e) => setForm({ ...form, sendInvitations: e.target.checked })}
                  style={{ accentColor: 'var(--iris-violet)' }}
                />
                Send invitation emails to invitees
              </label>
            </div>

            <button type="submit" className="btn btn-primary" style={{ justifySelf: 'start', marginTop: '8px' }}>
              Save Meeting
            </button>
          </form>

          {/* Details Modal or Panel for Selected Event */}
          {selectedEvent && (
            <div className="card" style={{ marginBottom: '20px', borderLeft: '4px solid var(--iris-violet)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div>
                  <h3 style={{ margin: '0 0 8px 0' }}>{selectedEvent.title}</h3>
                  <p style={{ margin: '0 0 4px 0', fontSize: '13px', color: 'var(--neutral-body)' }}>
                    <strong>Time:</strong> {new Date(selectedEvent.startTime).toLocaleString()} - {new Date(selectedEvent.endTime).toLocaleString()}
                  </p>
                  <p style={{ margin: '0 0 4px 0', fontSize: '13px', color: 'var(--neutral-body)' }}>
                    <strong>Organizer:</strong> {selectedEvent.organizer || 'Organizer'}
                  </p>
                  {selectedEvent.location && (
                    <p style={{ margin: '0 0 4px 0', fontSize: '13px', color: 'var(--neutral-body)' }}>
                      <strong>Location:</strong> {selectedEvent.location}
                    </p>
                  )}
                </div>
                <button className="btn btn-secondary" style={{ padding: '4px 8px', fontSize: '12px' }} onClick={() => setSelectedEvent(null)}>
                  Close
                </button>
              </div>

              {selectedEvent.attendees && selectedEvent.attendees.length > 0 && (
                <div style={{ marginTop: '12px' }}>
                  <h4 style={{ margin: '0 0 8px 0', fontSize: '14px' }}>Attendees & RSVPs</h4>
                  <div style={{ display: 'grid', gap: '6px' }}>
                    {selectedEvent.attendees.map((att) => (
                      <div
                        key={att.id}
                        style={{
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          padding: '6px 10px',
                          background: 'var(--surface-canvas)',
                          borderRadius: '4px',
                          fontSize: '13px',
                        }}
                      >
                        <div>
                          <span>{att.displayName || att.email}</span>
                          <span style={{ marginLeft: '8px', fontSize: '11px', color: 'var(--neutral-label)', textTransform: 'uppercase' }}>
                            ({att.responseStatus})
                          </span>
                          {att.responseStatus === 'reschedule_proposed' && att.proposedStartTime && (
                            <div style={{ fontSize: '12px', color: 'var(--accent-ruby)', marginTop: '2px' }}>
                              Proposed: {new Date(att.proposedStartTime).toLocaleString()}
                              {att.proposalNote && ` - "${att.proposalNote}"`}
                            </div>
                          )}
                        </div>
                        {selectedEvent.organizer === currentUserEmail && att.responseStatus === 'reschedule_proposed' && (
                          <button
                            type="button"
                            className="btn btn-primary"
                            style={{ padding: '2px 8px', fontSize: '12px' }}
                            onClick={() => handleAcceptProposal(selectedEvent, att.id)}
                          >
                            Accept Proposal
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}

          <div style={{ background: 'var(--pure-white)', border: '1px solid var(--neutral-border)', borderRadius: 'var(--radius-lg)', overflow: 'hidden', boxShadow: 'var(--shadow-lvl1)' }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', background: 'var(--surface-canvas)', borderBottom: '1px solid var(--neutral-border)', textAlign: 'center', fontWeight: 500, fontSize: '14px', padding: '10px 0', color: 'var(--neutral-label)' }}>
              <div>Sun</div>
              <div>Mon</div>
              <div>Tue</div>
              <div>Wed</div>
              <div>Thu</div>
              <div>Fri</div>
              <div>Sat</div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gridAutoRows: '100px' }}>
              {daysInMonth.map((day) => {
                const currentDate = new Date(month.getFullYear(), month.getMonth(), day);
                const today = new Date();
                const isToday = currentDate.toDateString() === today.toDateString();
                const dayEvents = events.filter((e) => {
                  const start = new Date(e.startTime);
                  return start.getFullYear() === month.getFullYear() && start.getMonth() === month.getMonth() && start.getDate() === day;
                });

                return (
                  <div
                    key={day}
                    style={{
                      borderRight: '1px solid var(--neutral-border)',
                      borderBottom: '1px solid var(--neutral-border)',
                      padding: '8px',
                      background: isToday ? 'rgba(99, 91, 255, 0.04)' : 'transparent',
                    }}
                  >
                    <div style={{ fontWeight: isToday ? 700 : 400, color: isToday ? 'var(--iris-violet)' : 'var(--deep-navy)', fontSize: '14px', marginBottom: '4px' }}>
                      {day} {isToday && '•'}
                    </div>

                    {dayEvents.map((evt) => (
                      <div
                        key={evt.id}
                        onClick={() => setSelectedEvent(evt)}
                        style={{
                          background: 'var(--iris-violet)',
                          color: '#fff',
                          fontSize: '11px',
                          borderRadius: '3px',
                          padding: '2px 6px',
                          marginBottom: '2px',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                          cursor: 'pointer',
                        }}
                        title={`${evt.title} (${evt.organizer}) - click to view details`}
                      >
                        {evt.title}
                      </div>
                    ))}
                  </div>
                );
              })}
            </div>
          </div>
        </main>
      </div>
    </div>
  );
};
