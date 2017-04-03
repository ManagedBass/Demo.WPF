using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using ManagedBass;

namespace Synthesizer
{
    public class MainViewModel : NotifyBase
    {
        #region Fields
        static BassInfo _info;
        int _stream;

        static readonly SynthKey[] Keys =
        {
            Key.Q,
            Key.D2,
            Key.W,
            Key.D3,
            Key.E,
            Key.R,
            Key.D5,
            Key.T,
            Key.D6,
            Key.Y,
            Key.D7,
            Key.U,
            Key.I,
            Key.D9,
            Key.O,
            Key.D0,
            Key.P,
            Key.OemPlus,
            Key.OemOpenBrackets,
            Key.OemCloseBrackets
        };

        const double MaxVol = 0.22 * 32768;
        const double Decay = MaxVol / 4000;

        static readonly SynthEffect[] Fx =
        {
            EffectType.DXChorus,
            EffectType.DXCompressor,
            EffectType.DXDistortion,
            EffectType.DXEcho,
            EffectType.DXFlanger,
            EffectType.DXGargle,
            EffectType.DX_I3DL2Reverb,
            EffectType.DXParamEQ,
            EffectType.DXReverb
        };
        #endregion

        ~MainViewModel()
        {
            Bass.Free();
        }

        public MainViewModel()
        {
            #region InitBASS
            // allows lower latency on Vista and newer
            Bass.VistaTruePlayPosition = false;

            // 10ms update period
            Bass.UpdatePeriod = 10;

            // initialize default output device (and measure latency)
            if (!Bass.Init(-1, 44100, DeviceInitFlags.Latency))
            {
                MessageBox.Show("Can't initialize device");

                Application.Current.Shutdown();
            }

            Bass.GetInfo(out _info);

            BufferLength = _info.MinBufferLength <= 0 ? 41 : 10 + _info.MinBufferLength + 1;
            #endregion

            IncreaseBufferCommand = new DelegateCommand(() => BufferLength++);
            DecreaseBufferCommand = new DelegateCommand(() => BufferLength--);
        }

        public ICommand IncreaseBufferCommand { get; }
        public ICommand DecreaseBufferCommand { get; }

        #region Key Handlers
        public void OnKeyUp(Key K)
        {
            if (K >= Key.F1 && K <= Key.F9)
            {
                var effect = Fx[K - Key.F1];

                if (effect.Handle != 0)
                {
                    Bass.ChannelRemoveFX(_stream, effect.Handle);

                    effect.Handle = 0;

                    Status = $"Effect {effect} = Off";
                }
                else
                {
                    // set the effect, not bothering with parameters (use defaults)
                    if ((effect.Handle = Bass.ChannelSetFX(_stream, effect.Name, 0)) != 0)
                        Status = $"Effect {effect} = On";
                }
            }
            else
            {
                foreach (var key in Keys.Where(Key => Key.Key == K && Math.Abs(Key.Volume) >= Decay))
                    key.Volume -= Decay; // trigger key fadeout                
            }
        }

        public void OnKeyDown(Key K)
        {
            foreach (var key in Keys.Where(Key => Key.Key == K && Key.Volume < MaxVol))
            {
                key.Position = 0;

                // start key (setting "vol" slightly higher than MAXVOL to cover any rounding-down)
                key.Volume = MaxVol + Decay / 2;
            }
        }
        #endregion

        static int Procedure(int Handle, IntPtr Ptr, int Length, IntPtr User)
        {
            var n = (int)Math.Round(Length / 2.0);

            var buffer = new short[n];

            for (var i = 0; i < Keys.Length; ++i)
            {
                var key = Keys[i];

                if (Math.Abs(key.Volume) <= Decay)
                    continue;

                var omega = 2 * Math.PI * Math.Pow(2.0, (i + 3) / 12.0) * 440.0 / _info.SampleRate;

                for (var j = 0; j < n; j += 2)
                {
                    // left and right channels are the same
                    buffer[j + 1] = buffer[j] = (short)(buffer[j] + Math.Sin(key.Position) * key.Volume);

                    key.Position += omega;

                    if (key.Volume < MaxVol)
                    {
                        key.Volume -= Decay;

                        // faded-out
                        if (key.Volume <= Decay)
                            break;
                    }
                }

                key.Position %= 2 * Math.PI;
            }

            Marshal.Copy(buffer, 0, Ptr, n);

            return Length;
        }

        #region Properties
        string _status = "Ready";

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public int BufferLength
        {
            get { return Bass.PlaybackBufferLength; }
            set
            {
                // Recreate stream with smaller/larger buffer
                Bass.StreamFree(_stream);

                Bass.PlaybackBufferLength = value;
                OnPropertyChanged();

                _stream = Bass.CreateStream(_info.SampleRate != 0 ? _info.SampleRate : 44100, 2, 0, Procedure);

                // set effects on the new stream
                foreach (var effect in Fx.Where(effect => effect.Handle != 0))
                    effect.Handle = Bass.ChannelSetFX(_stream, effect.Name, 0);

                Bass.ChannelPlay(_stream);
            }
        }

        public int Latency => _info.Latency;
        public int MinBufferLength => _info.MinBufferLength;
        public int DSVersion => _info.DSVersion;
        public string EffectsStatus => _info.DSVersion < 8 ? "Disabled" : "Enabled";
        #endregion
    }
}