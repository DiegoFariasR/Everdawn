using GameCore.Content;

namespace BattleSandbox.Web.Services
{
    /// <summary>
    /// The web client's entry point into GameCore.
    /// Mirrors the relationship UnityClient has with GameCore: the presentation layer
    /// calls through this facade rather than touching GameCore types directly.
    /// </summary>
    public sealed class WebGameClient
    {
        /// <summary>
        /// The loaded content source for this session.
        /// Pass to <c>IBattleScenario.CreateSetup</c> and to <c>ContentPipeline.Load</c>.
        /// </summary>
        public IContentSource ContentSource { get; }

        public WebGameClient(IContentSource contentSource)
        {
            ContentSource = contentSource;
        }

        // World map and inventory access will be added here as GameCore implements them.
    }
}
