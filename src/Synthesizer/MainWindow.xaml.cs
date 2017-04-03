using System;
using System.Windows.Input;
using ManagedBass;

namespace Synthesizer
{
    public partial class MainWindow
    {   
        public MainWindow()
        {
            InitializeComponent();

            var viewModel = DataContext as MainViewModel;

            KeyUp += (s, e) => viewModel.OnKeyUp(e.Key);
            KeyDown += (s, e) => viewModel.OnKeyDown(e.Key);
        }
    }
}
