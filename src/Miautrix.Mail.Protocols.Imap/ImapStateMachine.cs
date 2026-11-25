namespace Miautrix.Mail.Protocols.Imap;

public static class ImapStateMachine
{
    public static bool IsAllowed(ImapState state, string command)
    {
        command = command.ToUpperInvariant();
        if (command is "CAPABILITY" or "NOOP" or "LOGOUT" or "ID") return state != ImapState.LoggedOut;
        var authenticated = command is "ENABLE" or "LIST" or "LSUB" or "SUBSCRIBE" or "UNSUBSCRIBE" or "STATUS" or "NAMESPACE" or "SELECT" or "EXAMINE" or "CREATE" or "DELETE" or "RENAME" or "APPEND";
        var selected = authenticated || command is "CHECK" or "CLOSE" or "UNSELECT" or "EXPUNGE" or "SEARCH" or "FETCH" or "STORE" or "COPY" or "MOVE" or "IDLE" or "UID";
        return state switch
        {
            ImapState.NotAuthenticated => command is "LOGIN" or "AUTHENTICATE" or "STARTTLS",
            ImapState.Authenticated => authenticated,
            ImapState.Selected => selected,
            _ => false
        };
    }
}
