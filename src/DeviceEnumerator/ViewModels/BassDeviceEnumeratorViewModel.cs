using ManagedBass;

namespace DeviceEnumerator
{
    public class BassDeviceEnumeratorViewModel : ViewModelBase<DeviceInfo>
    {
        protected override void OnRefresh()
        {
            AvailableAudioSources.Clear();

            DeviceInfo devInfo;

            for (var i = 0; Bass.GetDeviceInfo(i, out devInfo); ++i)
                AvailableAudioSources.Add(devInfo);

            for (var i = 0; Bass.RecordGetDeviceInfo(i, out devInfo); ++i)
                AvailableAudioSources.Add(devInfo);

            SelectedAudioDevice = AvailableAudioSources[0];
        }
    }
}