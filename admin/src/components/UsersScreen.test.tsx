import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent, within } from '@testing-library/react';
import { UsersScreen } from './UsersScreen';
import { AdminApiClient } from '../api/client';
import { AdminUserItem, DomainItem, OrphanMailboxItem, SharedMailbox } from '../types';

const makeUser = (overrides: Partial<AdminUserItem> = {}): AdminUserItem => ({
  id: 'u-1',
  email: 'alice@example.com',
  name: 'Alice',
  is_active: true,
  role: 'member',
  mailbox_quota_bytes: 1073741824,
  mailbox_used_bytes: 1024,
  created_at: '2026-09-18T11:00:00Z',
  must_change_password: false,
  is_service: false,
  mailbox_kind: 'user',
  ...overrides,
});

const makeDomain = (name: string): DomainItem => ({
  id: `d-${name}`,
  name,
  is_verified: true,
  is_primary: true,
  transport_mode: 'local',
  cloudflare_zone_id: null,
  cloudflare_worker_url: null,
  created_at: '2026-09-18T11:00:00Z',
});

const makeOrphan = (overrides: Partial<OrphanMailboxItem> = {}): OrphanMailboxItem => ({
  id: 'orphan-1',
  email: 'ghost@example.com',
  name: 'Ghost Mailbox',
  kind: 'user',
  mailbox_quota_bytes: 1073741824,
  mailbox_used_bytes: 2048,
  is_active: true,
  created_at: '2026-09-18T11:00:00Z',
  message_count: 3,
  attachment_count: 2,
  ...overrides,
});

const makeSharedMailbox = (overrides: Partial<SharedMailbox> = {}): SharedMailbox => ({
  id: 'orphan-1',
  email: 'renamed@example.com',
  name: 'Ghost Mailbox',
  mailbox_quota_bytes: 1073741824,
  mailbox_used_bytes: 2048,
  is_active: true,
  created_at: '2026-09-18T11:00:00Z',
  delegates: [],
  ...overrides,
});

describe('UsersScreen — mailboxes without users', () => {
  let client: AdminApiClient;

  beforeEach(() => {
    client = new AdminApiClient('http://test');
    vi.restoreAllMocks();
    vi.spyOn(client, 'getUsers').mockResolvedValue({ data: [makeUser()] });
    vi.spyOn(client, 'getDomains').mockResolvedValue({ data: [makeDomain('example.com')] });
    vi.spyOn(client, 'getOrphanMailboxes').mockResolvedValue({ data: [makeOrphan()] });
  });

  it('shows the mailbox scope filter and only fetches orphans when it is selected', async () => {
    const getOrphans = vi.spyOn(client, 'getOrphanMailboxes');

    render(<UsersScreen client={client} />);

    await waitFor(() => {
      expect(screen.getByTestId('users-table')).toBeInTheDocument();
    });
    expect(getOrphans).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId('filter-mailbox-orphans'));

    await waitFor(() => {
      expect(screen.getByTestId('orphans-table')).toBeInTheDocument();
    });
    expect(getOrphans).toHaveBeenCalledTimes(1);

    const row = within(screen.getByTestId('orphans-table')).getByText('ghost@example.com').closest('tr')!;
    expect(within(row).getByText('Ghost Mailbox')).toBeInTheDocument();
    expect(within(row).getByText('3')).toBeInTheDocument();
    expect(within(row).getByText('2')).toBeInTheDocument();
    expect(screen.queryByTestId('users-table')).not.toBeInTheDocument();
  });

  it('assigns an orphan mailbox with a renamed address and a delegate', async () => {
    const delegate = makeUser({ id: 'u-2', email: 'bob@example.com', name: 'Bob' });
    vi.spyOn(client, 'getUsers').mockResolvedValue({ data: [makeUser(), delegate] });
    const assign = vi.spyOn(client, 'assignMailbox').mockResolvedValue({ data: makeSharedMailbox() });

    render(<UsersScreen client={client} />);
    fireEvent.click(screen.getByTestId('filter-mailbox-orphans'));

    await waitFor(() => {
      expect(screen.getByTestId('orphans-table')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('manage-mailbox-orphan-1'));

    const submit = screen.getByTestId('assign-mailbox-submit');
    expect(submit).toBeDisabled();

    fireEvent.change(screen.getByTestId('assign-mailbox-username'), { target: { value: 'renamed' } });
    fireEvent.click(screen.getByTestId('assign-delegate-u-2'));

    await waitFor(() => {
      expect(submit).not.toBeDisabled();
    });

    fireEvent.submit(screen.getByTestId('assign-mailbox-form'));

    await waitFor(() => {
      expect(assign).toHaveBeenCalledWith('orphan-1', expect.objectContaining({
        address: 'renamed@example.com',
        delegates: [{ user_id: 'u-2', access_level: 'read' }],
      }));
    });
  });

  it('keeps the delete button disabled until the mailbox address is typed exactly', async () => {
    const remove = vi.spyOn(client, 'deleteMailbox').mockResolvedValue({
      data: { messages_deleted: 3, attachments_deleted: 2, blobs_deleted: 4 },
    });

    render(<UsersScreen client={client} />);
    fireEvent.click(screen.getByTestId('filter-mailbox-orphans'));

    await waitFor(() => {
      expect(screen.getByTestId('orphans-table')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('manage-mailbox-orphan-1'));

    expect(screen.queryByTestId('delete-mailbox-confirm')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('arm-delete-mailbox'));
    const confirm = screen.getByTestId('confirm-delete-mailbox');
    expect(confirm).toBeDisabled();
    expect(remove).not.toHaveBeenCalled();

    fireEvent.change(screen.getByTestId('delete-mailbox-confirm-input'), { target: { value: 'wrong@example.com' } });
    expect(confirm).toBeDisabled();

    fireEvent.change(screen.getByTestId('delete-mailbox-confirm-input'), { target: { value: 'ghost@example.com' } });
    await waitFor(() => {
      expect(confirm).not.toBeDisabled();
    });

    fireEvent.click(confirm);

    await waitFor(() => {
      expect(remove).toHaveBeenCalledWith('orphan-1', { confirm_address: 'ghost@example.com' });
    });
  });
});
