using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DeviceEnumerator
{
    public abstract class ViewModelBase<T> : NotifyBase
    {
        protected ViewModelBase()
        {
            OnRefresh();
        } 

        public ObservableCollection<T> AvailableAudioSources { get; } = new ObservableCollection<T>();

        T _dev;
        public T SelectedAudioDevice
        {
            get { return _dev; }
            set
            {
                _dev = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshCommand => new DelegateCommand(OnRefresh);

        protected abstract void OnRefresh();
    }
}