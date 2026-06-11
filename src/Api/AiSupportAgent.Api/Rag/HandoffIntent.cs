namespace AiSupportAgent.Api.Rag;

public static class HandoffIntent
{
    public static bool WantsHuman(string msg)
    {
        var l = msg.ToLowerInvariant();
        return l.Contains("talk to a human") || l.Contains("speak to a human")
            || l.Contains("speak to someone") || l.Contains("real person")
            || l.Contains("customer service") || l.Contains("talk to an agent")
            || (l.Contains("human") && (l.Contains("talk") || l.Contains("speak") || l.Contains("connect")));
    }
}