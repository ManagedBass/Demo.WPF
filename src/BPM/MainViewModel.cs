using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ManagedBass;
using ManagedBass.Fx;
using Microsoft.Win32;

namespace BPM
{
    public class MainViewModel : NotifyBase
    {
        #region Fields
        int _chan, _bpmchan;
        ChannelInfo _info;
        readonly OpenFileDialog _ofd;
        readonly BPMProgressProcedure _progressProcedure;
        readonly BPMBeatProcedure _beatProcedure;
        readonly BPMProcedure _bpmProcedure;
        #endregion

        ~MainViewModel()
        {
            Bass.Free();
        }

        public MainViewModel()
        {
            _ofd = new OpenFileDialog
            {
                Filter = "Playable files|*.mo3; *.xm; *.mod; *.s3m; *.it; *.mtm; *.mp3; *.mp2; *.mp1; *.ogg; *.wav; *.aif|All files|*.*"
            };

            if (!Bass.Init())
            {
                MessageBox.Show("Can't initialize device");
                Application.Current.Shutdown();
            }

            _progressProcedure = GetBPM_ProgressCallback;
            _beatProcedure = GetBeatPos_Callback;
            _bpmProcedure = GetBPM_Callback;

            OpenCommand = new DelegateCommand(OpenFile);
        }

        void OpenFile()
        {
            if (!_ofd.ShowDialog().Value)
                return;

            // free decode bpm stream and resources
            BassFx.BPMFree(_bpmchan);

            // free tempo, stream, music & bpm/beat callbacks
            Bass.StreamFree(_chan);
            Bass.MusicFree(_chan);

            // create decode channel
            _chan = Bass.CreateStream(_ofd.FileName, 0, 0, BassFlags.Decode);

            if (_chan == 0)
                _chan = Bass.MusicLoad(_ofd.FileName, 0, 0, BassFlags.MusicRamp | BassFlags.Prescan | BassFlags.Decode, 0);

            if (_chan == 0)
            {
                // not a WAV/MP3 or MOD
                Status = "Click Here to Open and Play a File...";
                MessageBox.Show("Selected file couldn't be loaded!");
                return;
            }

            // get channel info
            Bass.ChannelGetInfo(_chan, out _info);

            // create a new stream - decoded & resampled :)
            if ((_chan = BassFx.TempoCreate(_chan, BassFlags.Loop | BassFlags.FxFreeSource)) == 0)
            {
                Status = "Click Here to Open and Play a File...";
                MessageBox.Show("Couldn't create a resampled stream!");
                Bass.StreamFree(_chan);
                Bass.MusicFree(_chan);
                return;
            }

            // update the button to show the loaded file name (without path)
            Status = Path.GetFileName(_ofd.FileName);
            
            SampleRate = _info.Frequency;
            OnPropertyChanged(nameof(MinSampleRate));
            OnPropertyChanged(nameof(MaxSampleRate));
            
            // update tempo view
            Tempo = 0;

            // set the callback bpm and beat
            IsBpmPeriod = _isBpmPeriod;

            IsBeatPosition = _isBeatPosition;

            // play new created stream
            Bass.ChannelPlay(_chan);

            // create bpmChan stream and get bpm value for BpmPeriod seconds from current position
            var pos = Bass.ChannelBytes2Seconds(_chan, Bass.ChannelGetPosition(_chan));
            var maxpos = Bass.ChannelBytes2Seconds(_chan, Bass.ChannelGetLength(_chan));
            DecodingBPM(true, pos, pos + BpmPeriod >= maxpos ? maxpos - 1 : pos + BpmPeriod, _ofd.FileName);
        }

        public ICommand OpenCommand { get; }

        #region Callbacks
        void DecodingBPM(bool newStream, double startSec, double endSec, string fp)
        {
            if (newStream)
            {
                // open the same file as played but for bpm decoding detection
                _bpmchan = Bass.CreateStream(fp, 0, 0, BassFlags.Decode);

                if (_bpmchan == 0)
                    _bpmchan = Bass.MusicLoad(fp, 0, 0, BassFlags.Decode | BassFlags.Prescan, 0);
            }

            // detect bpm in background and return progress in GetBPM_ProgressCallback function
            if (_bpmchan != 0)
                Bpm = BassFx.BPMDecodeGet(_bpmchan, startSec, endSec, 0, BassFlags.FxBpmBackground | BassFlags.FXBpmMult2 | BassFlags.FxFreeSource, _progressProcedure);
        }

        void GetBPM_ProgressCallback(int Channel, float Percent, IntPtr User)
        {
            BpmProgress = (int)Percent;
        }

        void GetBPM_Callback(int Channel, float BPM, IntPtr User)
        {
            // update the bpm view
            Bpm = BPM;
        }

        async void GetBeatPos_Callback(int Channel, double beatPosition, IntPtr User)
        {
            var curpos = Bass.ChannelBytes2Seconds(Channel, Bass.ChannelGetPosition(Channel));

            await Task.Delay(TimeSpan.FromSeconds(beatPosition - curpos));

            BeatPosition = Bass.ChannelBytes2Seconds(Channel, Bass.ChannelGetPosition(Channel)) / BassFx.TempoGetRateRatio(Channel);
        }
        #endregion

        #region Properties
        string _status = "Click Here to Open and Play a File...";

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        double _tempo;

        public double Tempo
        {
            get { return _tempo; }
            set
            {
                _tempo = value;

                Bass.ChannelSetAttribute(_chan, ChannelAttribute.Tempo, value);

                OnPropertyChanged();
            }
        }

        double _sampleRate = 44100;

        public double SampleRate
        {
            get { return _sampleRate; }
            set
            {
                _sampleRate = value;

                var oldRateRatio = BassFx.TempoGetRateRatio(_chan);

                Bass.ChannelSetAttribute(_chan, ChannelAttribute.TempoFrequency, value);

                Bpm /= oldRateRatio;

                OnPropertyChanged();
            }
        }

        public double MinSampleRate => SampleRate * 0.7;

        public double MaxSampleRate => SampleRate * 1.3;

        double _bpm;

        public double Bpm
        {
            get { return _bpm; }
            set
            {
                if (value == 0)
                    return;

                _bpm = value * BassFx.TempoGetRateRatio(_chan);

                OnPropertyChanged();
            }
        }

        double _beatPosition;

        public double BeatPosition
        {
            get { return _beatPosition; }
            set
            {
                _beatPosition = value;
                OnPropertyChanged();
            }
        }

        int _bpmPeriod = 30;

        public int BpmPeriod
        {
            get { return _bpmPeriod; }
            set
            {
                _bpmPeriod = value;
                OnPropertyChanged();
            }
        }

        int _bpmProgress;

        public int BpmProgress
        {
            get { return _bpmProgress; }
            set
            {
                _bpmProgress = value;
                OnPropertyChanged();
            }
        }

        bool _isBpmPeriod;

        public bool IsBpmPeriod
        {
            get { return _isBpmPeriod; }
            set
            {
                _isBpmPeriod = value;

                if (value)
                    BassFx.BPMCallbackSet(_chan, _bpmProcedure, BpmPeriod, 0, BassFlags.FXBpmMult2);
                else BassFx.BPMFree(_chan);

                OnPropertyChanged();
            }
        }

        bool _isBeatPosition;

        public bool IsBeatPosition
        {
            get { return _isBeatPosition; }
            set
            {
                _isBeatPosition = value;

                if (value)
                    BassFx.BPMBeatCallbackSet(_chan, _beatProcedure);
                else BassFx.BPMBeatFree(_chan);

                OnPropertyChanged();
            }
        }
        #endregion
    }
}