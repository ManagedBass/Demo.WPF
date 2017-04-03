using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ManagedBass;
using ManagedBass.Midi;
using Microsoft.Win32;

namespace MidiSynth
{
    public class MainViewModel : NotifyBase
    {
        #region Fields
        BassInfo _info;
        int _stream, _font;
        readonly OpenFileDialog _ofd;

        static readonly Key[] Keys =
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

        static readonly SynthEffect[] Fx =
        {
            EffectType.DXReverb,
            EffectType.DXEcho,
            EffectType.DXChorus,
            EffectType.DXFlanger,
            EffectType.DXDistortion
        };

        public ObservableCollection<KeyValuePair<int, string>> Presets { get; } = new ObservableCollection<KeyValuePair<int, string>>();

        public ObservableCollection<KeyValuePair<int, string>> Devices { get; } = new ObservableCollection<KeyValuePair<int, string>>();
        #endregion

        public MainViewModel()
        {
            _ofd = new OpenFileDialog
            {
                Filter = "Soundfonts (sf2/sf2pack/sfz)|*.sf2;*.sf2pack;*.sfz|All files|*.*"
            };

            InitBass();

            ResetCommand = new DelegateCommand(() =>
            {
                BassMidi.StreamEvent(_stream, 0, MidiEventType.System, (int)MidiSystem.GS); // send system reset event

                if (_drums)
                    BassMidi.StreamEvent(_stream, 16, MidiEventType.Drums, 1); // send drum switch event

                BassMidi.StreamEvent(_stream, 16, MidiEventType.Program, _preset); // send program/preset event
            });

            IncreaseBufferLengthCommand = new DelegateCommand(() => BufferLength++);
            DecreaseBufferLengthCommand = new DelegateCommand(() => BufferLength--);

            OpenCommand = new DelegateCommand(OpenSoundFont);
        }

        #region Command
        public ICommand ResetCommand { get; }

        public ICommand IncreaseBufferLengthCommand { get; }

        public ICommand DecreaseBufferLengthCommand { get; }

        public ICommand OpenCommand { get; }
        #endregion

        void OpenSoundFont()
        {
            if (!_ofd.ShowDialog().Value)
                return;

            var newfont = BassMidi.FontInit(_ofd.FileName, FontInitFlags.Unicode);

            if (newfont == 0)
                return;

            var sf = new[]
            {
                new MidiFont
                {
                    Handle = newfont,
                    Preset = -1, // use all presets
                    Bank = 0 // use default bank(s)
                }
            };

            BassMidi.StreamSetFonts(0, sf, 1); // set default soundfont
            BassMidi.StreamSetFonts(_stream, sf, 1); // apply to current stream too

            BassMidi.FontFree(_font); // free old soundfont

            _font = newfont;

            MidiFontInfo i;
            BassMidi.FontGetInfo(_font, out i);

            SoundFont = string.IsNullOrWhiteSpace(i.Name) ? Path.GetFileNameWithoutExtension(_ofd.FileName) : i.Name;

            if (i.Presets == 1)
            {
                // only 1 preset, auto-select it...
                var p = new int[1];
                BassMidi.FontGetPresets(_font, p);

                Drums = p[0].HiWord() == 128; // bank 128 = drums
                Preset = p[0].LoWord();

                MultiPresets = false;
            }
            else MultiPresets = true;

            UpdatePresetList();
        }

