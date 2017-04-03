using System;
using System.Windows.Input;

namespace PluginTest
{
    public class DelegateCommand : ICommand
    {
        readonly Action _execute;
        
        public DelegateCommand(Action OnExecute)
        {
            _execute = OnExecute;
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _execute?.Invoke();

        public event EventHandler CanExecuteChanged;
    }
}