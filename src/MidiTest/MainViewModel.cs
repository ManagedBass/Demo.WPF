using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using ManagedBass;
using ManagedBass.Midi;
using Microsoft.Win32;

namespace MidiTest
{
    public class MainViewModel : NotifyBase
    {
        #region Fields
        readonly OpenFileDialog _ofd;
        int _chan;
        int _font;
        int _originalTempo;
        readonly Timer _timer;
        #endregion

        ~MainViewModel()
        {
            Bass.Free();
            Bass.PluginFree(0);

            _timer.Stop();
        }

        public MainViewModel()
        {
            _ofd = new OpenFileDialog();

            _timer = new Timer(1000);

            _timer.Elapsed += TimerOnTick;

            InitBass();

            _timer.Start();

            OpenCommand = new DelegateCommand(OpenExecuted);

            ReplaceFontCommand = new DelegateCommand(ReplaceFontExecuted);
        }

        #region Commands
        public ICommand OpenCommand { get; }
        public ICommand ReplaceFontCommand { get; }

        void OpenExecuted()
        {
            _ofd.Filter = "MIDI files (mid/midi/rmi/kar)|*.mid;*.midi;*.rmi;*.kar|All files|*.*";

            if (!_ofd.ShowDialog().Value)
                return;

            Bass.StreamFree(_chan); // free old stream before opening new

            Lyrics = null; // clear lyrics display

            if ((_chan = BassMidi.CreateStream(_ofd.FileName, 0, 0, BassFlags.Loop | (Effects ? 0 : BassFlags.MidiNoFx), 1)) == 0)
            {
                // it ain't a MIDI
                FileName = "Click here to Open File...";
                MessageBox.Show("Can't play the file");
                return;
            }

            FileName = Path.GetFileName(_ofd.FileName);

            // set the title (track name of first track)
            MidiMarker titlemark;
            if (BassMidi.StreamGetMark(_chan, MidiMarkerType.TrackName, 0, out titlemark) && titlemark.Track == 0)
                FileName += $" - {titlemark.Text}";

            // set looping syncs
            MidiMarker loopmark;
            if (FindMarker(_chan, "loopend", out loopmark)) // found a loop end point
                Bass.ChannelSetSync(_chan, SyncFlags.Position | SyncFlags.Mixtime, loopmark.Position, LoopSync);
            // set a sync there
            Bass.ChannelSetSync(_chan, SyncFlags.End | SyncFlags.Mixtime, 0, LoopSync);
            // set one at the end too (eg. in case of seeking past the loop point)

            // clear lyrics buffer and set lyrics syncs
            MidiMarker lyricmark;
            if (BassMidi.StreamGetMark(_chan, MidiMarkerType.Lyric, 0, out lyricmark)) // got lyrics
                Bass.ChannelSetSync(_chan, SyncFlags.MidiMarker, (int)MidiMarkerType.Lyric, LyricSync,
                    new IntPtr((int)MidiMarkerType.Lyric));

            else if (BassMidi.StreamGetMark(_chan, MidiMarkerType.Text, 20, out lyricmark))
                // got text instead (over 20 of them)
                Bass.ChannelSetSync(_chan, SyncFlags.MidiMarker, (int)MidiMarkerType.Text, LyricSync,
                    new IntPtr((int)MidiMarkerType.Text));

            Bass.ChannelSetSync(_chan, SyncFlags.End, 0, EndSync);
            // override the initial tempo, and set a sync to override tempo events and another to override after seeking
            SetTempo(true);
            Bass.ChannelSetSync(_chan, SyncFlags.MidiEvent | SyncFlags.Mixtime, (int)MidiEventType.Tempo, TempoSync);
            Bass.ChannelSetSync(_chan, SyncFlags.Seeking | SyncFlags.Mixtime, 0, TempoSync);

            // get default soundfont in case of matching soundfont being used
            var sf = new MidiFont[1];

            if (BassMidi.StreamGetFonts(_chan, sf, 1) != 0)
                _font = sf[0].Handle;

            Bass.ChannelPlay(_chan);
        }

        void ReplaceFontExecuted()
        {
            _ofd.Filter = "Soundfonts (sf2/sf2pack)|*.sf2;*.sf2pack|All files|*.*";

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
            BassMidi.StreamSetFonts(_chan, sf, 1); // set for current stream too
            BassMidi.FontFree(_font); // free old soundfont
            _font = newfont;
        }
        #endregion

