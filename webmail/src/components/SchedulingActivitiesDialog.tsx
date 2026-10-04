import React, { useEffect, useMemo, useState } from 'react';
import type {
  AvailabilityCompare,
  CalendarEvent,
  CalendarInvitee,
  Contact,
  DirectoryParticipant,
} from '../types';
import { webmailClient } from './WebmailApiClient';
import { ContactPickerDialog } from './ContactPickerDialog';

interface SchedulingActivitiesDialogProps {
  currentUserEmail: string;
  currentUserId?: string;
  initialStartTime?: string;
  initialEndTime?: string;
  contacts?: Contact[];
  /** When present the dialog edits that event instead of creating a new one. */
  event?: CalendarEvent;
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}

type ShowAs = 'busy' | 'tentative' | 'free' | 'out_of_office';

const REMOTE = 'Remote';

const toLocalInput = (value: string): string => {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

// datetime-local values are minute-precision local strings with no zone; the API
// wants ISO-8601, so keep the string as-is and let the backend interpret it.
export const SchedulingActivitiesDialog: React.FC<SchedulingActivitiesDialogProps> = ({
  currentUserEmail,
  currentUserId = '',
  initialStartTime = '',
  initialEndTime = '',
  contacts = [],
  event,
  onClose,
  onSaved,
}) => {
  const editing = !!event;
  const [title, setTitle] = useState(event?.title ?? '');
  const [description, setDescription] = useState(event?.description ?? '');
  const [startTime, setStartTime] = useState(event ? toLocalInput(event.startTime) : initialStartTime);
  const [endTime, setEndTime] = useState(event ? toLocalInput(event.endTime) : initialEndTime);
  const [location, setLocation] = useState(event?.location || REMOTE);
  const [visibility, setVisibility] = useState<'private' | 'public'>(event?.visibility === 'public' ? 'public' : 'private');
  const [showAs, setShowAs] = useState<ShowAs>((event?.showAs as ShowAs) || 'busy');
  const [invitees, setInvitees] = useState<CalendarInvitee[]>(() =>
    (event?.attendees ?? []).map((att) => ({
      email: att.email,
      displayName: att.displayName || att.email,
      role: att.role || 'required',
    })),
  );
  const [externalInput, setExternalInput] = useState('');
  const [contactPickerOpen, setContactPickerOpen] = useState(false);
  const [sendInvitations, setSendInvitations] = useState(true);
  const [recurrenceFrequency, setRecurrenceFrequency] = useState<'none' | 'daily' | 'weekly' | 'monthly'>(
    (event?.recurrenceFrequency as any) ?? 'none',
  );
  const [recurrenceInterval, setRecurrenceInterval] = useState(event?.recurrenceInterval ?? 1);
  const [recurrenceUntil, setRecurrenceUntil] = useState(
    event?.recurrenceUntil ? event.recurrenceUntil.split('T')[0] : '',
  );

  const [directory, setDirectory] = useState<DirectoryParticipant[]>([]);
  const [comparison, setComparison] = useState<AvailabilityCompare | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const people = useMemo(() => directory.filter((d) => d.kind === 'user'), [directory]);
  const resources = useMemo(() => directory.filter((d) => d.kind === 'resource'), [directory]);

  // People and resources alike are tenant users holding a mailbox; only `kind`
  // separates a colleague from a bookable room. Matching on the whole directory lets
  // a room typed into the location field resolve to the id the API expects.
  const directoryByEmail = useMemo(() => {
    const map = new Map<string, DirectoryParticipant>();
    for (const d of directory) map.set(d.email.toLowerCase(), d);
    return map;
  }, [directory]);

  // Personal address-book entries are invitable too. Shared mailboxes, distribution
  // groups, and service accounts are excluded: a contact entry can carry any kind and
  // the API rejects the first two, while a room is booked through the location field
  // rather than by appearing in the invite list.
  const schedulingContacts = useMemo(() => {
    const own = currentUserEmail.trim().toLowerCase();
    const byEmail = new Map<string, Contact>();
    for (const contact of contacts) {
      const email = contact.email?.trim().toLowerCase();
      if (!email || !email.includes('@') || email === own) continue;
      // Personal entries may omit kind; directory entries must be user mailboxes.
      if (contact.isService || (contact.book === 'directory' ? contact.kind !== 'user' : contact.kind && contact.kind !== 'user')) continue;
      byEmail.set(email, { ...contact, email, book: contact.book === 'directory' ? 'directory' : 'personal' });
    }
    for (const participant of people) {
      if (participant.kind === 'resource') continue;
      const email = participant.email.trim().toLowerCase();
      if (!email || email === own) continue;
      byEmail.set(email, {
        id: participant.userId,
        name: participant.displayName || email,
        email,
        organization: '',
        book: 'directory',
        kind: 'user',
      });
    }
    return [...byEmail.values()];
  }, [contacts, people, currentUserEmail]);

  const applyContactSelection = (emails: string[]) => {
    const selected = new Map(schedulingContacts.map((contact) => [contact.email.toLowerCase(), contact]));
    setInvitees((current) => {
      const next = [...current];
      for (const rawEmail of emails) {
        const email = rawEmail.trim().toLowerCase();
        const contact = selected.get(email);
        if (!email || next.some((invitee) => invitee.email.toLowerCase() === email)) continue;
        next.push({ email, displayName: contact?.name || email, role: 'required' });
      }
      return next;
    });
    setContactPickerOpen(false);
  };

  useEffect(() => {
    webmailClient
      .getDirectoryParticipants()
      .then((res) => setDirectory(res.data))
      .catch(() => setDirectory([]));
  }, []);

  // Every invitee is classified as internal (a tenant user whose calendar we may read)
  // or external (no tenant id, so their free/busy is not ours to look at). The split
  // matters twice: only internal participants can be asked for availability, and the
  // external ones are reported as assumed available rather than silently omitted.
  const inviteeAvailability = useMemo(() => {
    const internal: Array<{ email: string; userId: string; name: string }> = [];
    const external: Array<{ email: string; name: string }> = [];
    const seen = new Set<string>();
    for (const inv of invitees) {
      const email = inv.email.toLowerCase();
      if (seen.has(email)) continue;
      seen.add(email);
      const match = directoryByEmail.get(email);
      if (match) internal.push({ email, userId: match.userId, name: match.displayName });
      else external.push({ email, name: inv.displayName || inv.email });
    }
    return { internal, external };
  }, [invitees, directoryByEmail]);

  // Editing an event must not report that event as a conflict with itself: the
  // organizer's own copy of the meeting being edited is in the same window and would
  // otherwise show up as busy for the whole duration of the slot.
  const selfInterval = useMemo(() => {
    if (!event) return null;
    const start = new Date(event.startTime).toISOString();
    const end = new Date(event.endTime).toISOString();
    return { start, end };
  }, [event]);

  const isSelfInterval = (start: string, end: string) =>
    !!selfInterval &&
    new Date(start).toISOString() === selfInterval.start &&
    new Date(end).toISOString() === selfInterval.end;

  // Re-run availability comparison whenever the time or participant set changes.
  // The organizer is included so their own calendar is checked too, and every invitee
  // is resolved by email rather than only the ones that came from the directory.
  useEffect(() => {
    if (!startTime || !endTime) {
      setComparison(null);
      return;
    }
    const ids = [...inviteeAvailability.internal.map((i) => i.userId)];
    if (currentUserId) ids.push(currentUserId);
    const unique = [...new Set(ids)];
    if (unique.length === 0) {
      setComparison(null);
      return;
    }
    let cancelled = false;
    webmailClient
      .compareAvailability(startTime, endTime, unique)
      .then((res) => {
        if (!cancelled) setComparison(res.data);
      })
      .catch(() => {
        if (!cancelled) setComparison(null);
      });
    return () => {
      cancelled = true;
    };
  }, [startTime, endTime, inviteeAvailability, currentUserId]);

  const visibleComparison = useMemo(() => {
    if (!comparison) return null;
    if (!selfInterval || !currentUserId) return comparison;
    const participants = comparison.participants.map((p) =>
      p.userId === currentUserId
        ? { ...p, busy: p.busy.filter((b) => !isSelfInterval(b.startTime, b.endTime)) }
        : p,
    );
    const conflicts = comparison.conflicts.filter(
      (c) => !(c.participantId === currentUserId && isSelfInterval(c.startTime, c.endTime)),
    );
    return { ...comparison, participants, conflicts, allAvailable: conflicts.length === 0 };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [comparison, selfInterval, currentUserId]);

  // A room is booked by inviting it, not by naming it in the location string: the API
  // auto-accepts a service-account invitee and mirrors the meeting onto the room calendar.
  // Selecting a resource therefore has to add it as an invitee, and switching away must
  // take it back off.
  const handleLocationChange = (value: string) => {
    const previous = resources.find((r) => r.email === location);
    const next = resources.find((r) => r.email === value);
    setLocation(value);

    if (next) {
      setInvitees((prev) =>
        prev.some((i) => i.email.toLowerCase() === next.email.toLowerCase())
          ? prev
          : [...prev, { email: next.email, displayName: next.displayName, role: 'required' }],
      );
    }

    if (previous && previous.email !== value) {
      setInvitees((prev) => prev.filter((i) => i.email.toLowerCase() !== previous.email.toLowerCase()));
    }
  };

  const addExternal = () => {
    const email = externalInput.trim().toLowerCase();
    if (!email || !email.includes('@')) return;
    if (invitees.some((i) => i.email.toLowerCase() === email)) {
      setExternalInput('');
      return;
    }
    setInvitees((prev) => [...prev, { email, displayName: email.split('@')[0], role: 'required' }]);
    setExternalInput('');
  };

  const removeInvitee = (email: string) => {
    setInvitees((prev) => prev.filter((i) => i.email.toLowerCase() !== email.toLowerCase()));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title || !startTime || !endTime) {
      setError('Title, start, and end are required.');
      return;
    }
    setSaving(true);
    setError('');
    try {
      const payload: Omit<CalendarEvent, 'id'> = {
        title,
        description: description || null,
        startTime,
        endTime,
        location: location || REMOTE,
        organizer: currentUserEmail,
        status: 'confirmed',
        visibility,
        showAs,
        invitees,
        sendInvitations,
        recurrenceFrequency: recurrenceFrequency === 'none' ? undefined : recurrenceFrequency,
        recurrenceInterval: recurrenceInterval,
        recurrenceUntil: recurrenceUntil || null,
      };
      // The update path re-sends the invitation when send_invitations is not false,
      // which is exactly the "edit and send update to the people invited" behaviour.
      if (event) {
        await webmailClient.updateCalendarEvent(event.id, payload);
      } else {
        await webmailClient.createCalendarEvent(payload);
      }
      await onSaved();
      onClose();
    } catch (err: any) {
      setError(err?.message ?? 'Failed to save activity.');
    } finally {
      setSaving(false);
    }
  };

  return (
      <div className="wm-modal-backdrop" onClick={contactPickerOpen ? undefined : onClose}>
        <div className="wm-modal-panel" onClick={(e) => e.stopPropagation()}>
          <div className="wm-modal-header">
            <div>
              <h2>{editing ? 'Edit Activity' : 'Scheduling Activities'}</h2>
              <p>
                {editing
                  ? 'Change the meeting and send the update to everyone invited.'
                  : 'Plan a meeting, book a room, and compare availability.'}
              </p>
            </div>
            <button type="button" className="btn btn-secondary" style={{ padding: '4px 10px' }} onClick={onClose}>
              Close
            </button>
          </div>

          <form onSubmit={handleSubmit} style={{ overflowY: 'auto', padding: '16px 18px', display: 'grid', gap: '12px' }}>
          {error && <div style={{ color: 'var(--accent-ruby)', fontSize: '13px' }}>{error}</div>}

          <input
            className="input-base"
            placeholder="Title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            required
          />
          <textarea
            className="input-base"
            placeholder="Agenda / description"
            rows={3}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
            <label style={{ fontSize: '12px', color: 'var(--neutral-label)' }}>
              Start
              <input
                className="input-base"
                type="datetime-local"
                value={startTime}
                onChange={(e) => setStartTime(e.target.value)}
                required
              />
            </label>
            <label style={{ fontSize: '12px', color: 'var(--neutral-label)' }}>
              End
              <input
                className="input-base"
                type="datetime-local"
                value={endTime}
                onChange={(e) => setEndTime(e.target.value)}
                required
              />
            </label>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px' }}>
            <label style={{ fontSize: '12px', color: 'var(--neutral-label)' }}>
              Location / resource
              <select className="input-base" value={location} onChange={(e) => handleLocationChange(e.target.value)}>
                <option value={REMOTE}>{REMOTE}</option>
                {resources.map((r) => (
                  <option key={r.userId} value={r.email}>
                    {r.displayName}
                  </option>
                ))}
              </select>
            </label>
            <label style={{ fontSize: '12px', color: 'var(--neutral-label)' }}>
              Details
              <select className="input-base" value={visibility} onChange={(e) => setVisibility(e.target.value as any)}>
                <option value="private">Private</option>
                <option value="public">Public</option>
              </select>
            </label>
            <label style={{ fontSize: '12px', color: 'var(--neutral-label)' }}>
              Show as
              <select className="input-base" value={showAs} onChange={(e) => setShowAs(e.target.value as ShowAs)}>
                <option value="busy">Busy</option>
                <option value="tentative">Tentative</option>
                <option value="free">Free</option>
                <option value="out_of_office">Out of office</option>
              </select>
            </label>
          </div>

          <div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '6px' }}>
              <div style={{ fontSize: '13px', fontWeight: 500 }}>Invite people</div>
              <button type="button" className="btn btn-secondary" onClick={() => setContactPickerOpen(true)}>
                Add people
              </button>
            </div>
            <div style={{ fontSize: '12px', color: 'var(--neutral-label)' }}>
              Choose from Personal Contacts and Company Directory. Rooms, shared mailboxes, groups, and service accounts are excluded.
            </div>
          </div>

          {/* External invitees */}
          <div style={{ display: 'flex', gap: '8px' }}>
            <input
              className="input-base"
              style={{ flex: 1 }}
              placeholder="External email address"
              value={externalInput}
              onChange={(e) => setExternalInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  addExternal();
                }
              }}
            />
            <button type="button" className="btn btn-secondary" onClick={addExternal}>
              Add
            </button>
          </div>

          {invitees.length > 0 && (
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px' }}>
              {invitees.map((inv) => (
                <span key={inv.email} className="contact-picker-badge">
                  {inv.displayName || inv.email}
                  <button
                    type="button"
                    onClick={() => removeInvitee(inv.email)}
                    style={{ background: 'none', border: 'none', cursor: 'pointer', marginLeft: '6px', fontWeight: 'bold' }}
                  >
                    ×
                  </button>
                </span>
              ))}
            </div>
          )}

          {/* Repeat controls */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '8px' }}>
            <label style={{ fontSize: '12px', color: 'var(--neutral-label)' }}>
              Repeat
              <select
                className="input-base"
                value={recurrenceFrequency}
                onChange={(e) => setRecurrenceFrequency(e.target.value as any)}
              >
                <option value="none">Does not repeat</option>
                <option value="daily">Daily</option>
                <option value="weekly">Weekly</option>
                <option value="monthly">Monthly</option>
              </select>
            </label>
            {recurrenceFrequency !== 'none' && (
              <>
                <label style={{ fontSize: '12px', color: 'var(--neutral-label)' }}>
                  Interval
                  <input
                    className="input-base"
                    type="number"
                    min="1"
                    value={recurrenceInterval}
                    onChange={(e) => setRecurrenceInterval(Number(e.target.value))}
                  />
                </label>
                <label style={{ fontSize: '12px', color: 'var(--neutral-label)' }}>
                  Until
                  <input
                    className="input-base"
                    type="date"
                    value={recurrenceUntil}
                    onChange={(e) => setRecurrenceUntil(e.target.value)}
                  />
                </label>
              </>
            )}
          </div>

          {/* Availability comparison: Outlook-style row grid */}
          {visibleComparison && (
            <div style={{ border: '1px solid var(--neutral-border)', borderRadius: 'var(--radius-md)', padding: '10px', background: 'var(--surface-canvas)' }}>
              <div style={{ fontSize: '13px', fontWeight: 600, marginBottom: '8px', color: visibleComparison.allAvailable ? 'var(--iris-violet)' : 'var(--accent-ruby)' }}>
                {visibleComparison.allAvailable ? 'All participants are available.' : `${visibleComparison.conflicts.length} conflict(s).`}
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '100px 1fr', gap: '8px', fontSize: '12px' }}>
                {visibleComparison.participants.map((p) => (
                  <React.Fragment key={p.userId}>
                    <div style={{ fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{p.displayName || p.email}</div>
                    <div style={{ position: 'relative', height: '20px', background: 'var(--neutral-faint)', borderRadius: '2px' }}>
                      {p.busy.map((b, i) => {
                        const start = new Date(b.startTime);
                        const end = new Date(b.endTime);
                        const dayStart = new Date(startTime).setHours(0, 0, 0, 0);
                        const dayEnd = new Date(startTime).setHours(23, 59, 59, 999);
                        const left = Math.max(0, (start.getTime() - dayStart) / (dayEnd - dayStart) * 100);
                        const width = Math.min(100 - left, (end.getTime() - start.getTime()) / (dayEnd - dayStart) * 100);
                        return <div key={i} style={{ position: 'absolute', left: `${left}%`, width: `${width}%`, height: '100%', background: 'var(--accent-ruby)', borderRadius: '2px' }} title={b.title || 'Busy'} />;
                      })}
                    </div>
                  </React.Fragment>
                ))}
              </div>
              {inviteeAvailability.external.length > 0 && (
                <div style={{ fontSize: '12px', marginTop: '8px', color: 'var(--neutral-label)' }}>
                  {inviteeAvailability.external.map((e) => e.name).join(', ')}: external, assumed available.
                </div>
              )}
            </div>
          )}

          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px' }}>
            <input
              type="checkbox"
              checked={sendInvitations}
              onChange={(e) => setSendInvitations(e.target.checked)}
              style={{ accentColor: 'var(--iris-violet)' }}
            />
            {editing ? 'Send the update to invited people' : 'Send invitation emails to invitees'}
          </label>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '4px' }}>
            <button type="button" className="btn btn-secondary" onClick={onClose} disabled={saving}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" disabled={saving}>
              {saving ? 'Saving…' : editing ? 'Save & send update' : 'Save activity'}
            </button>
          </div>
        </form>
      </div>
      {contactPickerOpen && (
        <ContactPickerDialog
          open={contactPickerOpen}
          target="to"
          contacts={schedulingContacts}
          contactFilter={(contact) => !contact.isService && (contact.book === 'directory' ? contact.kind === 'user' : !contact.kind || contact.kind === 'user')}
          currentValue={invitees.map((invitee) => invitee.email).join(', ')}
          onApply={applyContactSelection}
          onClose={() => setContactPickerOpen(false)}
          title="Add meeting invitees"
          description="Choose from Personal Contacts and Company Directory. Shared mailboxes, groups, service accounts, and rooms are hidden."
          noBackdrop={true}
        />
      )}
    </div>
  );
};
