using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using ManagedBass;

namespace PitchTracking
{
    public class MainViewModel : NotifyBase
    {
        #region Fields
        readonly int _stream;
        readonly PitchTracker _pitchTracker = new PitchTracker();
        float[] _buffer;
        #endregion

        public MainViewModel()
        {
            Bass.RecordInit();

            _stream = Bass.RecordStart(44100, 2, BassFlags.RecordPause | BassFlags.Float, Procedure);

            _pitchTracker.PitchDetected += OnPitchDetected;

            DetectCommand = new DelegateCommand(() =>
            {
                if (!IsRecording)
                {
                    Bass.ChannelPlay(_stream);
                    IsRecording = true;
                }
                else
                {
                    Bass.ChannelPause(_stream);
                    IsRecording = false;
                }
            });
        }

        void OnPitchDetected(PitchRecord Record)
        {
            if (!(Record?.Pitch > 1))
                return;

            Frequency = Record.Pitch.ToString();
            Note = Record.NoteName;
            Cents = Record.MidiCents.ToString();
        }
        
        bool Procedure(int Handle, IntPtr Buffer, int Length, IntPtr User)
        {
            if (_buffer == null || _buffer.Length < Length / 4)
                _buffer = new float[Length / 4];

            Marshal.Copy(Buffer, _buffer, 0, Length / 4);

            _pitchTracker.ProcessBuffer(_buffer);

            return true;
        }

        public ICommand DetectCommand { get; }

        #region Properties
        string _note = "--";

        public string Note
        {
            get { return _note; }
            set
            {
                _note = value;
                OnPropertyChanged();
            }
        }

        string _freq = "--";

        public string Frequency
        {
            get { return _freq; }
            set
            {
                _freq = value;
                OnPropertyChanged();
            }
        }

        string _cents = "--";

        public string Cents
        {
            get { return _cents; }
            set
            {
                _cents = value;
                OnPropertyChanged();
            }
        }

        bool _isRecording;

        public bool IsRecording
        {
            get { return _isRecording; }
            set
            {
                _isRecording = value;
                OnPropertyChanged();
            }
        }
        #endregion
        
        ~MainViewModel()
        {
            Bass.RecordFree();
        }
    }
}