import React from 'react';
import type { CalendarEvent } from '../types';

interface CalendarEventDialogProps {
  event: CalendarEvent;
  canEdit: boolean;
  detailsVisible: boolean;
  currentUserEmail: string;
  onClose: () => void;
  onEdit: (event: CalendarEvent) => void;
  onDelete: (event: CalendarEvent) => void;
  onResend: (event: CalendarEvent) => void;
  onAcceptProposal: (event: CalendarEvent, attendeeId: string) => void;
  onDeclineProposal: (event: CalendarEvent, attendeeId: string) => void;
  onOpenRsvp: (event: CalendarEvent) => void;
}

// Centered read-only detail dialog for a selected event. The body is the same content
// that used to render inline above the grid; moving it here stops the month/week/day
// view from being pushed down when an event is selected.
export const CalendarEventDialog: React.FC<CalendarEventDialogProps> = ({
  event,
  canEdit,
  detailsVisible,
  currentUserEmail,
  onClose,
  onEdit,
  onDelete,
  onResend,
  onAcceptProposal,
  onDeclineProposal,
  onOpenRsvp,
}) => (
  <div className="wm-modal-backdrop" onClick={onClose}>
    <div className="wm-modal-panel" onClick={(e) => e.stopPropagation()}>
      <div className="wm-modal-header">
        <div>
          <h2>{detailsVisible ? event.title : 'Busy'}</h2>
          <p>
            {new Date(event.startTime).toLocaleString()} – {new Date(event.endTime).toLocaleString()}
          </p>
        </div>
        <button type="button" className="btn btn-secondary" style={{ padding: '4px 10px' }} onClick={onClose}>
          Close
        </button>
      </div>

      <div style={{ overflowY: 'auto', padding: '16px 18px', display: 'grid', gap: '8px' }}>
        {detailsVisible ? (
          <>
            <p style={{ margin: 0, fontSize: '13px', color: 'var(--neutral-body)' }}>
              <strong>Organizer:</strong> {event.organizer || '—'}
            </p>
            {event.location && (
              <p style={{ margin: 0, fontSize: '13px', color: 'var(--neutral-body)' }}>
                <strong>Location:</strong> {event.location}
              </p>
            )}
            {event.description && (
              <p style={{ margin: 0, fontSize: '13px', color: 'var(--neutral-body)' }}>
                <strong>Agenda:</strong> {event.description}
              </p>
            )}
          </>
        ) : (
          <p style={{ margin: 0, fontSize: '13px', color: 'var(--neutral-label)', fontStyle: 'italic' }}>
            This is a subscribed calendar; details are private.
          </p>
        )}

        {detailsVisible && event.attendees && (
          <div style={{ marginTop: '8px' }}>
            <h4 style={{ margin: '0 0 8px 0', fontSize: '14px' }}>Attendees & RSVPs</h4>
            <div style={{ display: 'grid', gap: '6px' }}>
              {event.attendees.map((att) => (
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
                        {att.proposalNote && ` – "${att.proposalNote}"`}
                      </div>
                    )}
                  </div>
                  {canEdit && att.responseStatus === 'reschedule_proposed' && (
                    <div style={{ display: 'flex', gap: '6px' }}>
                      <button
                        type="button"
                        className="btn btn-primary"
                        style={{ padding: '2px 8px', fontSize: '12px' }}
                        onClick={() => onAcceptProposal(event, att.id)}
                      >
                        Accept Proposal
                      </button>
                      <button
                        type="button"
                        className="btn btn-secondary"
                        style={{ padding: '2px 8px', fontSize: '12px' }}
                        onClick={() => onDeclineProposal(event, att.id)}
                      >
                        Decline Proposal
                      </button>
                    </div>
                  )}
                </div>
              ))}
              {event.organizer !== currentUserEmail && event.attendees?.find(a => a.email.toLowerCase() === currentUserEmail.toLowerCase())?.responseStatus === 'needs_action' && (
                <div style={{ display: 'flex', gap: '6px' }}>
                  <button type="button" className="btn btn-primary" onClick={() => onOpenRsvp(event)}>Respond to invitation...</button>
                </div>
              )}
            </div>
          </div>
        )}
      </div>

      <div className="wm-modal-footer">
        <span />
        <div style={{ display: 'flex', gap: '6px' }}>
          {canEdit && (
            <>
              <button type="button" className="btn btn-secondary" style={{ padding: '4px 10px', fontSize: '12px' }} onClick={() => onResend(event)}>
                Resend invitations
              </button>
              <button type="button" className="btn btn-secondary" style={{ padding: '4px 10px', fontSize: '12px', color: 'var(--accent-ruby)' }} onClick={() => onDelete(event)}>
                Delete
              </button>
              <button type="button" className="btn btn-primary" style={{ padding: '4px 12px', fontSize: '12px' }} onClick={() => onEdit(event)}>
                Edit
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  </div>
);
