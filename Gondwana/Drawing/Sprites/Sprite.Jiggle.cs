using System.Drawing;

namespace Gondwana.Drawing.Sprites;

public partial class Sprite
{
    // ------------------------------------------------------------
    // Jiggle configuration/state
    // ------------------------------------------------------------

    private bool _isJiggling;
    private float _jiggleElapsedSeconds;

    private float _jiggleIntensityX;
    private float _jiggleIntensityY;
    private float _jiggleSpeed;
    private float _jiggleScaleIntensity;
    private bool _jiggleAffectsScale;
    private bool _jiggleLoop;
    private float _jiggleDurationSeconds; // ignored if loop = true

    // Per-sprite phase offsets so multiple sprites do not move in sync
    private float _jigglePhaseX1;
    private float _jigglePhaseX2;
    private float _jigglePhaseY1;
    private float _jigglePhaseY2;
    private float _jigglePhaseScale;

    // Current computed visual offsets
    private float _prevJiggleOffsetX;
    private float _prevJiggleOffsetY;
    private float _jiggleOffsetX;
    private float _jiggleOffsetY;
    private float _jiggleScale = 1f;

    // Optional decay support
    private bool _jiggleDecay;
    private float _jiggleStartIntensityX;
    private float _jiggleStartIntensityY;
    private float _jiggleStartScaleIntensity;

    /// <summary>
    /// Gets whether the sprite is currently jiggling.
    /// </summary>
    public bool IsJiggling => _isJiggling;

    /// <summary>
    /// Gets the current visual jiggle offset in screen/world pixels,
    /// depending on how the sprite is being drawn.
    /// </summary>
    public PointF JiggleOffset => new(_jiggleOffsetX, _jiggleOffsetY);

    /// <summary>
    /// Gets the current visual jiggle scale multiplier.
    /// This is intended to be applied at draw time only.
    /// </summary>
    public float JiggleScale => _jiggleScale;

    /// <summary>
    /// Starts a smooth, randomized-looking jiggle animation.
    /// This jiggle is visual only and does not modify RenderSize
    /// or collision bounds.
    /// </summary>
    /// <param name="intensityX">Maximum horizontal jiggle in pixels.</param>
    /// <param name="intensityY">Maximum vertical jiggle in pixels.</param>
    /// <param name="speed">How quickly the jiggle animates.</param>
    /// <param name="durationSeconds">
    /// Duration of the jiggle. Ignored if loop is true.
    /// </param>
    /// <param name="loop">Whether the jiggle should continue indefinitely.</param>
    /// <param name="affectsScale">
    /// Whether to add a subtle visual size wobble during the jiggle.
    /// </param>
    /// <param name="scaleIntensity">
    /// Maximum scale wobble amount. Example: 0.03 = about ±3%.
    /// </param>
    /// <param name="decay">
    /// Whether the jiggle should gradually fade out over its duration.
    /// </param>
    public void StartJiggle(
        float intensityX = 2f,
        float intensityY = 2f,
        float speed = 8f,
        float durationSeconds = 0.25f,
        bool loop = false,
        bool affectsScale = false,
        float scaleIntensity = 0.02f,
        bool decay = true)
    {
        _isJiggling = true;
        _jiggleElapsedSeconds = 0f;

        _jiggleIntensityX = MathF.Max(0f, intensityX);
        _jiggleIntensityY = MathF.Max(0f, intensityY);
        _jiggleSpeed = MathF.Max(0f, speed);
        _jiggleScaleIntensity = MathF.Max(0f, scaleIntensity);
        _jiggleAffectsScale = affectsScale;
        _jiggleLoop = loop;
        _jiggleDurationSeconds = MathF.Max(0f, durationSeconds);

        _jiggleDecay = decay;
        _jiggleStartIntensityX = _jiggleIntensityX;
        _jiggleStartIntensityY = _jiggleIntensityY;
        _jiggleStartScaleIntensity = _jiggleScaleIntensity;

        // Randomize phases so identical sprites do not move in lockstep
        _jigglePhaseX1 = Random.Shared.NextSingle() * MathF.PI * 2f;
        _jigglePhaseX2 = Random.Shared.NextSingle() * MathF.PI * 2f;
        _jigglePhaseY1 = Random.Shared.NextSingle() * MathF.PI * 2f;
        _jigglePhaseY2 = Random.Shared.NextSingle() * MathF.PI * 2f;
        _jigglePhaseScale = Random.Shared.NextSingle() * MathF.PI * 2f;

        _jiggleOffsetX = 0f;
        _jiggleOffsetY = 0f;
        _jiggleScale = 1f;
    }

    /// <summary>
    /// Stops the current jiggle effect.
    /// </summary>
    public void StopJiggle()
    {
        _isJiggling = false;
        _prevJiggleOffsetX = 0f;
        _prevJiggleOffsetY = 0f;
        _jiggleOffsetX = 0f;
        _jiggleOffsetY = 0f;
        _jiggleScale = 1f;
    }

