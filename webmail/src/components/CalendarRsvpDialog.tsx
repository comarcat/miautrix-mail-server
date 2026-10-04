import React, { useState } from 'react';
import type { CalendarEvent } from '../types';

interface CalendarRsvpDialogProps {
  event: CalendarEvent;
  onRsvp: (event: CalendarEvent, response: string, note?: string, proposedStartTime?: string, proposedEndTime?: string) => void;
  onClose: () => void;
}

export const CalendarRsvpDialog: React.FC<CalendarRsvpDialogProps> = ({ event, onRsvp, onClose }) => {
  const [note, setNote] = useState('');
  const [proposedStartTime, setProposedStartTime] = useState(event.startTime.slice(0, 16));
  const [proposedEndTime, setProposedEndTime] = useState(event.endTime.slice(0, 16));

  return (
    <div className="wm-modal-backdrop" onClick={onClose}>
      <div className="wm-modal-panel" onClick={(e) => e.stopPropagation()}>
        <div className="wm-modal-header">
          <h2>Respond to Invitation</h2>
          <button type="button" className="btn btn-ghost" onClick={onClose}>Close</button>
        </div>
        <div style={{ padding: '16px' }}>
          <p>Respond to invitation for <strong>{event.title}</strong></p>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: '12px', marginBottom: '12px' }}>
            <label style={{ fontSize: '13px' }}>Proposed start
              <input className="input-base" type="datetime-local" value={proposedStartTime} onChange={(e) => setProposedStartTime(e.target.value)} />
            </label>
            <label style={{ fontSize: '13px' }}>Proposed end
              <input className="input-base" type="datetime-local" value={proposedEndTime} onChange={(e) => setProposedEndTime(e.target.value)} />
            </label>
          </div>
          <textarea
            className="input-base"
            placeholder="Optional note..."
            value={note}
            onChange={(e) => setNote(e.target.value)}
            style={{ width: '100%', minHeight: '80px', marginBottom: '16px' }}
          />
          <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
            <button type="button" className="btn btn-primary" onClick={() => { onRsvp(event, 'accepted', note); onClose(); }}>Accept</button>
            <button type="button" className="btn btn-secondary" onClick={() => { onRsvp(event, 'tentative', note); onClose(); }}>Tentative</button>
            <button type="button" className="btn btn-secondary" onClick={() => { onRsvp(event, 'declined', note); onClose(); }}>Decline</button>
            <button type="button" className="btn btn-secondary" onClick={() => { onRsvp(event, 'reschedule', note, new Date(proposedStartTime).toISOString(), new Date(proposedEndTime).toISOString()); onClose(); }}>Request Reschedule</button>
          </div>
        </div>
      </div>
    </div>
  );
};
