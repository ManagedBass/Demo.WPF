using ManagedBass;

namespace Synthesizer
{
    class SynthEffect
    {
        public EffectType Name;
        public int Handle;

        public static implicit operator SynthEffect(EffectType Name) => new SynthEffect { Name = Name };

        public override string ToString() => Name.ToString();
    }
}