/** Triggers a browser download for an already fetched blob. */
export const downloadBlob = (blob: Blob, fileName: string): void => {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
};

export const archiveFileName = (email: string): string =>
  `mailbox-export-${email.replace(/[@.]/g, '-')}.zip`;
