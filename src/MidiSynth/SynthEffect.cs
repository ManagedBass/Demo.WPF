using ManagedBass;

namespace MidiSynth
{
    class SynthEffect
    {
        public EffectType Name;
        public int Handle;

        public static implicit operator SynthEffect(EffectType Name) => new SynthEffect { Name = Name };
    }
}