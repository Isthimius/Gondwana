namespace Gondwana.Demos.Slider;

/// <summary>
/// Bridges synchronous engine score updates to asynchronous browser storage.
/// Initial scores are prefetched before game startup; writes are scheduled by the UI.
/// </summary>
internal sealed class SliderBrowserScoreStore : ISliderScoreStore
{
    private readonly Action<string> _persist;
    private IReadOnlyList<SliderScore> _scores;

    internal SliderBrowserScoreStore(IReadOnlyList<SliderScore> initial, Action<string> persist)
    {
        _scores = initial ?? throw new ArgumentNullException(nameof(initial));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
    }

    public IReadOnlyList<SliderScore> LoadScores() => _scores;

    public void SaveScores(IReadOnlyList<SliderScore> scores)
    {
        _scores = scores.ToArray();
        _persist(SliderPersistence.SerializeScores(_scores));
    }
}