        void InitBass()
        {
            // Setup output - Default device
            if (!Bass.Init())
            {
                MessageBox.Show("Can't Initialize Device");
                Application.Current.Shutdown();
            }

            // Get default font (28mbgm.sf2/ct8mgm.sf2/ct4mgm.sf2/ct2mgm.sf2 if available)
            var sf = new MidiFont[1];

            if (BassMidi.StreamGetFonts(0, sf, 1) != 0)
                _font = sf[0].Handle;

            // FX enabled by default
            Effects = true;

            // load optional plugins for packed soundfonts (others may be used too)
            Bass.PluginLoad("bassflac.dll");
            Bass.PluginLoad("basswv.dll");
        }

        void TimerOnTick(object Sender, EventArgs Args)
        {
            MidiFontInfo i;
            if (BassMidi.FontGetInfo(_font, out i))
                SoundFontStatus = $"Name: {i.Name}\nLoaded: {i.SampleDataLoaded} / {i.SampleDataSize}";
        }

        void SetTempo(bool Reset)
        {
            if (Reset)
                _originalTempo = BassMidi.StreamGetEvent(_chan, 0, MidiEventType.Tempo); // get the file's tempo

            Tempo = (int)(_originalTempo * _tempoScale); // set tempo
        }

        #region Syncs
        void EndSync(int Handle, int Channel, int Data, IntPtr User) => Lyrics = null;

        void LyricSync(int Handle, int Channel, int Data, IntPtr User)
        {
            MidiMarker mark;
            BassMidi.StreamGetMark(Channel, (MidiMarkerType)User.ToInt32(), Data, out mark); // get the lyric/text

            var text = mark.Text;

            switch (text[0])
            {
                case '@':
                    break; // skip info

                case '\\':
                    // clear display
                    Lyrics = text.Substring(1);
                    break;

                default:
                    // new line
                    if (text[0] == '/')
                        Lyrics += '\n' + text.Substring(1);
                    else Lyrics += text;

                    break;
            }
        }

        void TempoSync(int Handle, int Channel, int Data, IntPtr User)
        {
            SetTempo(true); // override the tempo
        }

        void LoopSync(int Handle, int Channel, int Data, IntPtr User)
        {
            MidiMarker mark;

            Bass.ChannelSetPosition(Channel,
                FindMarker(Channel, "loopstart", out mark) ? mark.Position : 0,
                PositionFlags.Bytes | PositionFlags.MIDIDecaySeek);
        }
        #endregion

        // look for a marker (eg. loop points)
        static bool FindMarker(int handle, string text, out MidiMarker mark)
        {
            for (var a = 0; BassMidi.StreamGetMark(handle, MidiMarkerType.Marker, a, out mark); a++)
                if (mark.Text == text)
                    return true; // found it

            return false;
        }

        #region Properties
        int _tempo;

        // set tempo, return bpm
        public int Tempo
        {
            get { return _tempo == 0 ? 0 : 60000000 / _tempo; }
            set
            {
                _tempo = value;

                BassMidi.StreamEvent(_chan, 0, MidiEventType.Tempo, value);

                OnPropertyChanged();
            }
        }

        double _tempoScale = 1;
        double _sliderVal = 10;

        public double SliderValue
        {
            get { return _sliderVal; }
            set
            {
                _sliderVal = value;

                _tempoScale = 1 / ((30 - value) / 20.0);

                SetTempo(false);

                OnPropertyChanged();
            }
        }

        bool _effects = true;

        public bool Effects
        {
            get { return _effects; }
            set
            {
                _effects = value;

                Bass.ChannelFlags(_chan, value ? 0 : BassFlags.MidiNoFx, BassFlags.MidiNoFx);

                OnPropertyChanged();
            }
        }

        string _soundFontStatus = "No SoundFont";

        public string SoundFontStatus
        {
            get { return _soundFontStatus; }
            set
            {
                _soundFontStatus = value;
                OnPropertyChanged();
            }
        }

        string _lyrics;

        public string Lyrics
        {
            get { return _lyrics; }
            set
            {
                _lyrics = value;
                OnPropertyChanged();
            }
        }

        string _file = "Click here to Open File...";

        public string FileName
        {
            get { return _file; }
            set
            {
                _file = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}