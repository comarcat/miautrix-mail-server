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
  start_at?: string;
  end_at?: string;
  domain?: string;
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
  transport_mode: string;
  cloudflare_zone_id?: string | null;
  cloudflare_worker_url?: string | null;
}

export interface CreateDomainRequest {
  name: string;
  is_primary?: boolean;
  transport_mode?: string;
  cloudflare_zone_id?: string;
  cloudflare_worker_url?: string;
}

export interface UpdateDomainRequest {
  name?: string;
  is_primary?: boolean;
  dkim_selector?: string;
  spf_record?: string;
  dmarc_record?: string;
  transport_mode?: string;
  cloudflare_zone_id?: string;
  cloudflare_worker_url?: string;
}

export interface VerifyDomainResult {
  is_verified: boolean;
  message: string;
  transport_mode: string;
  dkim_status?: string | null;
  spf_status?: string | null;
  dmarc_status?: string | null;
  worker_status?: string | null;
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
  must_change_password: boolean;
  is_service: boolean;
  mailbox_kind: string;
}

export interface MailboxDelegate {
  user_id: string;
  email: string;
  name: string;
  access_level: 'read' | 'write';
}

export interface MailboxDelegateRequest {
  user_id: string;
  access_level: 'read' | 'write';
}

export interface SharedMailbox {
  id: string;
  email: string;
  name: string;
  mailbox_quota_bytes: number;
  mailbox_used_bytes: number;
  is_active: boolean;
  created_at: string;
  delegates: MailboxDelegate[];
}

export interface CreateSharedMailboxRequest {
  email: string;
  name: string;
  quota_bytes?: number;
  delegates?: MailboxDelegateRequest[];
}

export interface CreateUserRequest {
  email: string;
  name: string;
  password?: string;
  role?: string;
  quota_bytes?: number;
  must_change_password?: boolean;
  is_service?: boolean;
  mailbox_kind?: string;
}

export interface UpdateUserRequest {
  email?: string;
  name?: string;
  is_active?: boolean;
  role?: string;
  quota_bytes?: number;
  must_change_password?: boolean;
  is_service?: boolean;
}

export interface ResetPasswordRequest {
  password?: string;
  must_change_password: boolean;
  generate_password?: boolean;
}

export interface ResetPasswordResult {
  generated_password?: string | null;
}

export interface OrphanMailboxItem {
  id: string;
  email: string;
  name: string;
  kind: string;
  mailbox_quota_bytes: number;
  mailbox_used_bytes: number;
  is_active: boolean;
  created_at: string;
  message_count: number;
  attachment_count: number;
}

export interface AssignMailboxRequest {
  address: string;
  name?: string;
  delegates?: MailboxDelegateRequest[];
  quota_bytes?: number;
}

export interface DeleteMailboxRequest {
  confirm_address: string;
}

export interface DeleteMailboxResult {
  messages_deleted: number;
  attachments_deleted: number;
  blobs_deleted: number;
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
export interface TransportDashboardMetric {
  transport_mode: string;
  active_queued: number;
  retrying: number;
  dead_letters: number;
  outbound_delivered24h: number;
  outbound_delivered_total: number;
  successful_attempts24h: number;
  failed_attempts24h: number;
  total_attempts24h: number;
  retry_attempts24h: number;
  delivery_success_rate24h: number;
  last_delivery_attempt_at?: string | null;
  last_delivery_response_code?: number | null;
  last_delivery_error?: string | null;
}

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

  // Transport-aware outbound delivery metrics
  transport_breakdown: TransportDashboardMetric[];
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

export interface SecuritySettings {
  password_hashing_algorithm: string;
  argon2_memory_kb: number;
  argon2_iterations: number;
  argon2_parallelism: number;
  session_lifetime_minutes: number;
  refresh_lifetime_days: number;
  lockout_max_failed_attempts: number;
  lockout_duration_minutes: number;
  mfa_enforced: boolean;
}

export interface AntiSpamSettings {
  reject_score: number;
  quarantine_score: number;
  header_score: number;
  greylist_score: number;
  greylisting_enabled: boolean;
  spf_dmarc_enforcement_enabled: boolean;
}

export interface TenantInfo {
  id: string;
  slug: string;
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
