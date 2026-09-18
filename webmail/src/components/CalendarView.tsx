import React from 'react';
import type { CalendarEvent } from '../types';

interface CalendarViewProps {
  events: CalendarEvent[];
}

export const CalendarView: React.FC<CalendarViewProps> = ({ events }) => {
  const daysInMonth = Array.from({ length: 30 }, (_, i) => i + 1);

  return (
    <div className="webmail-layout">
      {/* Calendar Ribbon */}
      <div className="wm-ribbon">
        <div className="wm-ribbon-group">
          <button type="button" className="btn btn-primary" style={{ padding: '6px 14px', fontSize: '14px' }}>
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
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '14px', color: 'var(--deep-navy)', cursor: 'pointer' }}>
                <input type="checkbox" defaultChecked style={{ accentColor: 'var(--accent-ruby)' }} />
                Security Ops Schedule
              </label>
            </div>
          </div>
        </aside>

        <main className="wm-content">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
            <h1 style={{ fontSize: '2rem' }}>
              September 2026
            </h1>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button className="btn btn-secondary" style={{ padding: '6px 12px', fontSize: '14px' }}>Today</button>
              <button className="btn btn-secondary" style={{ padding: '6px 12px', fontSize: '14px' }}>&lt;</button>
              <button className="btn btn-secondary" style={{ padding: '6px 12px', fontSize: '14px' }}>&gt;</button>
            </div>
          </div>

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
                const isToday = day === 18;
                const dayEvents = events.filter((e) => new Date(e.startTime).getDate() === day);

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
                        }}
                        title={`${evt.title} (${evt.organizer})`}
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
