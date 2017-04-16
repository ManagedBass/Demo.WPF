using ManagedBass;
using System;
using System.Runtime.InteropServices;

namespace RecordingTest
{
    public class AudioRecorder : IDisposable
    {
        int _device, _handle;

        public AudioRecorder(RecordingDevice Device)
        {
            _device = Device.Index;

            Bass.RecordInit(_device);

            _handle = Bass.RecordStart(44100, 2, BassFlags.RecordPause, Procedure);
        }

        byte[] _buffer;

        bool Procedure(int Handle, IntPtr Buffer, int Length, IntPtr User)
        {
            if (_buffer == null || _buffer.Length < Length)
                _buffer = new byte[Length];

            Marshal.Copy(Buffer, _buffer, 0, Length);

            DataAvailable?.Invoke(_buffer, Length);

            return true;
        }

        public event DataAvailableHandler DataAvailable;

        public void Start()
        {
            Bass.ChannelPlay(_handle);
        }

        public void Stop()
        {
            Bass.ChannelStop(_handle);
        }

        public void Dispose()
        {
            Bass.CurrentRecordingDevice = _device;

            Bass.RecordFree();
        }
    }
}
