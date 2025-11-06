using System;
using System.Windows.Input;

namespace EngineToolBRE.Infrastructure
{
    public sealed class RelayCommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;

        public RelayCommand(Action _execute, Func<bool> _canExecute = null)
        {
            execute = _execute ?? throw new ArgumentNullException(nameof(_execute));
            canExecute = _canExecute;
        }

        public bool CanExecute(object _parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object _parameter) => execute();

        public event EventHandler CanExecuteChanged;
    }
}
