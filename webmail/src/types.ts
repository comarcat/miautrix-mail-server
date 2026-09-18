export interface Mailbox {
  id: string;
  name: string;
  role: 'inbox' | 'drafts' | 'sent' | 'junk' | 'archive' | 'trash' | 'custom';
  unreadEmails: number;
  totalEmails: number;
  icon: string;
}

export interface EmailAttachment {
  id: string;
  name: string;
  size: number;
  contentType: string;
  blobId?: string;
}

export interface EmailMessage {
  id: string;
  mailboxId: string;
  from: {
    name: string;
    email: string;
  };
  to: Array<{
    name: string;
    email: string;
  }>;
  cc?: Array<{
    name: string;
    email: string;
  }>;
  subject: string;
  snippet: string;
  bodyHtml: string;
  bodyText?: string;
  receivedAt: string;
  isUnread: boolean;
  isFlagged?: boolean;
  securityChecks: {
    spfPass: boolean;
    dkimPass: boolean;
    dmarcPass: boolean;
    tlsVersion?: string;
    spamScore?: number;
  };
  attachments: EmailAttachment[];
}

export interface Contact {
  id: string;
  name: string;
  email: string;
  organization: string;
  department?: string;
  phone?: string;
  book: 'personal' | 'directory';
}

export interface CalendarEvent {
  id: string;
  title: string;
  startTime: string;
  endTime: string;
  location?: string;
  organizer: string;
  status: 'confirmed' | 'tentative' | 'cancelled';
}

export interface SieveFilterRule {
  id: string;
  name: string;
  field: 'from' | 'subject' | 'to' | 'header';
  comparator: 'contains' | 'is' | 'matches' | 'exists';
  value: string;
  action: 'fileinto' | 'redirect' | 'reject' | 'addflag' | 'discard';
  targetFolder?: string;
  active: boolean;
}
