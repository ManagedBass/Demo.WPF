using System;
using System.Windows;
using System.Windows.Input;
using ManagedBass;
using Microsoft.Win32;

namespace DSPTest
{
    public class MainViewModel : NotifyBase
    {
        readonly OpenFileDialog _ofd;
        int _chan;

        ~MainViewModel()
        {
            Bass.Free();
        }

        public MainViewModel()
        {
            _ofd = new OpenFileDialog
            {
                Filter = "Playable files|*.mo3;*.xm;*.mod;*.s3m;*.it;*.mtm;*.umx;*.mp3;*.mp2;*.mp1;*.ogg;*.wav;*.aif|All files|*.*"
            };

            Bass.FloatingPointDSP = true;

            if (!Bass.Init())
            {
                MessageBox.Show("Can't initialize device");
                Application.Current.Shutdown();
            }

            OpenCommand = new DelegateCommand(OpenFile);
        }

        string _status = "Click here to Open a File...";

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        #region Open
        public ICommand OpenCommand { get; }

        void OpenFile()
        {
            if (!_ofd.ShowDialog().Value)
                return;

            // free both MOD and stream, it must be one of them! :)
            Bass.MusicFree(_chan);
            Bass.StreamFree(_chan);

            if ((_chan = Bass.CreateStream(_ofd.FileName, 0, 0, BassFlags.Loop | BassFlags.Float)) == 0
                && (_chan = Bass.MusicLoad(_ofd.FileName, 0, 0, BassFlags.Loop | BassFlags.MusicSensitiveRamping | BassFlags.Float, 1)) == 0)
            {
                // whatever it is, it ain't playable
                Status = "Click here to Open a File...";
                MessageBox.Show("Can't play the file");
                return;
            }

            ChannelInfo info;
            Bass.ChannelGetInfo(_chan, out info);

            if (info.Channels != 2)
            {
                // only stereo is allowed
                Status = "Click here to Open a File...";

                Bass.MusicFree(_chan);
                Bass.StreamFree(_chan);

                MessageBox.Show("only stereo sources are supported");
                return;
            }

            Status = _ofd.FileName;

            // setup DSPs on new channel and play it
            if (IsRotate)
                IsRotate = true;

            if (IsEcho)
                IsEcho = true;

            if (IsFlanger)
                IsFlanger = true;

            Bass.ChannelPlay(_chan);
        }
        #endregion

        #region Rotate
        public bool IsRotate
        {
            get { return _rotateDsp != 0; }
            set
            {
                if (value)
                {
                    _rotPos = (float)Math.PI / 4;
                    _rotateDsp = Bass.ChannelSetDSP(_chan, Rotate, Priority: 2);
                }
                else
                {
                    if (Bass.ChannelRemoveDSP(_chan, _rotateDsp))
                        _rotateDsp = 0;
                }

                OnPropertyChanged();
            }
        }

        int _rotateDsp;
        float _rotPos;

        unsafe void Rotate(int Handle, int Channel, IntPtr Buffer, int Length, IntPtr User)
        {
            var d = (float*)Buffer;

            for (var a = 0; a < Length / 4; a += 2)
            {
                d[a] *= (float)Math.Abs(Math.Sin(_rotPos));
                d[a + 1] *= (float)Math.Abs(Math.Cos(_rotPos));
                _rotPos += 0.00003f;
            }

            _rotPos %= 2 * (float)Math.PI;
        }
        #endregion

        #region Echo
        public bool IsEcho
        {
            get { return _echdsp != 0; }
            set
            {
                if (value)
                {
                    _echpos = 0;
                    _echdsp = Bass.ChannelSetDSP(_chan, Echo, Priority: 1);
                }
                else
                {
                    if (Bass.ChannelRemoveDSP(_chan, _echdsp))
                        _echdsp = 0;
                }

                OnPropertyChanged();
            }
        }

        int _echdsp;    // DSP handle
        const int Echbuflen = 1200;	// buffer length
        readonly float[,] _echbuf = new float[Echbuflen, 2];	// buffer
        int _echpos; // cur.pos

        unsafe void Echo(int Handle, int Channel, IntPtr Buffer, int Length, IntPtr User)
        {
            var d = (float*)Buffer;
            int a;

            for (a = 0; a < Length / 4; a += 2)
            {
                var l = d[a] + _echbuf[_echpos, 1] / 2;
                var r = d[a + 1] + _echbuf[_echpos, 0] / 2;

                _echbuf[_echpos, 0] = d[a];
                _echbuf[_echpos, 1] = d[a + 1];

                d[a] = l;
                d[a + 1] = r;

                _echpos++;

                if (_echpos == Echbuflen)
                    _echpos = 0;
            }
        }
        #endregion

        #region Flanger
        public bool IsFlanger
        {
            get { return _fladsp != 0; }
            set
            {
                if (value)
                {
                    _flapos = 0;
                    _flas = Flabuflen / 2.0f;
                    _flasinc = 0.002f;
                    _fladsp = Bass.ChannelSetDSP(_chan, Flange);
                }
                else
                {
                    if (Bass.ChannelRemoveDSP(_chan, _fladsp))
                        _fladsp = 0;
                }

                OnPropertyChanged();
            }
        }

        int _fladsp;    // DSP handle
        const int Flabuflen = 350;	// buffer length
        readonly float[,] _flabuf = new float[Flabuflen, 2];	// buffer
        int _flapos; // cur.pos
        float _flas, _flasinc;    // sweep pos/increment

        unsafe void Flange(int Handle, int Channel, IntPtr Buffer, int Length, IntPtr User)
        {
            var d = (float*)Buffer;
            int a;

            for (a = 0; a < Length / 4; a += 2)
            {
                var p1 = (_flapos + (int)_flas) % Flabuflen;
                var p2 = (p1 + 1) % Flabuflen;
                var f = _flas - (int)_flas;

                var s = (float)((d[a] + (_flabuf[p1, 0] * (1 - f) + _flabuf[p2, 0] * f)) * 0.7);
                _flabuf[_flapos, 0] = d[a];
                d[a] = s;

                s = (float)((d[a + 1] + (_flabuf[p1, 1] * (1 - f) + _flabuf[p2, 1] * f)) * 0.7);
                _flabuf[_flapos, 1] = d[a + 1];
                d[a + 1] = s;

                _flapos++;

                if (_flapos == Flabuflen)
                    _flapos = 0;

                _flas += _flasinc;

                if (_flas < 0 || _flas > Flabuflen - 1)
                {
                    _flasinc = -_flasinc;
                    _flas += _flasinc;
                }
            }
        }
        #endregion
    }
}