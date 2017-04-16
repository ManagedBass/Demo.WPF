using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;

namespace Revic
{
    public class MainViewModel : NotifyBase
    {
        readonly OpenFileDialog _ofd;
        double _autoCrossfadeTarget, _autoCrossfadeDelta;
        bool _autoCrossfadeRunning;

        async void StartAutoCrossfade()
        {
            if (_autoCrossfadeRunning)
                return;

            _autoCrossfadeRunning = true;

            while (Math.Abs(_autoCrossfadeTarget - Crossfade) >= Math.Abs(_autoCrossfadeDelta))
            {
                Crossfade += _autoCrossfadeDelta;
                await Task.Delay(30);
            }

            _autoCrossfadeRunning = false;
        }

        DeckViewModel _deckA, _deckB;
        DeckViewModel DeckA
        {
            get { return _deckA; }
            set
            {
                _deckA = value;

                _deckA.MusicLoaded += () =>
                {
                    try { _deckA.Volume = 1 - Crossfade; }
                    catch { }
                };
            }
        }

        DeckViewModel DeckB
        {
            get { return _deckB; }
            set
            {
                _deckB = value;

                _deckB.MusicLoaded += () =>
                {
                    try { _deckB.Volume = Crossfade; }
                    catch { }
                };
            }
        }

        public Deck DeckADeck { set { DeckA = value.DataContext as DeckViewModel; } }
        public Deck DeckBDeck { set { DeckB = value.DataContext as DeckViewModel; } }

        public ObservableCollection<PlaylistLabel> PlaylistItems { get; } = new ObservableCollection<PlaylistLabel>();
        
        public MainViewModel()
        {
            _ofd = new OpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = "Audio Files|*.mp3;*.wav;*.wma;*.aac;*.m4a",
                Title = "Select Audio File",
                ValidateNames = true,
                Multiselect = true
            };
            
            OpenCommand = new DelegateCommand(() =>
            {
                if (!_ofd.ShowDialog().Value)
                    return;

                foreach (var fileName in _ofd.FileNames)
                    PlaylistItems.Add(new PlaylistLabel(fileName));
            });

            SyncStartCommand = new DelegateCommand(() =>
            {
                try { DeckA.Play(); }
                catch { }

                try { DeckB.Play(); }
                catch { }
            });
            
            DeleteCommand = new DelegateCommand<IList>(list =>
            {
                var items = new PlaylistLabel[list.Count];

                list.CopyTo(items, 0);

                foreach (var item in items)
                    PlaylistItems.Remove(item);
            });
            
            AutoCrossfadeCommand = new DelegateCommand<string>(Param =>
            {
                _autoCrossfadeTarget = double.Parse(Param.ToString());

                _autoCrossfadeDelta = (_autoCrossfadeTarget - Crossfade) / 200;

                StartAutoCrossfade();
            });
        }

        public ICommand OpenCommand { get; }

        public ICommand SyncStartCommand { get; }

        public ICommand DeleteCommand { get; }

        public ICommand AutoCrossfadeCommand { get; }

        double _crossfade = 0.5;

        public double Crossfade
        {
            get { return _crossfade; }
            set
            {
                _crossfade = value;

                try { DeckB.Volume = value; }
                catch { }

                try { DeckA.Volume = 1 - value; }
                catch { }

                OnPropertyChanged();
            }
        }
    }
}