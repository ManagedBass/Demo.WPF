using ManagedBass;
using ManagedBass.Cd;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace CDTest
{
    public class MainViewModel : NotifyBase
    {
        public ObservableCollection<CDInfo> AvailableDrives { get; } = new ObservableCollection<CDInfo>();

        public ObservableCollection<string> CDAFiles { get; } = new ObservableCollection<string>();

        CDInfo _dev;
        public CDInfo SelectedDrive
        {
            get { return _dev; }
            set
            {
                _dev = value;
                OnPropertyChanged();

                CDAFiles.Clear();
                
                CDInfo devInfo;

                for (_currentDriveIndex = 0; BassCd.GetInfo(_currentDriveIndex, out devInfo); ++_currentDriveIndex)
                    if (devInfo.DriveLetter == SelectedDrive.DriveLetter)
                        break;
                
                if (!BassCd.IsReady(_currentDriveIndex))
                    return;

                foreach (var file in Directory.EnumerateFiles(SelectedDrive.DriveLetter + ":\\", "*.cda"))
                    CDAFiles.Add(file);
            }
        }

        int _currentDriveIndex;

        string _cda;
        public string SelectedCDA
        {
            get { return _cda; }
            set
            {
                _cda = value;
                OnPropertyChanged();
            }
        }

        public MainViewModel()
        {
            Refresh();

            RefreshCommand = new DelegateCommand(Refresh);

            PlayCommand = new DelegateCommand(Play, () => !string.IsNullOrWhiteSpace(SelectedCDA));
        }

        public ICommand RefreshCommand { get; }

        public ICommand PlayCommand { get; }

        void Refresh()
        {
            AvailableDrives.Clear();

            CDInfo devInfo;

            for (var i = 0; BassCd.GetInfo(i, out devInfo); ++i)
                AvailableDrives.Add(devInfo);
        }
                
        int _cdc;

        bool _isPlaying;

        public void Play()
        {
            if (!_isPlaying)
            {
                if (!BassCd.IsReady(_currentDriveIndex))
                    return;

                _cdc = BassCd.CreateStream(SelectedCDA, 0);
                Bass.ChannelPlay(_cdc);

                _isPlaying = true;
            }
            else
            {
                Bass.StreamFree(_cdc);

                _isPlaying = false;
            }
        }
    }
}