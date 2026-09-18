import React, { useState } from 'react';

interface ComposerViewProps {
  onDiscardClick: () => void;
  onSendClick: (message: { to: string; subject: string; body: string }) => void;
}

export const ComposerView: React.FC<ComposerViewProps> = ({ onDiscardClick, onSendClick }) => {
  const [to, setTo] = useState('');
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');

  const handleSend = () => {
    onSendClick({ to, subject, body });
  };

  return (
    <div className="webmail-layout" style={{ background: '#f8fafc' }}>
      {/* Compose Actions Strip */}
      <div className="wm-header" style={{ padding: '16px 24px', borderBottom: '1px solid var(--neutral-border)' }}>
        <div className="wm-compose-actions" style={{ marginBottom: 0 }}>
          <div className="btn-send-split">
            <button type="button" className="btn-send-main" onClick={handleSend}>
              <img src="/images/icons/webmail/compose-envelope.png" alt="" /> Send
            </button>
            <button type="button" className="btn-send-drop" aria-label="Send Options">
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                <polyline points="6 9 12 15 18 9"></polyline>
              </svg>
            </button>
          </div>

          <button type="button" className="btn btn-secondary" style={{ padding: '10px 16px' }} onClick={onDiscardClick}>
            Discard
          </button>
          <button type="button" className="btn btn-secondary" style={{ padding: '10px 16px' }}>
            Save Draft
          </button>
        </div>
      </div>

      {/* Main Composer Area */}
      <div style={{ flex: 1, padding: '24px', display: 'flex', justifyContent: 'center', overflowY: 'auto' }}>
        <div className="card" style={{ width: '100%', maxWidth: '900px', display: 'flex', flexDirection: 'column', height: '100%', padding: 0, overflow: 'hidden' }}>

          <div style={{ padding: '24px 24px 0 24px' }}>
            <div className="compose-header">
              <div className="from-selector">
                <span style={{ color: 'var(--neutral-body)' }}>From:</span>
                <div style={{ padding: '4px 8px', borderRadius: '4px', background: 'var(--surface-canvas)', display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                  Alex Vance &lt;alex.vance@miautrix.org&gt;
                  <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                    <polyline points="6 9 12 15 18 9"></polyline>
                  </svg>
                </div>
              </div>

              <div className="compose-row">
                <div className="pill-label">To</div>
                <input
                  type="text"
                  className="compose-input"
                  placeholder="Enter recipients..."
                  value={to}
                  onChange={(e) => setTo(e.target.value)}
                />
              </div>

              <div className="compose-row" style={{ paddingBottom: '16px' }}>
                <input
                  type="text"
                  className="subject-input"
                  placeholder="Add a subject"
                  value={subject}
                  onChange={(e) => setSubject(e.target.value)}
                  style={{ fontSize: '1.25rem', padding: '8px 0' }}
                />
              </div>
            </div>
          </div>

          {/* Formatting Ribbon inside the composer card */}
          <div style={{ background: '#f8fafc', borderTop: '1px solid var(--neutral-border)', borderBottom: '1px solid var(--neutral-border)', padding: '8px 16px', display: 'flex', gap: '12px', alignItems: 'center', overflowX: 'auto' }}>
            <button className="wm-tool-btn active"><img src="/images/icons/webmail/bold.png" alt="B" /></button>
            <button className="wm-tool-btn"><img src="/images/icons/webmail/italic.png" alt="I" /></button>
            <button className="wm-tool-btn"><img src="/images/icons/webmail/underline.png" alt="U" /></button>
            <div style={{ width: '1px', height: '20px', background: 'var(--neutral-border)' }}></div>
            <button className="wm-tool-btn"><img src="/images/icons/webmail/text-color.png" alt="Color" /></button>
            <button className="wm-tool-btn"><img src="/images/icons/webmail/align-left.png" alt="A-L" /></button>
            <button className="wm-tool-btn"><img src="/images/icons/webmail/unordered-list.png" alt="UL" /></button>
            <div style={{ width: '1px', height: '20px', background: 'var(--neutral-border)' }}></div>
            <button className="wm-tool-btn"><img src="/images/icons/webmail/link.png" alt="Link" /></button>
            <button className="wm-tool-btn"><img src="/images/icons/webmail/attach-file.png" alt="Attach" /></button>
            <button className="wm-tool-btn"><img src="/images/icons/webmail/insert-image.png" alt="Image" /></button>
            <div style={{ flex: 1 }}></div>
            <button className="wm-tool-btn"><img src="/images/icons/webmail/more-options.png" alt="More" /></button>
          </div>

          {/* Editable Body */}
          <textarea
            className="compose-body"
            style={{ padding: '24px', border: 'none', resize: 'none', background: 'transparent' }}
            placeholder="Type your message here..."
            value={body}
            onChange={(e) => setBody(e.target.value)}
          />

          <div style={{ padding: '12px 24px', background: 'var(--surface-canvas)', borderTop: '1px solid var(--neutral-border)', fontSize: '13px', color: 'var(--neutral-body)' }}>
            Status: Saved to Drafts
          </div>
        </div>
      </div>
    </div>
  );
};
