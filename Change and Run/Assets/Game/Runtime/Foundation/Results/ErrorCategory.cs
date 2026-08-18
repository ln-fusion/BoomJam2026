namespace Game.Foundation.Results
{
    public enum ErrorCategory
    {
        None = 0,
        Validation,
        Content,
        SaveIo,
        SaveCorrupt,
        PlatformUnavailable,
        PlatformSync,
        SceneTransition,
        SimulationPerformance,
        Unexpected
    }
}
