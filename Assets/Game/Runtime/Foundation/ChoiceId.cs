namespace Game.Foundation
{
    /// <summary>Strongly typed stable identifier for a story choice.</summary>
    [System.Serializable]
    public sealed class ChoiceId : StrongId<ChoiceId>
    {
        /// <summary>Creates a choice identifier.</summary>
        /// <param name="value">Non-empty stable value.</param>
        public ChoiceId(string value) : base(value) { }
    }
}
