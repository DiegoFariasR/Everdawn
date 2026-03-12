namespace GameCore.World
{
    /// <summary>
    /// A selectable option inside a Location. Activities start EventFlows.
    /// </summary>
    public sealed record Activity(string Id, string DisplayName);
}
