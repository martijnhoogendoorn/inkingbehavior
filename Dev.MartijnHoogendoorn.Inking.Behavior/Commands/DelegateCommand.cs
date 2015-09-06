using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Reflection;

namespace Dev.MartijnHoogendoorn.Inking.Behavior.Commands
{
    public class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public event EventHandler CanExecuteChanged;

        public DelegateCommand(Action execute) : this(execute, () => true) { }

        public DelegateCommand(Action execute, Func<bool> canexecute)
        {
            if (execute == null)
            {
                throw new ArgumentNullException("execute");
            }

            _execute = execute;
            _canExecute = canexecute;
        }

        public bool CanExecute(object p)
        {
            return _canExecute == null ? true : _canExecute();
        }

        public void Execute(object p)
        {
            if (CanExecute(null))
            {
                _execute();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
            {
                CanExecuteChanged(this, EventArgs.Empty);
            }
        }
    }

    public class DelegateCommand<T> : ICommand
    {
        private Action<T> _execute;
        private Func<bool> _canExecute;

        public DelegateCommand(Action<T> execute, Func<bool> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public DelegateCommand(Action<T> execute) : this(execute, null) { }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute();
        }

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
            {
                CanExecuteChanged(this, EventArgs.Empty);
            }
        }

        public void Execute(object parameter)
        {
            if (_execute != null)
            {
                if(parameter != null)
                {
                    if(typeof(T).GetInterfaces().FirstOrDefault(p => p.FullName == typeof(IList).FullName) != null && parameter.GetType() != typeof(IList))
                    {
                        var arg = Activator.CreateInstance<T>();
                        ((IList)arg).Add(parameter);
                        _execute(arg);
                    }
                    else
                    {
                        _execute((T)parameter);
                    }
                }
            }
        }
    }
}
