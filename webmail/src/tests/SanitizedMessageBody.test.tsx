import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { SanitizedMessageBody, sanitizeEmailHtml } from '../components/SanitizedMessageBody';

describe('SanitizedMessageBody isolation', () => {
  it('renders sanitized content in a sandboxed iframe without scripts', () => {
    render(<SanitizedMessageBody htmlContent={'<p>Normal text</p><script>alert("xss")</script><b>Safe</b>'} />);
    const frame = screen.getByTestId('sanitized-email-body');
    expect(frame.tagName).toBe('IFRAME');
    expect(frame.getAttribute('sandbox')).toBe('allow-same-origin allow-popups allow-popups-to-escape-sandbox');
    expect(frame.getAttribute('sandbox')).not.toContain('allow-scripts');
    expect(frame.getAttribute('srcdoc')).not.toContain('<script>');
    expect(frame.getAttribute('srcdoc')).toContain('Normal text');
    expect(frame.getAttribute('srcdoc')).not.toContain('alert("xss")');
  });

  it('measures the srcdoc document after its load event', () => {
    const { getByTestId } = render(<SanitizedMessageBody htmlContent="<p>Body</p>" />);
    const frame = getByTestId('sanitized-email-body') as HTMLIFrameElement;
    const doc = frame.contentDocument!;
    Object.defineProperty(doc.documentElement, 'scrollHeight', { configurable: true, value: 184 });
    Object.defineProperty(doc.body, 'scrollHeight', { configurable: true, value: 120 });
    fireEvent.load(frame);
    expect(frame.style.height).toBe('184px');
  });

  it('blocks remote images by default and allows them only after the user action', () => {
    render(<SanitizedMessageBody htmlContent={'<img src="https://tracking.example/pixel.gif">'} />);
    const frame = screen.getByTestId('sanitized-email-body');
    expect(frame.getAttribute('srcdoc')).toContain('img-src data:;');
    expect(frame.getAttribute('srcdoc')).toContain('data-blocked-src="https://tracking.example/pixel.gif"');
    fireEvent.click(screen.getByRole('button', { name: /load remote images/i }));
    expect(frame.getAttribute('srcdoc')).toContain('img-src data: cid: https: http:;');
    expect(frame.getAttribute('srcdoc')).toContain('src="https://tracking.example/pixel.gif"');
  });

  it('removes remote CSS fetches while remote images are blocked', () => {
    const clean = sanitizeEmailHtml('<style>@import url(https://evil.test/x.css); p{background:url(https://evil.test/a)}</style><p>x</p>');
    expect(clean).not.toContain('@import');
    expect(clean).not.toContain('https://evil.test');
  });

  it('keeps remote loading disabled when the embedding shell disables it', () => {
    render(<SanitizedMessageBody htmlContent="<p>Body</p>" allowRemoteImagesDefault remoteImagesDisabled />);
    expect(screen.queryByRole('button', { name: /load remote images/i })).toBeNull();
    expect(screen.getByTestId('sanitized-email-body').getAttribute('srcdoc')).toContain('img-src data:;');
  });

  it('decoded MIME content is rendered without quoted-printable artifacts', () => {
    render(<SanitizedMessageBody htmlContent="<p>Hello, world = done</p>" />);
    const srcdoc = screen.getByTestId('sanitized-email-body').getAttribute('srcdoc') ?? '';
    expect(srcdoc).toContain('Hello, world = done');
    expect(srcdoc).not.toContain('=20');
    expect(srcdoc).not.toContain('=3D');
  });
});
