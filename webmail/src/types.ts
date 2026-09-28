export interface Mailbox {
  id: string;
  name: string;
  role: 'inbox' | 'drafts' | 'sent' | 'junk' | 'archive' | 'trash' | 'custom';
  unreadEmails: number;
  totalEmails: number;
  icon: string;
  parentId?: string | null;
  mailboxId?: string;
  quotaBytes?: number;
  usedBytes?: number;
}

export interface MailboxAccount {
  id: string;
  address: string;
  name?: string;
  kind?: 'user' | 'shared';
  accessLevel?: 'read' | 'write';
  quotaBytes?: number;
  usedBytes?: number;
}

export interface SharedMailboxGroup {
  account: MailboxAccount;
  folders: Mailbox[];
}

// Folder/group DTOs use the same shape as Mailbox in the current webmail UI.
// (Mailbox is misnamed historically in this repo.)

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
  folderId: string;
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
  bcc?: Array<{
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
  flagColor?: 'red' | 'blue' | 'green' | 'orange' | 'purple';
  securityChecks: {
    spfPass: boolean;
    dkimPass: boolean;
    dmarcPass: boolean;
    tlsVersion?: string;
    spamScore?: number;
  };
  attachments: EmailAttachment[];
}

export interface RawEmailMessage {
  id: string;
  mailbox_id: string;
  folder_id: string;
  sender: string;
  recipient: string;
  subject: string;
  preview: string;
  date: string;
  is_read: boolean;
  size_bytes: number;
  flag_color?: 'red' | 'blue' | 'green' | 'orange' | 'purple' | null;
  flagColor?: 'red' | 'blue' | 'green' | 'orange' | 'purple' | null;
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

export interface MailSignature {
  id: string;
  name: string;
  contentText: string;
  contentHtml?: string | null;
  isDefault: boolean;
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
