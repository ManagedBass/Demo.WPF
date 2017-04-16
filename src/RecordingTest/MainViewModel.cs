using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ManagedBass;

namespace RecordingTest
{
    public class MainViewModel : NotifyBase
    {
        static readonly string OutFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MBass\\");

        static MainViewModel()
        {
            Bass.Init();

            if (!Directory.Exists(OutFolder))
                Directory.CreateDirectory(OutFolder);
        }

        public ObservableCollection<RecordingDevice> AvailableAudioSources { get; } = new ObservableCollection<RecordingDevice>();
        
        public MainViewModel()
        {
            RefreshCommand = new DelegateCommand(Refresh);
            PlayPauseCommand = new DelegateCommand(PlayPause);
            StopCommand = new DelegateCommand(Stop, () => State != PlaybackState.Stopped && SelectedAudioDevice != null);

            Refresh();
        }

        public ICommand OpenOutputFolderCommand { get; } = new DelegateCommand(() => Process.Start("explorer.exe", OutFolder));

        public ICommand RefreshCommand { get; }

        void Refresh()
        {
            AvailableAudioSources.Clear();
            
            foreach (var dev in RecordingDevice.Enumerate())
                AvailableAudioSources.Add(dev);

            if (AvailableAudioSources.Count > 0)
                SelectedAudioDevice = AvailableAudioSources[0];
        }

        public ICommand PlayPauseCommand { get; }

        void PlayPause()
        {
            switch (State)
            {
                case PlaybackState.Playing:
                    _r.Stop();
                    State = PlaybackState.Paused;
                    break;

                case PlaybackState.Paused:
                    _r.Start();
                    State = PlaybackState.Playing;
                    break;

                default:
                    New();
                    State = PlaybackState.Playing;
                    break;
            }
        }

        void New()
        {
            var filePath = Path.Combine(OutFolder, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".wav");

            _r = new AudioRecorder(_dev);

            try
            {
                _writer = new WaveFileWriter(new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read), new WaveFormat());
            }
            catch
            {
                MessageBox.Show(Bass.LastError.ToString());
            }

            _r.DataAvailable += (Buffer, Length) => _writer?.Write(Buffer, Length);

            _r.Start();
        }

        public ICommand StopCommand { get; }

        void Stop()
        {
            State = PlaybackState.Stopped;

            _r?.Dispose();
            
            _writer?.Dispose();
        }

        AudioRecorder _r;
        WaveFileWriter _writer;

        RecordingDevice _dev;
        public RecordingDevice SelectedAudioDevice
        {
            get { return _dev; }
            set
            {
                if (_dev == value)
                    return;

                _dev = value;
                OnPropertyChanged();
            }
        }

        PlaybackState _state;

        public PlaybackState State
        {
            get { return _state; }
            set
            {
                if (_state == value)
                    return;

                _state = value;
                
                OnPropertyChanged();
            }
        }
    }
}