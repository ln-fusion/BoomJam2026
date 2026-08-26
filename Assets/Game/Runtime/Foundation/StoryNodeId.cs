namespace Game.Foundation
{
    /// <summary>Strongly typed stable identifier for a story node.</summary>
    [System.Serializable]
    public sealed class StoryNodeId : StrongId<StoryNodeId>
    {
        /// <summary>Creates a story node identifier.</summary>
        /// <param name="value">Non-empty stable value.</param>
        public StoryNodeId(string value) : base(value) { }
    }
}
