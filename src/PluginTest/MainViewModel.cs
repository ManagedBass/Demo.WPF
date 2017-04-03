using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ManagedBass;
using Microsoft.Win32;

namespace PluginTest
{
    public class MainViewModel : INotifyPropertyChanged
    {
        int _chan;
        readonly OpenFileDialog _ofd = new OpenFileDialog();

        public ObservableCollection<string> PluginCollection { get; } = new ObservableCollection<string>();

        ~MainViewModel()
        {
            Bass.Free();
            Bass.PluginFree(0);
        }

        public MainViewModel()
        {
            InitBass();

            OpenCommand = new DelegateCommand(OpenFile);
        }

        public ICommand OpenCommand { get; }

        void InitBass()
        {
            // initialize default output device
            if (!Bass.Init())
            {
                MessageBox.Show("Can't initialize device");

                Application.Current.Shutdown();
            }

            // initialize file selector
            _ofd.Filter = "BASS built-in (*.mp3;*.mp2;*.mp1;*.ogg;*.wav;*.aif)|*.mp3;*.mp2;*.mp1;*.ogg;*.wav;*.aif";

            // look for plugins (in the executable's directory)
            foreach (var plugin in Directory.EnumerateFiles(Environment.CurrentDirectory, "bass*.dll"))
            {
                var fileName = Path.GetFileNameWithoutExtension(plugin);

                int hPlugin;

                if ((hPlugin = Bass.PluginLoad(plugin)) == 0)
                    continue;

                // plugin loaded...
                var pinfo = Bass.PluginGetInfo(hPlugin);

                // get plugin info to add to the file selector filter...
                foreach (var format in pinfo.Formats)
                    _ofd.Filter += $"|{format.Name} ({format.FileExtensions}) - {fileName}|{format.FileExtensions}";

                // add plugin to the list
                PluginCollection.Add(fileName);
            }

            _ofd.Filter += "|All Files|*.*";

            if (PluginCollection.Count == 0) // no plugins...
                PluginCollection.Add("no plugins - visit the BASS webpage to get some");
        }

        void OpenFile()
        {
            if (!_ofd.ShowDialog().Value)
                return;

            Bass.StreamFree(_chan); // free the old stream

            if ((_chan = Bass.CreateStream(_ofd.FileName, 0, 0, BassFlags.Loop)) == 0)
            {
                // it ain't playable
                Status = "Click here to open a file...";

                MessageBox.Show("Can't play the file");

                return;
            }

            Status = _ofd.FileName; // display the file type and length

            Bass.ChannelPlay(_chan);
        }

        string _status = "Click here to open a file...";

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}