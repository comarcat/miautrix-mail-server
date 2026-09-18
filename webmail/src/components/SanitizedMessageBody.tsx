import React, { useMemo, useState } from 'react';
import DOMPurify from 'dompurify';

interface SanitizedMessageBodyProps {
  htmlContent: string;
  allowRemoteImagesDefault?: boolean;
}

export const sanitizeEmailHtml = (dirtyHtml: string, allowRemoteImages: boolean = false): string => {
  if (!dirtyHtml) return '';

  const clean = DOMPurify.sanitize(dirtyHtml, {
    FORBID_TAGS: ['script', 'iframe', 'object', 'embed', 'form', 'link', 'base'],
    FORBID_ATTR: ['onerror', 'onload', 'onclick', 'onmouseover', 'onfocus', 'onblur', 'formaction'],
    ALLOW_DATA_ATTR: false,
    ADD_ATTR: ['target'],
  });

  if (!allowRemoteImages) {
    // Replace http/https image src with placeholder to block tracking pixels
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = clean;
    const images = tempDiv.querySelectorAll('img');
    images.forEach((img) => {
      const src = img.getAttribute('src');
      if (src && (src.startsWith('http://') || src.startsWith('https://') || src.startsWith('//'))) {
        img.setAttribute('data-blocked-src', src);
        img.setAttribute('src', 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="100" height="20"><text y="15" fill="%23999" font-size="12">Image Blocked</text></svg>');
        img.setAttribute('alt', '[Remote tracking image blocked]');
        img.style.border = '1px dashed #cbd5e1';
        img.style.padding = '4px';
      }
    });
    return tempDiv.innerHTML;
  }

  return clean;
};

export const SanitizedMessageBody: React.FC<SanitizedMessageBodyProps> = ({
  htmlContent,
  allowRemoteImagesDefault = false,
}) => {
  const [allowRemoteImages, setAllowRemoteImages] = useState(allowRemoteImagesDefault);

  const cleanHtml = useMemo(() => {
    return sanitizeEmailHtml(htmlContent, allowRemoteImages);
  }, [htmlContent, allowRemoteImages]);

  return (
    <div className="sanitized-message-container">
      <div className="sanitization-banner">
        <span>🛡️ Protected by DOMPurify: Untrusted active scripts and remote tracking pixels have been sanitized.</span>
        {!allowRemoteImages ? (
          <button
            type="button"
            className="btn btn-secondary"
            style={{ padding: '4px 10px', fontSize: '12px' }}
            onClick={() => setAllowRemoteImages(true)}
          >
            Load Remote Images
          </button>
        ) : (
          <span style={{ fontSize: '12px', color: 'var(--neutral-success-txt)' }}>Remote images enabled</span>
        )}
      </div>

      <div
        className="reading-body"
        data-testid="sanitized-email-body"
        dangerouslySetInnerHTML={{ __html: cleanHtml }}
      />
    </div>
  );
};
