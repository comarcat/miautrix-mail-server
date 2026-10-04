import React, { useEffect, useMemo, useRef, useState } from 'react';
import type { CalendarEvent, Contact, DirectoryParticipant, MailboxAccount, Subscription } from '../types';
import { webmailClient } from './WebmailApiClient';
import { SchedulingActivitiesDialog } from './SchedulingActivitiesDialog';
import { CalendarEventDialog } from './CalendarEventDialog';
import { CalendarRsvpDialog } from './CalendarRsvpDialog';
import { CalendarEventContextMenu } from './CalendarEventContextMenu';
import { WORK_HOURS_CHANGED_EVENT, readWorkHours } from './workHours';

interface CalendarViewProps {
  events: CalendarEvent[];
  onEventsChanged: (events: CalendarEvent[]) => void;
  currentUserEmail?: string;
  currentUserId?: string;
  currentUserRoles?: string[];
  contacts?: Contact[];
  mailboxAccounts?: MailboxAccount[];
}

// Working hours are a display preference: the grid always draws all 24 hours so an
// out-of-hours meeting stays visible and bookable, and the hours outside the window
// are only dimmed.

type ViewMode = 'month' | 'week' | 'day';

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const HOURS = Array.from({ length: 24 }, (_, i) => i);

// The time grid is drawn in 30-minute slots: two per hour, one per drag-stop.
const SLOTS_PER_HOUR = 2;
const SLOT_MINUTES = 30;
const SLOT_HEIGHT = 24;
const HALVES = Array.from({ length: SLOTS_PER_HOUR }, (_, i) => i);

// The start of a slot on a given day.
const slotStart = (day: Date, slot: number): Date => {
  const d = new Date(day);
  d.setHours(Math.floor(slot / SLOTS_PER_HOUR), (slot % SLOTS_PER_HOUR) * SLOT_MINUTES, 0, 0);
  return d;
};

