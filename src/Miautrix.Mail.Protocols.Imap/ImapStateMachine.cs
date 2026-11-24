namespace Miautrix.Mail.Protocols.Imap;

public static class ImapStateMachine
{
    public static bool IsAllowed(ImapState state, string command)
    {
        command = command.ToUpperInvariant();
        if (command is "CAPABILITY" or "NOOP" or "LOGOUT") return state != ImapState.LoggedOut;
        var authenticated = command is "ENABLE" or "LIST" or "LSUB" or "SUBSCRIBE" or "UNSUBSCRIBE" or "STATUS" or "SELECT" or "EXAMINE" or "CREATE" or "DELETE" or "RENAME" or "APPEND";
        var selected = authenticated || command is "CHECK" or "CLOSE" or "UNSELECT" or "EXPUNGE" or "SEARCH" or "FETCH" or "STORE" or "COPY" or "UID";
        return state switch
        {
            ImapState.NotAuthenticated => command is "LOGIN" or "AUTHENTICATE",
            ImapState.Authenticated => authenticated,
            ImapState.Selected => selected,
            _ => false
        };
    }
}
