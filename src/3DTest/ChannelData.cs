using ManagedBass;

namespace Test3D
{
    public class ChannelData
    {
        public ChannelData(string FileName, int Handle)
        {
            _fileName = FileName;
            this.Handle = Handle;
        }

        readonly string _fileName;

        public int Handle;

        public Vector3D Position = new Vector3D();

        public readonly Vector3D Velocity = new Vector3D();

        public override string ToString() => _fileName;
    }
}