    /// <summary>
    /// Advances the jiggle animation by the specified delta time.
    /// This should be called from the same engine update path that advances
    /// other sprite animations.
    /// </summary>
    /// <param name="deltaSeconds">Elapsed time in seconds.</param>
    internal void AdvanceJiggle(float deltaSeconds)
    {
        if (!_isJiggling)
            return;

        _prevJiggleOffsetX = _jiggleOffsetX;
        _prevJiggleOffsetY = _jiggleOffsetY;
        _jiggleElapsedSeconds += deltaSeconds;

        if (!_jiggleLoop && _jiggleDurationSeconds > 0f && _jiggleElapsedSeconds >= _jiggleDurationSeconds)
        {
            StopJiggle();
            return;
        }

        float intensityX = _jiggleIntensityX;
        float intensityY = _jiggleIntensityY;
        float scaleIntensity = _jiggleScaleIntensity;

        if (_jiggleDecay && !_jiggleLoop && _jiggleDurationSeconds > 0f)
        {
            float lifeT = _jiggleElapsedSeconds / _jiggleDurationSeconds;
            lifeT = Math.Clamp(lifeT, 0f, 1f);

            float decayFactor = 1f - lifeT;

            intensityX = _jiggleStartIntensityX * decayFactor;
            intensityY = _jiggleStartIntensityY * decayFactor;
            scaleIntensity = _jiggleStartScaleIntensity * decayFactor;
        }

        float t = _jiggleElapsedSeconds * _jiggleSpeed;

        // Layer a few frequencies to fake randomness without harsh frame-to-frame jumps
        float xWave =
            MathF.Sin(t + _jigglePhaseX1) +
            (0.5f * MathF.Sin((t * 2.37f) + _jigglePhaseX2));

        float yWave =
            MathF.Cos((t * 1.13f) + _jigglePhaseY1) +
            (0.5f * MathF.Cos((t * 2.91f) + _jigglePhaseY2));

        // Normalize the layered wave somewhat
        _jiggleOffsetX = xWave * 0.6667f * intensityX;
        _jiggleOffsetY = yWave * 0.6667f * intensityY;

        if (_jiggleAffectsScale && scaleIntensity > 0f)
        {
            float scaleWave =
                MathF.Sin((t * 1.41f) + _jigglePhaseScale) +
                (0.35f * MathF.Sin((t * 3.17f) + (_jigglePhaseScale * 0.5f)));

            _jiggleScale = 1f + (scaleWave * 0.7407f * scaleIntensity);
            if (_jiggleScale < 0.01f)
                _jiggleScale = 0.01f;
        }
        else
        {
            _jiggleScale = 1f;
        }

        bool changed = Math.Abs(_jiggleOffsetX - _prevJiggleOffsetX) > 0.01f ||
                       Math.Abs(_jiggleOffsetY - _prevJiggleOffsetY) > 0.01f;

        if (changed && _sceneLayer != null)
        {
            var rect = DrawLocationWorld;

            int inflateX = (int)MathF.Ceiling(_jiggleIntensityX + 1f);
            int inflateY = (int)MathF.Ceiling(_jiggleIntensityY + 1f);

            rect.Inflate(inflateX, inflateY);

            _sceneLayer.RefreshQueue.AddWorldRect(rect);
        }
    }

    /// <summary>
    /// Applies the current jiggle effect to a destination rectangle.
    /// This is a visual-only transform and does not modify RenderSize.
    /// </summary>
    /// <param name="destRect">The rectangle that would normally be drawn.</param>
    /// <returns>A modified rectangle including current jiggle offset and optional scale wobble.</returns>
    internal RectangleF ApplyJiggleToDestRect(RectangleF destRect)
    {
        if (!_isJiggling)
            return destRect;

        RectangleF result = destRect;
        result.Offset(_jiggleOffsetX, _jiggleOffsetY);

        if (Math.Abs(_jiggleScale - 1f) > 0.0001f)
        {
            float centerX = result.X + (result.Width / 2f);
            float centerY = result.Y + (result.Height / 2f);

            result.Width *= _jiggleScale;
            result.Height *= _jiggleScale;
            result.X = centerX - (result.Width / 2f);
            result.Y = centerY - (result.Height / 2f);
        }

        return result;
    }

    /// <summary>
    /// Starts a one-shot impact-style jiggle.
    /// Handy for hits, invalid selections, bump feedback, and similar moments.
    /// </summary>
    public void JiggleOnce(
        float intensityX = 2f,
        float intensityY = 2f,
        float speed = 12f,
        float durationSeconds = 0.18f,
        bool affectsScale = false,
        float scaleIntensity = 0.015f)
    {
        StartJiggle(
            intensityX: intensityX,
            intensityY: intensityY,
            speed: speed,
            durationSeconds: durationSeconds,
            loop: false,
            affectsScale: affectsScale,
            scaleIntensity: scaleIntensity,
            decay: true);
    }
}