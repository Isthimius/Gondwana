using Gondwana.Common.Effects;
using System;

namespace Gondwana.Drawing.Effects
{
    public sealed class Earthquake : DisplayEffectBase
    {
        public Earthquake(int duration)
            : base(duration)
        {
        }

        public override void ApplyEffect()
        {
            throw new NotImplementedException();
        }
    }
}