-- Cleanup script for mailboxes using @miautrix.local
-- This script removes all records related to mailboxes with the .local domain.

DO $$
DECLARE
    mailbox_rec RECORD;
BEGIN
    -- Iterate through all mailboxes with .local address
    FOR mailbox_rec IN 
        SELECT id FROM mailboxes WHERE address LIKE '%@miautrix.local'
    LOOP
        -- Remove child records first (Foreign Key constraints)
        DELETE FROM message_flags WHERE mailbox_id = mailbox_rec.id;
        DELETE FROM flag_alert_configurations WHERE mailbox_id = mailbox_rec.id;
        DELETE FROM messages WHERE mailbox_id = mailbox_rec.id;
        DELETE FROM folders WHERE mailbox_id = mailbox_rec.id;
        DELETE FROM mailbox_delegates WHERE mailbox_id = mailbox_rec.id;
        
        -- Finally remove the mailbox
        DELETE FROM mailboxes WHERE id = mailbox_rec.id;
        
        RAISE NOTICE 'Deleted mailbox %', mailbox_rec.id;
    END LOOP;
END $$;

-- Optional: Cleanup users with .local email if they were created as part of these tests
-- Note: We only do this if the user has no other valid memberships or is explicitly a test user.
-- For safety, we'll just list them first.
SELECT id, email FROM users WHERE email LIKE '%@miautrix.local';
