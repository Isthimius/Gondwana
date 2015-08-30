using Gondwana.Common.Effects;
using Gondwana.Common.Enums;
using System;

namespace Gondwana.Drawing.Effects
{
    public sealed class Erase : DisplayEffectBase
    {
        public Erase(int duration, EffectDirection direction)
            : base(duration, direction)
        {
        }

        public override void ApplyEffect()
        {
            throw new NotImplementedException();
        }
    }
}