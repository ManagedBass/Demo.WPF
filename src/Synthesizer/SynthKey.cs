using System.Windows.Input;

namespace Synthesizer
{
    class SynthKey
    {
        public Key Key;
        public double Volume, Position;

        public static implicit operator SynthKey(Key K) => new SynthKey { Key = K };
    }
}