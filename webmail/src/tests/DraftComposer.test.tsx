import { act, render } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ComposerView } from '../components/ComposerView';
import { webmailClient } from '../components/WebmailApiClient';

describe('externally-created draft hydration', () => {
  afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });

  it.each([
    ['BodyText only', { body: 'plain draft body' }, 'plain draft body'],
    ['BodyHtml only', { bodyHtml: '<p>html draft body</p>' }, 'html draft body'],
  ])('%s renders its body and an untouched autosave does not rewrite it', async (_name, body, expected) => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const save = vi.spyOn(webmailClient, 'upsertDraft').mockResolvedValue({ data: { draftId: 'draft-1', success: true, message: 'Draft saved.' } });
    vi.spyOn(webmailClient, 'getSignatures').mockResolvedValue({ data: [] });
    const { container } = render(
      <ComposerView
        accounts={[{ id: 'mailbox-1', address: 'owner@example.test' }]}
        defaultAccountId="mailbox-1"
        initialState={{ mode: 'draft', draftId: 'draft-1', ...body }}
        onDiscardClick={() => undefined}
        onSendClick={async () => undefined}
      />,
    );

    const editor = container.querySelector('[contenteditable="true"]');
    expect(editor?.textContent).toContain(expected);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000);
    });
    expect(save).not.toHaveBeenCalled();
  });
});
