import { describe, it, expect, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { MailboxDelegatePicker } from './MailboxDelegatePicker';

describe('MailboxDelegatePicker sender grants', () => {
  it('exposes independent Send As and Send on Behalf flags', () => {
    const onChange = vi.fn();
    const selected = [{ user_id: 'u1', access_level: 'write' as const, can_send_as: false, can_send_on_behalf: false }];
    render(<MailboxDelegatePicker users={[{ id: 'u1', email: 'user@example.test', name: 'User', mailbox_kind: 'user', is_active: true } as any]} domain="example.test" selected={selected} onChange={onChange} />);
    fireEvent.click(screen.getByRole('checkbox', { name: 'Send As user@example.test' }));
    expect(onChange).toHaveBeenCalledWith([{ ...selected[0], can_send_as: true }]);
    fireEvent.click(screen.getByRole('checkbox', { name: 'Send on Behalf user@example.test' }));
    expect(onChange).toHaveBeenLastCalledWith([{ ...selected[0], can_send_on_behalf: true }]);
  });
});
