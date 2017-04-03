using System.Collections.ObjectModel;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using ManagedBass;
using Microsoft.Win32;

namespace Test3D
{
    public class MainViewModel : NotifyBase
    {
        #region Fields
        readonly Timer _timer;
        readonly OpenFileDialog _ofd;
        public ObservableCollection<ChannelData> Channels { get; } = new ObservableCollection<ChannelData>();
        const int TimerPeriod = 50,
            MaxDist = 100;
        #endregion

        ~MainViewModel()
        {
            _timer.Stop();

            Bass.Free();
        }

        public MainViewModel()
        {
            Bass.Init(Flags: DeviceInitFlags.Device3D);

            Bass.Set3DFactors(1, 1, 1);

            _ofd = new OpenFileDialog
            {
                Filter = "wav/aif/mo3/xm/mod/s3m/it/mtm/umx|*.wav;*.aif;*.mo3;*.xm;*.mod;*.s3m;*.it;*.mtm;*.umx|All files|*.*"
            };

            #region Commands
            PlayCommand = new DelegateCommand(() => Bass.ChannelPlay(CurrentChannel.Handle),
                () => CurrentChannel != null);

            PauseCommand = new DelegateCommand(() => Bass.ChannelPause(CurrentChannel.Handle),
                () => CurrentChannel != null);

            ResetCommand = new DelegateCommand(() =>
            {
                CurrentChannel.Position = new Vector3D();
                XVelocity = ZVelocity = 0;
            }, () => CurrentChannel != null);

            OpenCommand = new DelegateCommand(OnOpenExecute);

            RemoveCommand = new DelegateCommand(() =>
            {
                Bass.SampleFree(CurrentChannel.Handle);
                Bass.MusicFree(CurrentChannel.Handle);

                Channels.Remove(CurrentChannel);
            }, () => CurrentChannel != null);
            #endregion

            _timer = new Timer(TimerPeriod);
            _timer.Elapsed += (Sender, Args) => UpdateDisplay();
            
            _timer.Start();
        }

        void OnOpenExecute()
        {
            if (!_ofd.ShowDialog().Value)
                return;

            int newChannel;

            if ((newChannel = Bass.MusicLoad(_ofd.FileName, Flags: BassFlags.MusicRamp | BassFlags.Loop | BassFlags.Bass3D, Frequency: 1)) == 0
                && (newChannel = Bass.SampleLoad(_ofd.FileName, 0, 0, 1, BassFlags.Loop | BassFlags.Bass3D | BassFlags.Mono)) == 0)
                return;

            Channels.Add(new ChannelData(_ofd.FileName, newChannel));
            Bass.SampleGetChannel(newChannel); // initialize sample channel
        }

        void UpdateDisplay()
        {
            if (Channels.Count == 0)
            {
                BallPosition = new Point(100, 100);
                return;
            }

            foreach (var channel in Channels)
            {
                // If the channel's playing then update it's position
                if (Bass.ChannelIsActive(channel.Handle) == PlaybackState.Playing)
                {
                    // Check if channel has reached the max distance
                    if (channel.Position.Z >= MaxDist || channel.Position.Z <= -MaxDist)
                        channel.Velocity.Z = -channel.Velocity.Z;

                    if (channel.Position.X >= MaxDist || channel.Position.X <= -MaxDist)
                        channel.Velocity.X = -channel.Velocity.X;

                    // Update channel position
                    channel.Position.Z += channel.Velocity.Z * TimerPeriod / 1000;
                    channel.Position.X += channel.Velocity.X * TimerPeriod / 1000;
                    Bass.ChannelSet3DPosition(channel.Handle, channel.Position, null, channel.Velocity);
                }

                // Draw the channel position indicator
                BallPosition = new Point(100 + (int)((100 - 10) * channel.Position.X / MaxDist),
                    100 - (int)((100 - 10) * channel.Position.Z / MaxDist));
            }

            Bass.Apply3D();
        }

        #region Commands
        public ICommand PlayCommand { get; }

        public ICommand PauseCommand { get; }

        public ICommand ResetCommand { get; }

        public ICommand OpenCommand { get; }

        public ICommand RemoveCommand { get; }
        #endregion

        #region Properties
        ChannelData _currentChannel;

        public ChannelData CurrentChannel
        {
            get { return _currentChannel; }
            set
            {
                _currentChannel = value;
                OnPropertyChanged();
            }
        }

        Point _ballPos = new Point(100, 100);

        public Point BallPosition 
        {
            get { return _ballPos; }
            set
            {
                _ballPos = value;
                OnPropertyChanged();
            }
        }

        public int XVelocity
        {
            get { return (int)(CurrentChannel?.Velocity.X ?? 0); }
            set
            {
                if (CurrentChannel == null)
                    return;

                CurrentChannel.Velocity.X = value;
                OnPropertyChanged();
            }
        }

        public int ZVelocity
        {
            get { return (int)(CurrentChannel?.Velocity.Z ?? 0); }
            set
            {
                if (CurrentChannel == null)
                    return;

                CurrentChannel.Velocity.Z = value;
                OnPropertyChanged();
            }
        }

        public double RollOff
        {
            get
            {
                float dist = 0, rolloff = 0, doppler = 0;
                Bass.Get3DFactors(ref dist, ref rolloff, ref doppler);
                return rolloff;
            }
            set
            {
                Bass.Set3DFactors(-1, (float)value, -1);
                OnPropertyChanged();
            }
        }

        public double Doppler
        {
            get
            {
                float dist = 0, rolloff = 0, doppler = 0;
                Bass.Get3DFactors(ref dist, ref rolloff, ref doppler);
                return doppler;
            }
            set
            {
                Bass.Set3DFactors(-1, -1, (float)value);
                OnPropertyChanged();
            }
        }
        #endregion
    }
}