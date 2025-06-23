namespace Gondwana.Drawing.Effects;

public abstract class DisplayEffectBase
{
    public int Duration { get; private set; }
    public EffectDirection Direction { get; }

    protected DisplayEffectBase(int duration)
    {
        Duration = duration;
    }

    protected DisplayEffectBase(int duration, EffectDirection direction)
    {
        Duration = duration;
        Direction = direction;
    }

    public abstract void ApplyEffect();
}
