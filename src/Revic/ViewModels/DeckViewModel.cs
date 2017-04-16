using System;
using System.Timers;
using System.Windows.Input;
using ManagedBass;
using ManagedBass.Fx;

namespace Revic
{
    public class DeckViewModel : NotifyBase
    {
        #region Fields
        public MediaPlayerFX Player { get; } = new MediaPlayerFX();
        public ReverbEffect Reverb { get; } = new ReverbEffect();
        public DistortionEffect Distortion { get; } = new DistortionEffect();
        public EchoEffect Echo { get; } = new EchoEffect();
        public AutoWahEffect AutoWah { get; } = new AutoWahEffect();
        public RotateEffect Rotate { get; } = new RotateEffect();
        
        readonly Timer _progressBarTimer;
        #endregion
        
        public bool IsDragging { get; set; }

        public DeckViewModel()
        {
            _progressBarTimer = new Timer(100);
            _progressBarTimer.Elapsed += (s, e) =>
            {
                if (!IsDragging)
                    OnPropertyChanged(nameof(Position));
            };

            Player.MediaEnded += (s, e) =>
            {
                Player.Stop();
                Position = Player.Reverse ? Player.Duration.TotalSeconds : 0;
                _progressBarTimer.Stop();
            };
            
            Reverb.ApplyOn(Player);
            Distortion.ApplyOn(Player);
            Echo.ApplyOn(Player);
            AutoWah.ApplyOn(Player);
            Rotate.ApplyOn(Player);

            #region Commands
            PlayCommand = new DelegateCommand(Play);
            StopCommand = new DelegateCommand(Stop);

            SoftDistortionCommand = new DelegateCommand(Distortion.Soft);
            MediumDistortionCommand = new DelegateCommand(Distortion.Medium);
            HardDistortionCommand = new DelegateCommand(Distortion.Hard);
            VeryHardDistortionCommand = new DelegateCommand(Distortion.VeryHard);
            
            ManyEchoesCommand = new DelegateCommand(Echo.ManyEchoes);
            ReverseEchoesCommand = new DelegateCommand(Echo.ReverseEchoes);
            RoboticEchoesCommand = new DelegateCommand(Echo.RoboticVoice);
            SmallEchoesCommand = new DelegateCommand(Echo.Small);

            SlowAutoWahCommand = new DelegateCommand(AutoWah.Slow);
            FastAutoWahCommand = new DelegateCommand(AutoWah.Fast);
            HiFastAutoWahCommand = new DelegateCommand(AutoWah.HiFast);

            ResetPitchCommand = new DelegateCommand(() => Player.Pitch = 0);
            ResetFrequencyCommand = new DelegateCommand(() => Player.Frequency = 44100);
            ResetPanCommand = new DelegateCommand(() => Player.Balance = 0);
            ResetTempoCommand = new DelegateCommand(() => Player.Tempo = 0);
            #endregion
        }

        #region Commands
        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        
        public ICommand SoftDistortionCommand { get; }
        public ICommand MediumDistortionCommand { get; }
        public ICommand HardDistortionCommand { get; }
        public ICommand VeryHardDistortionCommand { get; }

        public ICommand ManyEchoesCommand { get; }
        public ICommand ReverseEchoesCommand { get; }
        public ICommand RoboticEchoesCommand { get; }
        public ICommand SmallEchoesCommand { get; }

        public ICommand SlowAutoWahCommand { get; }
        public ICommand FastAutoWahCommand { get; }
        public ICommand HiFastAutoWahCommand { get; }

        public ICommand ResetPitchCommand { get; }
        public ICommand ResetFrequencyCommand { get; }
        public ICommand ResetPanCommand { get; }
        public ICommand ResetTempoCommand { get; }
        #endregion

        public async void Load(string FilePath)
        {
            Stop();

            if (!await Player.LoadAsync(FilePath))
                return;
            
            Ready = true;
            
            MusicLoaded?.Invoke();
        }

        public event Action MusicLoaded;

        public void Play()
        {
            if (!Ready)
                return;

            if (Player.State == PlaybackState.Playing)
            {
                if (!Player.Pause())
                    return;
                
                _progressBarTimer.Stop();
            }
            else
            {
                if (Player.Reverse && Position == 0)
                    Position = Player.Duration.TotalSeconds;

                if (!Player.Play())
                    return;
                
                _progressBarTimer.Start();
            }
        }

        void Stop()
        {
            if (!Player.Stop())
                return;
            
            Position = Player.Reverse ? Player.Duration.TotalSeconds : 0;
            _progressBarTimer.Stop();
        }

        #region Properties
        bool _ready;

        public bool Ready
        {
            get { return _ready; }
            set
            {
                _ready = value;
                OnPropertyChanged();
            }
        }

        public double Position
        {
            get { return Player.Position.TotalSeconds; }
            set
            {
                Player.Position = TimeSpan.FromSeconds(value);

                OnPropertyChanged();
            }
        }

        public double Volume { set { Player.Volume = value; } }
        #endregion
    }
}