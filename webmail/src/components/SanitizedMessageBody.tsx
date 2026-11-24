import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import DOMPurify from 'dompurify';

interface SanitizedMessageBodyProps {
  htmlContent: string;
  allowRemoteImagesDefault?: boolean;
  remoteImagesDisabled?: boolean;
}

export const sanitizeEmailHtml = (dirtyHtml: string, allowRemoteImages: boolean = false): string => {
  if (!dirtyHtml) return '';

  const clean = DOMPurify.sanitize(dirtyHtml, {
    FORBID_TAGS: ['script', 'iframe', 'object', 'embed', 'form', 'link', 'base'],
    FORBID_ATTR: ['onerror', 'onload', 'onclick', 'onmouseover', 'onfocus', 'onblur', 'formaction'],
    ALLOW_DATA_ATTR: false,
    ADD_ATTR: ['target', 'width'],
  });

  const tempDiv = document.createElement('div');
  tempDiv.innerHTML = clean;
  if (!allowRemoteImages) {
    // Remove CSS fetch mechanisms as well as image src fetches while remote content is blocked.
    tempDiv.querySelectorAll('style').forEach((style) => {
      style.textContent = (style.textContent ?? '')
        .replace(/@import\s+(?:url\([^)]*\)|[^;]+);?/gi, '')
        .replace(/url\(\s*(['"]?)(?:https?:)?\/\/[^)]*\)/gi, 'none');
    });
    tempDiv.querySelectorAll<HTMLElement>('[style]').forEach((element) => {
      const style = element.getAttribute('style') ?? '';
      element.setAttribute('style', style
        .replace(/@import\s+(?:url\([^)]*\)|[^;]+);?/gi, '')
        .replace(/url\(\s*(['"]?)(?:https?:)?\/\/[^)]*\)/gi, 'none'));
    });
    const images = tempDiv.querySelectorAll('img');
    images.forEach((img) => {
      const src = img.getAttribute('src');
      if (src && (/^(?:https?:)?\/\//i.test(src))) {
        img.setAttribute('data-blocked-src', src);
        img.setAttribute('src', 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="100" height="20"><text y="15" fill="%23999" font-size="12">Image Blocked</text></svg>');
        img.setAttribute('alt', '[Remote tracking image blocked]');
        img.style.border = '1px dashed #cbd5e1';
        img.style.padding = '4px';
      }
    });
  }
  return tempDiv.innerHTML;
};

export const SanitizedMessageBody: React.FC<SanitizedMessageBodyProps> = ({
  htmlContent,
  allowRemoteImagesDefault = false,
  remoteImagesDisabled = false,
}) => {
  const [allowRemoteImages, setAllowRemoteImages] = useState(allowRemoteImagesDefault && !remoteImagesDisabled);
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const resizeObserverRef = useRef<ResizeObserver | null>(null);
  const cleanHtml = useMemo(() => sanitizeEmailHtml(htmlContent, allowRemoteImages), [htmlContent, allowRemoteImages]);
  const csp = allowRemoteImages
    ? "default-src 'none'; img-src data: cid: https: http:; style-src 'unsafe-inline'"
    : "default-src 'none'; img-src data:; style-src 'unsafe-inline'";
  const srcDoc = `<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="${csp}"><base target="_blank"><style>html,body{margin:0;padding:0;overflow-wrap:anywhere}img{max-width:100%;height:auto}</style></head><body>${cleanHtml}</body></html>`;

  const handleFrameLoad = useCallback(() => {
    resizeObserverRef.current?.disconnect();
    resizeObserverRef.current = null;
    const frame = iframeRef.current;
    const doc = frame?.contentDocument;
    if (!frame || !doc) return;
    const resize = () => { frame.style.height = `${Math.max(doc.documentElement.scrollHeight, doc.body?.scrollHeight ?? 0)}px`; };
    resize();
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(resize);
    resizeObserverRef.current = observer;
    observer.observe(doc.documentElement);
    if (doc.body) observer.observe(doc.body);
  }, []);

  useEffect(() => {
    return () => {
      resizeObserverRef.current?.disconnect();
      resizeObserverRef.current = null;
    };
  }, [srcDoc]);

  return (
    <div className="sanitized-message-container">
      <div className="sanitization-banner">
        <span>🛡️ Protected by DOMPurify: Untrusted active scripts and remote tracking pixels have been sanitized.</span>
        {!allowRemoteImages && !remoteImagesDisabled ? (
          <button type="button" className="btn btn-secondary" style={{ padding: '4px 10px', fontSize: '12px' }} onClick={() => setAllowRemoteImages(true)}>
            Load Remote Images
          </button>
        ) : allowRemoteImages ? (
          <span style={{ fontSize: '12px', color: 'var(--neutral-success-txt)' }}>Remote images enabled</span>
        ) : null}
      </div>
      <iframe
        ref={iframeRef}
        title="Email message body"
        sandbox="allow-same-origin allow-popups allow-popups-to-escape-sandbox"
        srcDoc={srcDoc}
        onLoad={handleFrameLoad}
        data-testid="sanitized-email-body"
        style={{ display: 'block', width: '100%', border: 0, minHeight: '1px' }}
      />
    </div>
  );
};
