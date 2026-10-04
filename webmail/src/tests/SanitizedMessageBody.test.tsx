import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { SanitizedMessageBody, sanitizeEmailHtml } from '../components/SanitizedMessageBody';

describe('SanitizedMessageBody (Security & Acceptance Criteria)', () => {
  it('WHEN a message body containing a script tag is rendered THE SYSTEM SHALL sanitise it with DOMPurify and SHALL NOT execute the script', () => {
    const maliciousHtml = '<p>Normal text</p><script>window.pwned = true; alert("xss");</script><b>Safe Bold</b>';

    render(<SanitizedMessageBody htmlContent={maliciousHtml} />);

    const container = screen.getByTestId('sanitized-email-body');

    // Verify script tag is completely removed from DOM
    expect(container.querySelector('script')).toBeNull();
    expect(container.innerHTML).not.toContain('<script>');
    expect(container.innerHTML).not.toContain('alert("xss")');
    expect(container.textContent).toContain('Normal text');
    expect(container.textContent).toContain('Safe Bold');
  });

  it('strips inline event handlers (onerror, onload, onclick, onmouseover)', () => {
    const maliciousPayload = `
      <img src="invalid.jpg" onerror="alert(1)" />
      <a href="#" onclick="fetch('/api/token')">Click me</a>
      <div onmouseover="stealData()">Hover</div>
      <body onload="exploit()">Hello</body>
    `;

    const clean = sanitizeEmailHtml(maliciousPayload, true);

    expect(clean).not.toContain('onerror');
    expect(clean).not.toContain('onclick');
    expect(clean).not.toContain('onmouseover');
    expect(clean).not.toContain('onload');
  });

  it('strips dangerous protocols such as javascript: and data: URIs in anchors', () => {
    const dangerousHref = '<a href="javascript:alert(document.cookie)">Malicious Link</a>';
    const clean = sanitizeEmailHtml(dangerousHref, true);

    expect(clean).not.toContain('javascript:');
  });

  it('strips iframe, object, and embed tags', () => {
    const embeddedPayload = `
      <iframe src="http://attacker.com/cookie-stealer"></iframe>
      <object data="malware.swf"></object>
      <embed src="exploit.pdf"></embed>
    `;

    const clean = sanitizeEmailHtml(embeddedPayload, true);

    expect(clean).not.toContain('<iframe');
    expect(clean).not.toContain('<object');
    expect(clean).not.toContain('<embed');
  });

  it('blocks remote images by default to prevent tracking pixels until user clicks load', () => {
    const emailWithTrackingPixel = `
      <p>Invoice details</p>
      <img src="https://tracking.analytics.internal/pixel.gif?uid=123" alt="tracker" />
    `;

    render(<SanitizedMessageBody htmlContent={emailWithTrackingPixel} />);

    const container = screen.getByTestId('sanitized-email-body');
    const img = container.querySelector('img');

    // Initially blocked
    expect(img?.getAttribute('src')).toContain('data:image/svg+xml');
    expect(img?.getAttribute('data-blocked-src')).toBe('https://tracking.analytics.internal/pixel.gif?uid=123');

    // Click button to allow remote images
    const loadBtn = screen.getByRole('button', { name: /load remote images/i });
    fireEvent.click(loadBtn);

    const updatedImg = screen.getByTestId('sanitized-email-body').querySelector('img');
    expect(updatedImg?.getAttribute('src')).toBe('https://tracking.analytics.internal/pixel.gif?uid=123');
  });
});
