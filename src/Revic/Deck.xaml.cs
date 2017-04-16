using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Revic
{
    public partial class Deck
    {
        readonly DeckViewModel _viewModel;

        public Deck()
        {
            InitializeComponent();

            _viewModel = DataContext as DeckViewModel;
        }
        
        #region Progress Slider
        void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_viewModel.IsDragging)
                return;

            _viewModel.Position = ((Slider)sender).Value;

            _viewModel.IsDragging = false;
        }

        void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _viewModel.IsDragging = true;

        void Slider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _viewModel.Position = ((Slider)sender).Value;
        #endregion

        void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
                _viewModel.Load((string)e.Data.GetData(DataFormats.StringFormat));
        }
    }
}