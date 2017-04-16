using ManagedBass;
using System;
using System.Collections.Generic;

namespace RecordingTest
{
    public class RecordingDevice : IDisposable
    {
        string _name;

        public int Index { get; }

        RecordingDevice(int Index, string Name)
        {
            this.Index = Index;

            _name = Name;
        }

        public static IEnumerable<RecordingDevice> Enumerate()
        {
            for (int i = 0; Bass.RecordGetDeviceInfo(i, out var info); ++i)
                yield return new RecordingDevice(i, info.Name);
        }

        public void Dispose()
        {
            Bass.CurrentRecordingDevice = Index;
            Bass.RecordFree();
        }

        public override string ToString() => _name;
    }
}