        void InitBass()
        {
            Bass.VistaTruePlayPosition = false; // allows lower latency on Vista and newer
            Bass.UpdatePeriod = 10; // 10ms update period

            // initialize default output device (and measure latency)
            if (!Bass.Init(-1, 44100, DeviceInitFlags.Latency))
            {
                MessageBox.Show("Can't initialize output device");
                Application.Current.Shutdown();
            }

            Bass.GetInfo(out _info);

            // default buffer size = update period + 'minbuf' + 1ms margin
            BufferLength = _info.MinBufferLength <= 0 ? 41 : 10 + _info.MinBufferLength + 1;

            // enumerate available input devices
            MidiDeviceInfo di;
            int dev;

            for (dev = 0; BassMidi.InGetDeviceInfo(dev, out di); dev++)
                Devices.Add(new KeyValuePair<int, string>(dev, di.Name));

            if (dev != 0)
            {
                // got sone, try to initialize one
                int a;
                for (a = 0; a < dev; a++)
                {
                    if (BassMidi.InInit(a, MidiInProc))
                    {
                        // succeeded, start it
                        _device = a;
                        BassMidi.InStart(Device);
                        break;
                    }
                }

                if (a == dev)
                    MessageBox.Show("Can't initialize MIDI device");
            }

            // get default font (28mbgm.sf2/ct8mgm.sf2/ct4mgm.sf2/ct2mgm.sf2 if available)
            var sf = new MidiFont[1];

            if (BassMidi.StreamGetFonts(0, sf, 1) != 0)
            {
                _font = sf[0].Handle;
                MidiFontInfo i;
                BassMidi.FontGetInfo(_font, out i);
                SoundFont = i.Name;
            }

            UpdatePresetList();

            // load optional plugins for packed soundfonts (others may be used too)
            Bass.PluginLoad("bassflac.dll");
            Bass.PluginLoad("basswv.dll");
        }

        // MIDI input function
        async void MidiInProc(int handle, double time, IntPtr buffer, int length, IntPtr user)
        {
            if (_chans16) // using 16 channels
                BassMidi.StreamEvents(_stream, MidiEventsMode.Raw, buffer, length); // send MIDI data to the MIDI stream
            else BassMidi.StreamEvents(_stream, (MidiEventsMode.Raw + 17) | MidiEventsMode.Sync, buffer, length); // send MIDI data to channel 17 in the MIDI stream

            Activity = Visibility.Visible;

            await Task.Delay(100);

            Activity = Visibility.Hidden;
        }

        void UpdatePresetList()
        {
            Presets.Clear();

            for (var a = 0; a < 128; ++a)
            {
                var name = BassMidi.FontGetPreset(_font, a, _drums ? 128 : 0) ?? ""; // get preset name

                Presets.Add(new KeyValuePair<int, string>(a, $"{a:D3}: {name}"));
            }

            Preset = 0;
        }

        // program/preset event sync function
        void ProgramEventSync(int handle, int channel, int data, IntPtr user)
        {
            Preset = data.LoWord();
            BassMidi.FontCompact(0); // unload unused samples
        }

        #region Key Handlers
        public void OnKeyDown(Key K, bool IsRepeat)
        {
            if (!Repeat && IsRepeat)
                return;

            for (var i = 0; i < Keys.Length; ++i)
            {
                if (K != Keys[i])
                    continue;

                BassMidi.StreamEvent(_stream, 16, MidiEventType.Note, BitHelper.MakeWord((byte)((Drums ? 36 : 60) + i), 100)); // send note on event
                break;
            }
        }

        public void OnKeyUp(Key K)
        {
            for (var i = 0; i < Keys.Length; ++i)
            {
                if (K != Keys[i])
                    continue;

                BassMidi.StreamEvent(_stream, 16, MidiEventType.Note, BitHelper.MakeWord((byte)((Drums ? 36 : 60) + i), 0)); // send note off event
                break;
            }
        }
        #endregion

        #region Properties
        bool _multiPresets = true;

        public bool MultiPresets
        {
            get { return _multiPresets; }
            set
            {
                _multiPresets = value;
                OnPropertyChanged();
            }
        }

        Visibility _activity = Visibility.Hidden;

        public Visibility Activity
        {
            get { return _activity; }
            set
            {
                _activity = value;
                OnPropertyChanged();
            }
        }

