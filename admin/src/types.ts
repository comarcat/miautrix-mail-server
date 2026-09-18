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
  meta: ApiMeta;
}

export interface QueueQueryParams {
  cursor?: string | null;
  limit?: number;
  status?: string;
  search?: string;
}
