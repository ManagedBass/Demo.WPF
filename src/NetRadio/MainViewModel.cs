using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using ManagedBass;

namespace NetRadio
{
    public class MainViewModel : NotifyBase
    {
        #region Fields
        static readonly object Lock = new object();
        readonly Timer _timer;
        int _req; // request number/counter
        int _chan; // stream handle

        public string[] Presets { get; } =
        {
            "http://www.radioparadise.com/m3u/mp3-128.m3u",
            "http://www.radioparadise.com/m3u/mp3-32.m3u",
            "http://icecast.timlradio.co.uk/vr160.ogg",
            "http://icecast.timlradio.co.uk/vr32.ogg",
            "http://icecast.timlradio.co.uk/a8160.ogg",
            "http://icecast.timlradio.co.uk/a832.ogg",
            "http://somafm.com/secretagent.pls",
            "http://somafm.com/secretagent24.pls",
            "http://somafm.com/suburbsofgoa.pls",
            "http://somafm.com/suburbsofgoa24.pls"
        };
        #endregion

        ~MainViewModel()
        {
            Bass.Free();
        }

        public MainViewModel()
        {
            if (!Bass.Init())
            {
                MessageBox.Show("Can't initialize device");
                Application.Current.Shutdown();
            }

            // enable playlist processing
            Bass.NetPlaylist = 1;

            // minimize automatic pre-buffering, so we can do it (and display it) instead
            Bass.NetPreBuffer = 0;

            OpenCommand = new DelegateCommand<string>(OpenUrl);

            _timer = new Timer(50);
            _timer.Elapsed += _timer_Tick;
        }

        public ICommand OpenCommand { get; }

        void OpenUrl(string Url)
        {
            TitleAndArtist = IcyMeta = null;

            Bass.NetProxy = DirectConnection ? null : Proxy;

            Task.Factory.StartNew(() =>
            {
                int r;

                lock (Lock) // make sure only 1 thread at a time can do the following
                    r = ++_req; // increment the request counter for this request

                _timer.Stop(); // stop prebuffer monitoring

                Bass.StreamFree(_chan); // close old stream

                Status = "Connecting...";

                var c = Bass.CreateStream(Url, 0,
                    BassFlags.StreamDownloadBlocks | BassFlags.StreamStatus | BassFlags.AutoFree, StatusProc,
                    new IntPtr(r));

                lock (Lock)
                {
                    if (r != _req)
                    {
                        // there is a newer request, discard this stream
                        if (c != 0)
                            Bass.StreamFree(c);

                        return;
                    }

                    _chan = c; // this is now the current stream
                }

                if (_chan == 0)
                {
                    // failed to open
                    Status = "Can't play the stream";
                }
                else _timer.Start(); // start prebuffer monitoring
            });
        }

        void _timer_Tick(object sender, EventArgs e)
        {
            // percentage of buffer filled
            var progress = Bass.StreamGetFilePosition(_chan, FileStreamPosition.Buffer)
                * 100 / Bass.StreamGetFilePosition(_chan, FileStreamPosition.End);

            if (progress > 75 || Bass.StreamGetFilePosition(_chan, FileStreamPosition.Connected) == 0)
            {
                // over 75% full (or end of download)
                _timer.Stop(); // finished prebuffering, stop monitoring

                Status = "Playing";

                // get the broadcast name and URL
                var icy = Bass.ChannelGetTags(_chan, TagType.ICY);

                if (icy == IntPtr.Zero)
                    icy = Bass.ChannelGetTags(_chan, TagType.HTTP); // no ICY tags, try HTTP

                if (icy != IntPtr.Zero)
                {
                    foreach (var tag in Extensions.ExtractMultiStringAnsi(icy))
                    {
                        var icymeta = string.Empty;

                        if (tag.StartsWith("icy-name:"))
                            icymeta += $"ICY Name: {tag.Substring(9)}";

                        if (tag.StartsWith("icy-url:"))
                            icymeta += $"ICY Url: {tag.Substring(8)}";

                        IcyMeta = icymeta;
                    }
                }

                // get the stream title and set sync for subsequent titles
                DoMeta();

                Bass.ChannelSetSync(_chan, SyncFlags.MetadataReceived, 0, MetaSync); // Shoutcast
                Bass.ChannelSetSync(_chan, SyncFlags.OggChange, 0, MetaSync); // Icecast/OGG

                // set sync for end of stream
                Bass.ChannelSetSync(_chan, SyncFlags.End, 0, EndSync);

                // play it!
                Bass.ChannelPlay(_chan);
            }

            else Status = $"Buffering... {progress}%";
        }

        void StatusProc(IntPtr buffer, int length, IntPtr user)
        {
            if (buffer != IntPtr.Zero
                && length == 0
                && user.ToInt32() == _req) // got HTTP/ICY tags, and this is still the current request

                Status = Marshal.PtrToStringAnsi(buffer); // display status
        }

        void EndSync(int Handle, int Channel, int Data, IntPtr User) => Status = "Not Playing";

        void MetaSync(int Handle, int Channel, int Data, IntPtr User) => DoMeta();

        void DoMeta()
        {
            var meta = Bass.ChannelGetTags(_chan, TagType.META);

            if (meta != IntPtr.Zero)
            {
                // got Shoutcast metadata
                var data = Marshal.PtrToStringAnsi(meta);

                var i = data.IndexOf("StreamTitle='"); // locate the title

                if (i == -1)
                    return;

                var j = data.IndexOf("';", i); // locate the end of it

                if (j != -1)
                    TitleAndArtist = $"Title: {data.Substring(i, j - i + 1)}";
            }
            else
            {
                meta = Bass.ChannelGetTags(_chan, TagType.OGG);

                if (meta == IntPtr.Zero)
                    return;

                // got Icecast/OGG tags
                foreach (var tag in Extensions.ExtractMultiStringUtf8(meta))
                {
                    string artist = null, title = null;

                    if (tag.StartsWith("artist="))
                        artist = $"Artist: {tag.Substring(7)}";

                    if (tag.StartsWith("title="))
                        title = $"Title: {tag.Substring(6)}";

                    if (title != null)
                        TitleAndArtist = artist != null ? $"{title} - {artist}" : title;
                }
            }
        }

        #region Properties
        bool _directConnect;

        public bool DirectConnection
        {
            get { return _directConnect; }
            set
            {
                _directConnect = value;
                OnPropertyChanged();
            }
        }

        string _status = "Ready";

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        string _titleAndArtist;

        public string TitleAndArtist
        {
            get { return _titleAndArtist; }
            set
            {
                _titleAndArtist = value;
                OnPropertyChanged();
            }
        }
        
        string _icyMeta;

        public string IcyMeta
        {
            get { return _icyMeta; }
            set
            {
                _icyMeta = value;
                OnPropertyChanged();
            }
        }

        string _proxy;

        public string Proxy
        {
            get { return _proxy; }
            set
            {
                _proxy = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}