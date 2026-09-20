export interface QueueItem {
  id: string;
  message_id: string;
  sender: string;
  recipient: string;
  size_bytes: number;
  status: 'queued' | 'retrying' | 'dead_letter' | 'delivered';
  attempts: number;
  next_retry_at: string;
  created_at: string;
  error_message?: string;
}

export interface ApiMeta {
  next_cursor?: string | null;
  has_more: boolean;
  total_count?: number;
}

export interface ApiResponse<T> {
  data: T;
  meta?: ApiMeta;
}

export interface QueueQueryParams {
  cursor?: string | null;
  limit?: number;
  status?: string;
  search?: string;
}

// Domain Types
export interface DomainItem {
  id: string;
  name: string;
  is_verified: boolean;
  dkim_selector?: string;
  dkim_public_key?: string;
  spf_record?: string;
  dmarc_record?: string;
  is_primary: boolean;
  created_at: string;
}

export interface CreateDomainRequest {
  name: string;
  is_primary?: boolean;
}

export interface UpdateDomainRequest {
  name?: string;
  is_primary?: boolean;
  dkim_selector?: string;
  spf_record?: string;
  dmarc_record?: string;
}

export interface VerifyDomainResult {
  is_verified: boolean;
  message: string;
  dkim_status: string;
  spf_status: string;
  dmarc_status: string;
}

// User Types
export interface AdminUserItem {
  id: string;
  email: string;
  name: string;
  is_active: boolean;
  role: string;
  mailbox_quota_bytes: number;
  mailbox_used_bytes: number;
  created_at: string;
}

export interface CreateUserRequest {
  email: string;
  name: string;
  password?: string;
  role?: string;
  quota_bytes?: number;
}

export interface UpdateUserRequest {
  name?: string;
  is_active?: boolean;
  role?: string;
  quota_bytes?: number;
}

// Quarantine Types
export interface QuarantineItem {
  id: string;
  sender: string;
  recipient: string;
  subject?: string;
  spam_score: number;
  threshold: number;
  reasons_json: string;
  status: string;
  quarantined_at: string;
  released_at?: string;
}

// Audit Types
export interface AuditLogItem {
  id: string;
  action: string;
  actor_id?: string;
  actor_email?: string;
  target_type: string;
  target_id?: string;
  details_json?: string;
  ip_address?: string;
  created_at: string;
}

// Mail Flow Rules
export interface MailFlowRuleItem {
  id: string;
  name: string;
  priority: number;
  is_enabled: boolean;
  conditions_json: string;
  actions_json: string;
  created_at: string;
}

// System & Telemetry
export interface DashboardSummary {
  active_queued: number;
  retrying: number;
  dead_letters: number;
  delivered24h: number;
  quarantined24h: number;
  spam_blocked24h: number;
  system_health: string;
  uptime_seconds: number;
  tenant_count: number;
}

export interface SystemInfo {
  version: string;
  runtime: string;
  database_status: string;
  uptime_seconds: number;
  storage_used_bytes: number;
  storage_total_bytes: number;
  active_workers: number;
  os_version: string;
}

export interface LicensingInfo {
  edition: string;
  active_mailboxes: number;
  max_mailboxes: number;
  mfa_included: boolean;
  backup_included: boolean;
  custom_domains_included: boolean;
  status: string;
  valid_until: string;
}

export interface BackupJobItem {
  id: string;
  name: string;
  status: string;
  size_bytes: number;
  created_at: string;
  completed_at?: string;
}

export interface BackupItem {
  id: string;
  filename: string;
  backup_type: string;
  size_bytes: number;
  storage_location: string;
  status: string;
  created_at: string;
}