        public int BufferLength
        {
            get { return Bass.PlaybackBufferLength; }
            set
            {
                // update buffer length
                Bass.PlaybackBufferLength = value;

                // recreate the MIDI stream with new buffer length
                Bass.StreamFree(_stream);

                // create the MIDI stream (16 MIDI channels for device input + 1 for keyboard input)
                _stream = BassMidi.CreateStream(17, SincInterpolation ? BassFlags.SincInterpolation : 0, 1);

                // limit CPU usage to 75% (also enables async sample loading)
                Bass.ChannelSetAttribute(_stream, ChannelAttribute.MidiCPU, 75);

                // catch program/preset changes
                Bass.ChannelSetSync(_stream, SyncFlags.MidiEvent | SyncFlags.Mixtime, (int)MidiEventType.Program, ProgramEventSync);

                // send GS system reset event
                BassMidi.StreamEvent(_stream, 0, MidiEventType.System, (int)MidiSystem.GS);

                // set drum switch
                if (_drums)
                    BassMidi.StreamEvent(_stream, 16, MidiEventType.Drums, 1);

                // set program/preset
                BassMidi.StreamEvent(_stream, 16, MidiEventType.Program, _preset);

                // re-apply effects
                for (var a = 0; a < 5; a++)
                    if (Fx[a].Handle != 0)
                        Fx[a].Handle = Bass.ChannelSetFX(_stream, Fx[a].Name, a);

                Bass.ChannelPlay(_stream); // start it

                OnPropertyChanged();
            }
        }

        string _soundFont = "No Soundfont";

        public string SoundFont
        {
            get { return _soundFont; }
            set
            {
                _soundFont = value;
                OnPropertyChanged();
            }
        }

        int _device;

        public int Device
        {
            get { return _device; }
            set
            {
                if (_device == value)
                    return;

                BassMidi.InFree(_device); // free current input device

                _device = value;

                if (BassMidi.InInit(_device, MidiInProc)) // successfully initialized...
                    BassMidi.InStart(_device); // start it
                else MessageBox.Show("Can't initialize MIDI device");

                OnPropertyChanged();
            }
        }

        int _preset;

        public int Preset
        {
            get { return _preset; }
            set
            {
                _preset = value;

                // send program/preset event
                BassMidi.StreamEvent(_stream, 16, MidiEventType.Program, _preset);

                // unload unused samples
                BassMidi.FontCompact(0);

                OnPropertyChanged();
            }
        }

        bool _drums;

        public bool Drums
        {
            get { return _drums; }
            set
            {
                _drums = value;

                // send drum switch event
                BassMidi.StreamEvent(_stream, 16, MidiEventType.Drums, _drums ? 1 : 0);

                // preset is reset in drum switch
                Preset = BassMidi.StreamGetEvent(_stream, 16, MidiEventType.Program);

                UpdatePresetList();

                // unload unused samples
                BassMidi.FontCompact(0);

                OnPropertyChanged();
            }
        }

        bool _chans16;

        public bool Channels16
        {
            get { return _chans16; }
            set
            {
                _chans16 = value;
                OnPropertyChanged();
            }
        }

        public bool SincInterpolation
        {
            get { return _stream != 0 && Bass.ChannelHasFlag(_stream, BassFlags.SincInterpolation); }
            set
            {
                Bass.ChannelFlags(_stream, value ? BassFlags.SincInterpolation : 0, BassFlags.SincInterpolation);

                OnPropertyChanged();
            }
        }

        void SetFx(int Index, bool Value)
        {
            if (Value)
                Fx[Index].Handle = Bass.ChannelSetFX(_stream, Fx[Index].Name, Index);
            else
            {
                Bass.ChannelRemoveFX(_stream, Fx[Index].Handle);
                Fx[Index].Handle = 0;
            }
        }

        public bool Reverb
        {
            get { return Fx[0].Handle != 0; }
            set
            {
                SetFx(0, value);
                OnPropertyChanged();
            }
        }

        public bool Echo
        {
            get { return Fx[1].Handle != 0; }
            set
            {
                SetFx(1, value);
                OnPropertyChanged();
            }
        }

        public bool Chorus
        {
            get { return Fx[2].Handle != 0; }
            set
            {
                SetFx(2, value);
                OnPropertyChanged();
            }
        }

        public bool Flanger
        {
            get { return Fx[3].Handle != 0; }
            set
            {
                SetFx(3, value);
                OnPropertyChanged();
            }
        }

        public bool Distortion
        {
            get { return Fx[4].Handle != 0; }
            set
            {
                SetFx(4, value);
                OnPropertyChanged();
            }
        }

        bool _repeat;

        public bool Repeat
        {
            get { return _repeat; }
            set
            {
                _repeat = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}