// Format a Date as a datetime-local string (local time, minute precision).
const toLocalInput = (d: Date): string => {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const startOfWeek = (d: Date): Date => {
  const s = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  s.setDate(s.getDate() - s.getDay());
  return s;
};

const sameDay = (a: Date, b: Date) => a.toDateString() === b.toDateString();

export const CalendarView: React.FC<CalendarViewProps> = ({
  events,
  onEventsChanged,
  currentUserEmail = '',
  currentUserId = '',
  currentUserRoles = [],
  contacts = [],
  mailboxAccounts = [],
}) => {
  const [anchorDate, setAnchorDate] = useState(() => new Date());
  const [viewMode, setViewMode] = useState<ViewMode>('month');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogStart, setDialogStart] = useState('');
  const [dialogEnd, setDialogEnd] = useState('');
  const [editingEvent, setEditingEvent] = useState<CalendarEvent | null>(null);
  const [selectedEvent, setSelectedEvent] = useState<CalendarEvent | null>(null);
  const [rsvpEvent, setRsvpEvent] = useState<CalendarEvent | null>(null);
  const [error, setError] = useState('');
  const [drag, setDrag] = useState<{ day: Date; anchor: number; focus: number } | null>(null);

  const [directory, setDirectory] = useState<DirectoryParticipant[]>([]);
  const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
  const [hiddenCalendars, setHiddenCalendars] = useState<Set<string>>(new Set());

  const [tooltip, setTooltip] = useState<{ x: number; y: number; event: CalendarEvent } | null>(null);
  const [contextMenu, setContextMenu] = useState<{ x: number; y: number; event: CalendarEvent } | null>(null);
  const [workHours, setWorkHours] = useState(readWorkHours);

  const gridRef = useRef<HTMLDivElement | null>(null);

  const isOwnerOrAdmin = currentUserRoles.includes('owner') || currentUserRoles.includes('admin');

  const loadSidebar = async () => {
    try {
      const [dir, subs] = await Promise.all([
        webmailClient.getDirectoryParticipants(),
        webmailClient.getSubscriptions(),
      ]);
      setDirectory(dir.data);
      setSubscriptions(subs.data);
    } catch {
      /* sidebar is best-effort */
    }
  };

  useEffect(() => {
    void loadSidebar();
  }, []);

  // Working hours live in Settings & Rules; the settings page dispatches this event
  // rather than the calendar polling localStorage.
  useEffect(() => {
    const onWorkHoursChanged = () => setWorkHours(readWorkHours());
    window.addEventListener(WORK_HOURS_CHANGED_EVENT, onWorkHoursChanged);
    return () => window.removeEventListener(WORK_HOURS_CHANGED_EVENT, onWorkHoursChanged);
  }, []);

  // Bring the work-start hour to the top of the grid. The slot is SLOT_HEIGHT tall,
  // and slot 0 begins after the sticky day header.
  useEffect(() => {
    const el = gridRef.current;
    if (!el) return;
    el.scrollTop = workHours.start * SLOT_HEIGHT;
  }, [viewMode, workHours.start]);

  const reloadEvents = async () => {
    const res = await webmailClient.getCalendarEvents();
    onEventsChanged(res.data);
  };

  const openDialog = (start: Date, end: Date) => {
    setEditingEvent(null);
    setDialogStart(toLocalInput(start));
    setDialogEnd(toLocalInput(end));
    setDialogOpen(true);
  };

  // Editing reuses the scheduling dialog in edit mode: same fields, same resource
  // booking path, but it PUTs the existing event instead of creating a new one.
  const openEditDialog = (event: CalendarEvent) => {
    setEditingEvent(event);
    setDialogStart(toLocalInput(new Date(event.startTime)));
    setDialogEnd(toLocalInput(new Date(event.endTime)));
    setSelectedEvent(null);
    setContextMenu(null);
    setDialogOpen(true);
  };

  const handleResendInvitations = async (event: CalendarEvent) => {
    setContextMenu(null);
    try {
      await webmailClient.resendInvitations(event.id);
      await reloadEvents();
    } catch (err: any) {
      setError(err?.message ?? 'Failed to resend invitations.');
    }
  };

  const openDayCell = (date: Date) => {
    const start = new Date(date);
    start.setHours(9, 0, 0, 0);
    const end = new Date(start);
    end.setHours(10, 0, 0, 0);
    openDialog(start, end);
  };

  // A drag runs from pointer-down to pointer-up. A plain click is a drag of zero
  // length, which is a 30-minute meeting — the smallest slot the grid draws.
  const beginDrag = (day: Date, slot: number) => (e: React.PointerEvent) => {
    if (e.button !== 0) return;
    e.preventDefault();
    // Release the implicit touch capture so pointer-enter keeps firing on the cells
    // the finger actually passes over.
    if (e.currentTarget.hasPointerCapture?.(e.pointerId)) {
      e.currentTarget.releasePointerCapture(e.pointerId);
    }
    setDrag({ day, anchor: slot, focus: slot });
  };

  const extendDrag = (day: Date, slot: number) => () => {
    if (!drag || !sameDay(day, drag.day)) return;
    if (drag.focus !== slot) setDrag({ ...drag, focus: slot });
  };

  const selectionBounds = (day: Date): { from: number; to: number } | null => {
    if (!drag || !sameDay(day, drag.day)) return null;
    return { from: Math.min(drag.anchor, drag.focus), to: Math.max(drag.anchor, drag.focus) };
  };

  useEffect(() => {
    if (!drag) return;
    const finish = () => {
      const from = Math.min(drag.anchor, drag.focus);
      const to = Math.max(drag.anchor, drag.focus);
      setDrag(null);
      openDialog(slotStart(drag.day, from), slotStart(drag.day, to + 1));
    };
    const cancel = () => setDrag(null);
    window.addEventListener('pointerup', finish);
    window.addEventListener('pointercancel', cancel);
    return () => {
      window.removeEventListener('pointerup', finish);
      window.removeEventListener('pointercancel', cancel);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [drag]);

  const shift = (delta: number) => {
    const next = new Date(anchorDate);
    if (viewMode === 'month') next.setMonth(next.getMonth() + delta);
    else if (viewMode === 'week') next.setDate(next.getDate() + delta * 7);
    else next.setDate(next.getDate() + delta);
    setAnchorDate(next);
  };

  const headerLabel = useMemo(() => {
    if (viewMode === 'month') {
      return anchorDate.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
    }
    if (viewMode === 'week') {
      const s = startOfWeek(anchorDate);
      const e = new Date(s);
      e.setDate(s.getDate() + 6);
      return `${s.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })} – ${e.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}`;
    }
    return anchorDate.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' });
  }, [anchorDate, viewMode]);

  const eventsOn = (date: Date) =>
    events
      .filter((e) => {
        if (!sameDay(new Date(e.startTime), date)) return false;
        const isOwnEvent = e.isOwn !== false;
        if (!isOwnEvent && e.organizer && hiddenCalendars.has(e.organizer)) return false;

        // Filter out service account/resource calendars, but never hide the user's own invited/accepted copy.
        const participant = directory.find(d => d.email === e.organizer);
        if (!isOwnEvent && participant && participant.kind === 'resource') return false;

        return true;
      })
      .sort((a, b) => a.startTime.localeCompare(b.startTime));

  const eventLayoutsForDay = (date: Date) => {
    const entries = eventsOn(date).map((event) => {
      const start = new Date(event.startTime);
      const end = new Date(event.endTime);
      const startMinutes = start.getHours() * 60 + start.getMinutes();
      const endMinutes = Math.max(startMinutes + 1, Math.min(24 * 60, end.getHours() * 60 + end.getMinutes()));
      return { event, startMinutes, endMinutes, lane: 0 };
    });
    const active: typeof entries = [];
    let laneCount = 1;
    for (const entry of entries) {
      for (let index = active.length - 1; index >= 0; index -= 1) {
        if (active[index].endMinutes <= entry.startMinutes) active.splice(index, 1);
      }
      const occupied = new Set(active.map((item) => item.lane));
      while (occupied.has(entry.lane)) entry.lane += 1;
      active.push(entry);
      laneCount = Math.max(laneCount, entry.lane + 1);
    }
    return entries.map((entry) => ({ ...entry, laneCount }));
  };

  const handleDelete = async (event: CalendarEvent) => {
    setContextMenu(null);
    try {
      await webmailClient.deleteCalendarEvent(event.id);
      setSelectedEvent(null);
      await reloadEvents();
    } catch (err: any) {
      setError(err?.message ?? 'Failed to delete activity.');
    }
  };

  const handleAcceptProposal = async (event: CalendarEvent, attendeeId: string) => {
    try {
      await webmailClient.acceptRescheduleProposal(event.id, attendeeId);
      setSelectedEvent(null);
      await reloadEvents();
    } catch (err: any) {
      setError(err?.message ?? 'Failed to accept proposal.');
    }
  };

  const handleDeclineProposal = async (event: CalendarEvent, attendeeId: string) => {
    try {
      await webmailClient.declineRescheduleProposal(event.id, attendeeId);
      setSelectedEvent(null);
      await reloadEvents();
    } catch (err: any) {
      setError(err?.message ?? 'Failed to decline proposal.');
    }
  };

  const handleRsvp = async (event: CalendarEvent, response: string, note?: string, proposedStartTime?: string, proposedEndTime?: string) => {
    try {
      await webmailClient.rsvpEvent(event.id, {
        response,
        note,
        proposed_start_time: proposedStartTime,
        proposed_end_time: proposedEndTime,
      });
      setRsvpEvent(null);
      await reloadEvents();
    } catch (err: any) {
      setError(err?.message ?? 'Failed to RSVP.');
    }
  };

  const addColleague = async (userId: string) => {
    try {
      await webmailClient.addSubscription(userId);
      await loadSidebar();
      await reloadEvents();
    } catch (err: any) {
      setError(err?.message ?? 'Failed to add calendar.');
    }
  };

  const removeColleague = async (targetUserId: string) => {
    try {
      await webmailClient.deleteSubscription(targetUserId);
      await loadSidebar();
      await reloadEvents();
    } catch (err: any) {
      setError(err?.message ?? 'Failed to remove calendar.');
    }
  };

  const subscribedIds = new Set(subscriptions.map((s) => s.userId));
  const availableColleagues = directory.filter((d) => d.kind === 'user' && !subscribedIds.has(d.userId));

  const eventLabel = (evt: CalendarEvent) =>
    evt.isOwn === false && evt.visibility !== 'public' && !isOwnerOrAdmin ? 'Busy' : evt.title;

  const eventChip = (evt: CalendarEvent, style: React.CSSProperties = {}) => {
    const label = eventLabel(evt);
    const myAttendee = evt.attendees?.find(a => a.email.toLowerCase() === currentUserEmail.toLowerCase());
    const needsAction = myAttendee?.responseStatus === 'needs_action';

    return (
      <div
        key={evt.id}
        onClick={(e) => {
          e.stopPropagation();
          setContextMenu(null);
          setSelectedEvent(evt);
        }}
        onContextMenu={(e) => {
          // Right-click opens edit/resend/delete instead of the browser menu.
          e.preventDefault();
          e.stopPropagation();
          setTooltip(null);
          setContextMenu({ x: e.clientX, y: e.clientY, event: evt });
        }}
        onMouseEnter={(e) => {
          const rect = e.currentTarget.getBoundingClientRect();
          setTooltip({ x: rect.left, y: rect.bottom, event: evt });
        }}
        onMouseLeave={() => setTooltip((prev) => (prev?.event.id === evt.id ? null : prev))}
        style={{
          background: needsAction ? 'var(--accent-ruby)' : (evt.status === 'tentative' ? 'var(--accent-ruby)' : evt.isOwn === false ? 'var(--neutral-label)' : 'var(--iris-violet)'),
          backgroundImage: needsAction ? 'linear-gradient(45deg, rgba(255,255,255,.15) 25%, transparent 25%, transparent 50%, rgba(255,255,255,.15) 50%, rgba(255,255,255,.15) 75%, transparent 75%, transparent)' : undefined,
          backgroundSize: needsAction ? '40px 40px' : undefined,
          color: '#fff',
          fontSize: '11px',
          borderRadius: '3px',
          padding: '2px 6px',
          marginBottom: '2px',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          cursor: 'pointer',
          pointerEvents: drag ? 'none' : undefined,
          ...style,
        }}
      >
        {new Date(evt.startTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}  {label}
        {needsAction && <span style={{ marginLeft: '4px', fontWeight: 'bold' }}>!</span>}
      </div>
    );
  };

  // Hover card. Fixed-positioned so a chip clipped by its slot still gets a full
  // tooltip; it reports the same redaction the chip shows.
  const renderTooltip = () => {
    if (!tooltip || drag) return null;
    const { event } = tooltip;
    const visible = event.isOwn !== false || event.visibility === 'public' || isOwnerOrAdmin;
    return (
      <div
        role="tooltip"
        style={{
          position: 'fixed',
          top: Math.min(tooltip.y + 6, window.innerHeight - 96),
          left: Math.min(tooltip.x, window.innerWidth - 280),
          zIndex: 1050,
          width: '260px',
          background: 'var(--surface-card)',
          border: '1px solid var(--neutral-border)',
          borderRadius: 'var(--radius-md)',
          boxShadow: 'var(--shadow-lvl3)',
          padding: '8px 10px',
          pointerEvents: 'none',
          fontSize: '12px',
          color: 'var(--deep-navy)',
        }}
      >
        <div style={{ fontWeight: 600, marginBottom: '2px' }}>{eventLabel(event)}</div>
        <div style={{ color: 'var(--neutral-body)' }}>
          {new Date(event.startTime).toLocaleString()} – {new Date(event.endTime).toLocaleTimeString()}
        </div>
        {visible && event.location && <div style={{ color: 'var(--neutral-label)' }}>{event.location}</div>}
      </div>
    );
  };

  const renderMonth = () => {
    const year = anchorDate.getFullYear();
    const month = anchorDate.getMonth();
    const firstDay = new Date(year, month, 1).getDay();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const cells: Array<Date | null> = [
      ...Array.from({ length: firstDay }, () => null),
      ...Array.from({ length: daysInMonth }, (_, i) => new Date(year, month, i + 1)),
    ];
    return (
      <div style={{ background: 'var(--pure-white)', border: '1px solid var(--neutral-border)', borderRadius: 'var(--radius-lg)', overflow: 'hidden' }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', background: 'var(--surface-canvas)', borderBottom: '1px solid var(--neutral-border)', textAlign: 'center', fontWeight: 500, fontSize: '14px', padding: '10px 0', color: 'var(--neutral-label)' }}>
          {WEEKDAYS.map((d) => (
            <div key={d}>{d}</div>
          ))}
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gridAutoRows: '110px' }}>
          {cells.map((date, idx) => {
            if (!date) return <div key={`empty-${idx}`} style={{ borderRight: '1px solid var(--neutral-border)', borderBottom: '1px solid var(--neutral-border)' }} />;
            const isToday = sameDay(date, new Date());
            const isWorkDay = workHours.workDays.includes(date.getDay());
            return (
              <div
                key={date.toISOString()}
                onClick={() => openDayCell(date)}
                style={{
                  borderRight: '1px solid var(--neutral-border)',
                  borderBottom: '1px solid var(--neutral-border)',
                  padding: '6px',
                  cursor: 'pointer',
                  background: isToday
                    ? 'rgba(99, 91, 255, 0.04)'
                    : isWorkDay
                      ? 'transparent'
                      : 'rgba(0, 0, 0, 0.03)',
                  overflow: 'hidden',
                }}
              >
                <div style={{ fontWeight: isToday ? 700 : 400, color: isToday ? 'var(--iris-violet)' : 'var(--deep-navy)', fontSize: '14px', marginBottom: '4px' }}>
                  {date.getDate()}
                </div>
                {eventsOn(date).map((event) => eventChip(event))}
              </div>
            );
          })}
        </div>
      </div>
    );
  };

  const renderTimeGrid = (days: Date[]) => (
    <div
      ref={gridRef}
      data-testid="calendar-time-grid"
      style={{ background: 'var(--pure-white)', border: '1px solid var(--neutral-border)', borderRadius: 'var(--radius-lg)', overflow: 'auto', maxHeight: '70vh', userSelect: 'none', touchAction: 'none' }}
    >
      <div style={{ display: 'grid', gridTemplateColumns: `60px repeat(${days.length}, 1fr)`, position: 'sticky', top: 0, background: 'var(--surface-canvas)', borderBottom: '1px solid var(--neutral-border)', zIndex: 3 }}>
        <div />
        {days.map((d) => {
          const isToday = sameDay(d, new Date());
          const isWorkDay = workHours.workDays.includes(d.getDay());
          return (
            <div key={d.toISOString()} style={{ textAlign: 'center', padding: '8px 0', fontSize: '13px', fontWeight: 500, color: isToday ? 'var(--iris-violet)' : 'var(--neutral-label)', background: isWorkDay ? undefined : 'rgba(0, 0, 0, 0.03)' }}>
              {d.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' })}
            </div>
          );
        })}
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: `60px repeat(${days.length}, 1fr)` }}>
        <div>
          {HOURS.map((hour) => (
            <div key={hour} style={{ height: `${SLOT_HEIGHT * SLOTS_PER_HOUR}px`, fontSize: '11px', color: 'var(--neutral-label)', padding: '2px 6px', textAlign: 'right', borderBottom: '1px solid var(--neutral-border)' }}>
              {String(hour).padStart(2, '0')}:00
            </div>
          ))}
        </div>
        {days.map((d) => {
          const selection = selectionBounds(d);
          const isWorkDay = workHours.workDays.includes(d.getDay());
          return (
            <div key={d.toISOString()} style={{ position: 'relative', borderLeft: '1px solid var(--neutral-border)' }}>
              {HOURS.flatMap((hour) => HALVES.map((half) => {
                const slot = hour * SLOTS_PER_HOUR + half;
                const selected = !!selection && slot >= selection.from && slot <= selection.to;
                const withinWorkHours = isWorkDay && slot >= workHours.start && slot < workHours.end;
                return (
                  <div
                    key={slot}
                    onPointerDown={beginDrag(d, slot)}
                    onPointerEnter={extendDrag(d, slot)}
                    data-work-hours={withinWorkHours ? 'in' : 'out'}
                    style={{
                      height: `${SLOT_HEIGHT}px`,
                      cursor: 'pointer',
                      borderBottom: half === 1 ? '1px solid var(--neutral-border)' : '1px dashed var(--neutral-border)',
                      background: selected
                        ? 'rgba(99, 91, 255, 0.18)'
                        : withinWorkHours
                          ? 'transparent'
                          : 'rgba(0, 0, 0, 0.03)',
                    }}
                  />
                );
              }))}
              {eventLayoutsForDay(d).map(({ event, startMinutes, endMinutes, lane, laneCount }) => {
                const top = (startMinutes / SLOT_MINUTES) * SLOT_HEIGHT;
                const height = Math.max(SLOT_HEIGHT, ((endMinutes - startMinutes) / SLOT_MINUTES) * SLOT_HEIGHT - 2);
                const width = `${100 / laneCount}%`;
                const left = `${(lane * 100) / laneCount}%`;
                return eventChip(event, {
                  position: 'absolute',
                  top: `${top}px`,
                  left,
                  width: `calc(${width} - 4px)`,
                  height: `${height}px`,
                  marginBottom: 0,
                  whiteSpace: 'normal',
                  zIndex: 2,
                });
              })}
            </div>
          );
        })}
      </div>
    </div>
  );

  const renderWeek = () => {
    const s = startOfWeek(anchorDate);
    return renderTimeGrid(Array.from({ length: 7 }, (_, i) => {
      const d = new Date(s);
      d.setDate(s.getDate() + i);
      return d;
    }));
  };

  const renderDay = () => renderTimeGrid([anchorDate]);

  const canEditSelected = selectedEvent && (selectedEvent.canEdit || isOwnerOrAdmin);
  const detailsVisible = selectedEvent && (selectedEvent.isOwn !== false || selectedEvent.visibility === 'public' || isOwnerOrAdmin);

  return (
    <div className="webmail-layout">
      <div className="wm-ribbon">
        <div className="wm-ribbon-group">
          <button
            type="button"
            className="btn btn-primary"
            style={{ padding: '6px 14px', fontSize: '14px' }}
            onClick={() => {
              const start = new Date();
              start.setMinutes(0, 0, 0);
              const end = new Date(start);
              end.setHours(start.getHours() + 1);
              openDialog(start, end);
            }}
          >
            <img src="/images/icons/webmail/calendar.png" alt="" style={{ width: '14px', filter: 'brightness(0) invert(1)' }} />
            Schedule Activity
          </button>
        </div>
        <div className="wm-ribbon-group">
          <button type="button" className={`wm-tool-btn${viewMode === 'month' ? ' active' : ''}`} title="Month View" onClick={() => setViewMode('month')}>
            <img src="/images/icons/webmail/grid-view.png" alt="Month" />
          </button>
          <button type="button" className={`wm-tool-btn${viewMode === 'week' ? ' active' : ''}`} title="Week View" onClick={() => setViewMode('week')}>
            <img src="/images/icons/webmail/list-view.png" alt="Week" />
          </button>
          <button type="button" className={`wm-tool-btn${viewMode === 'day' ? ' active' : ''}`} title="Day View" onClick={() => setViewMode('day')}>
            <img src="/images/icons/webmail/list-view-bullets.png" alt="Day" />
          </button>
        </div>
      </div>

      <div className="wm-body">
        <aside className="wm-sidebar">
          <div className="wm-nav-section" style={{ padding: '0 16px' }}>
            <div className="wm-nav-head" style={{ padding: '8px 0' }}>My Calendars</div>
            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', color: 'var(--deep-navy)', cursor: 'default' }}>
              <input type="checkbox" defaultChecked disabled style={{ accentColor: 'var(--iris-violet)' }} />
              {currentUserEmail || 'My calendar'}
            </label>

            {isOwnerOrAdmin && (
              <div style={{ fontSize: '13px', marginTop: '8px' }}>
                <div className="wm-nav-head" style={{ padding: '0 0 8px' }}>Tenant Calendars</div>
                {directory.filter((d) => d.kind === 'user' && d.email !== currentUserEmail && !d.email.toLowerCase().includes('service') && !d.displayName.toLowerCase().includes('service')).map((d) => (
                  <label key={d.email} style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', cursor: 'pointer', marginBottom: '4px' }}>
                    <input
                      type="checkbox"
                      checked={!hiddenCalendars.has(d.email)}
                      onChange={() => setHiddenCalendars(prev => {
                        const next = new Set(prev);
                        if (next.has(d.email)) next.delete(d.email);
                        else next.add(d.email);
                        return next;
                      })}
                      style={{ accentColor: 'var(--iris-violet)' }}
                    />
                    {d.displayName}
                  </label>
                ))}
              </div>
            )}

            {!isOwnerOrAdmin && (
              <>
                <div className="wm-nav-head" style={{ padding: '12px 0 8px' }}>Colleague Calendars</div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                  {subscriptions.length === 0 && (
                    <span style={{ fontSize: '12px', color: 'var(--neutral-label)' }}>None added.</span>
                  )}
                  {subscriptions.map((s) => (
                    <div key={s.userId} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '6px', fontSize: '13px' }}>
                      <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <input
                          type="checkbox"
                          checked={!hiddenCalendars.has(s.email)}
                          onChange={() => setHiddenCalendars(prev => {
                            const next = new Set(prev);
                            if (next.has(s.email)) next.delete(s.email);
                            else next.add(s.email);
                            return next;
                          })}
                          style={{ accentColor: 'var(--accent-ruby)' }}
                        />
                        {s.displayName}
                      </span>
                      <button
                        type="button"
                        onClick={() => removeColleague(s.userId)}
                        style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--neutral-label)', fontWeight: 'bold' }}
                        title="Remove calendar"
                      >
                        ×
                      </button>
                    </div>
                  ))}
                </div>

                {availableColleagues.length > 0 && (
                  <select
                    className="input-base"
                    style={{ marginTop: '10px', fontSize: '13px' }}
                    value=""
                    onChange={(e) => {
                      if (e.target.value) void addColleague(e.target.value);
                    }}
                  >
                    <option value="">+ Add a colleague calendar…</option>
                    {availableColleagues.map((c) => (
                      <option key={c.userId} value={c.userId}>
                        {c.displayName}
                      </option>
                    ))}
                  </select>
                )}
              </>
            )}

            {mailboxAccounts.filter((a) => a.kind === 'shared').length > 0 && (
              <div style={{ fontSize: '11px', color: 'var(--neutral-label)', marginTop: '12px' }}>
                Shared mailboxes cannot be invited to activities.
              </div>
            )}
          </div>
        </aside>

        <main className="wm-content">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
            <h1 style={{ fontSize: '1.8rem' }}>{headerLabel}</h1>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button className="btn btn-secondary" style={{ padding: '6px 12px', fontSize: '14px' }} onClick={() => setAnchorDate(new Date())}>Today</button>
              <button className="btn btn-secondary" style={{ padding: '6px 12px', fontSize: '14px' }} onClick={() => shift(-1)}>&lt;</button>
              <button className="btn btn-secondary" style={{ padding: '6px 12px', fontSize: '14px' }} onClick={() => shift(1)}>&gt;</button>
            </div>
          </div>

          {error && <div style={{ color: 'var(--accent-ruby)', fontSize: '13px', marginBottom: '12px' }}>{error}</div>}

          {viewMode === 'month' && renderMonth()}
          {viewMode === 'week' && renderWeek()}
          {viewMode === 'day' && renderDay()}
        </main>
      </div>

      {renderTooltip()}

      {selectedEvent && (
        <CalendarEventDialog
          event={selectedEvent}
          canEdit={!!canEditSelected}
          detailsVisible={!!detailsVisible}
          currentUserEmail={currentUserEmail}
          onClose={() => setSelectedEvent(null)}
          onEdit={openEditDialog}
          onDelete={(evt) => {
            setSelectedEvent(null);
            void handleDelete(evt);
          }}
          onResend={(evt) => void handleResendInvitations(evt)}
          onAcceptProposal={(evt, attendeeId) => void handleAcceptProposal(evt, attendeeId)}
          onDeclineProposal={(evt, attendeeId) => void handleDeclineProposal(evt, attendeeId)}
          onOpenRsvp={(evt) => {
            setSelectedEvent(null);
            setRsvpEvent(evt);
          }}
        />
      )}

      {rsvpEvent && (
        <CalendarRsvpDialog
          event={rsvpEvent}
          onRsvp={handleRsvp}
          onClose={() => setRsvpEvent(null)}
        />
      )}

      {contextMenu && (
        <CalendarEventContextMenu
          x={contextMenu.x}
          y={contextMenu.y}
          event={contextMenu.event}
          canEdit={!!(contextMenu.event.canEdit || isOwnerOrAdmin)}
          isOrganizer={contextMenu.event.organizer === currentUserEmail}
          onOpen={(evt) => {
            setContextMenu(null);
            setSelectedEvent(evt);
          }}
          onEdit={openEditDialog}
          onResend={(evt) => void handleResendInvitations(evt)}
          onDelete={(evt) => void handleDelete(evt)}
          onClose={() => setContextMenu(null)}
        />
      )}

      {dialogOpen && (
        <SchedulingActivitiesDialog
          currentUserEmail={currentUserEmail}
          currentUserId={currentUserId}
          event={editingEvent ?? undefined}
          initialStartTime={dialogStart}
          initialEndTime={dialogEnd}
          contacts={contacts}
          onClose={() => {
            setDialogOpen(false);
            setEditingEvent(null);
          }}
          onSaved={reloadEvents}
        />
      )}
    </div>
  );
};
