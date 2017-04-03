namespace MidiSynth
{
    public partial class MainWindow
    {
        readonly MainViewModel _mainViewModel;

        public MainWindow()
        {
            InitializeComponent();

            _mainViewModel = DataContext as MainViewModel;

            KeyDown += (s, e) => _mainViewModel.OnKeyDown(e.Key, e.IsRepeat);

            KeyUp += (s, e) => _mainViewModel.OnKeyUp(e.Key);
        }
    }
}
