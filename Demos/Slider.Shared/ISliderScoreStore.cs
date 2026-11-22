namespace Gondwana.Demos.Slider;

// Host-specific persistence keeps saved high scores durable on each platform.
internal interface ISliderScoreStore
{
    IReadOnlyList<SliderScore> LoadScores();
    void SaveScores(IReadOnlyList<SliderScore> scores);
}
