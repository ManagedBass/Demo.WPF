using ManagedBass.Wasapi;

namespace DeviceEnumerator
{
    public class WasapiDeviceEnumeratorViewModel : ViewModelBase<WasapiDeviceInfo>
    {
        protected override void OnRefresh()
        {
            AvailableAudioSources.Clear();

            WasapiDeviceInfo devInfo;

            for (var i = 0; BassWasapi.GetDeviceInfo(i, out devInfo); ++i)
                AvailableAudioSources.Add(devInfo);

            SelectedAudioDevice = AvailableAudioSources[0];
        }
    }
}