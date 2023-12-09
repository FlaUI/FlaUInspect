using FlaUI.Core;
using FlaUInspect.Core;
using System;
using System.Threading.Tasks;

namespace FlaUInspect.ViewModels
{
    public interface IDetailViewModel
    {
        string Key { get; set; }
        string Value { get; set; }
        bool Important { get; set; }
        bool HasExecutableAction { get; set; }
        Action ActionToExecute { get; set; }
    }

    public class DetailViewModel : ObservableObject, IDetailViewModel
    {
        public DetailViewModel(string key, string value, Action actionToExecute = null)
        {
            Key = key;
            Value = value;
            if (actionToExecute != null)
            {
                HasExecutableAction = true;
            }
            else
            {
                HasExecutableAction = false;
            }
            ActionToExecute = actionToExecute;
        }

        public string Key { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string Value { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public bool Important { get { return GetProperty<bool>(); } set { SetProperty(value); } }
        public bool HasExecutableAction { get { return GetProperty<bool>(); } set { SetProperty(value); } }
        public Action ActionToExecute { get; set; }

        public static DetailViewModel FromAutomationProperty<T>(string key, IAutomationProperty<T> value)
        {
            return new DetailViewModel(key, value.ToDisplayText(), null);
        }
    }
}