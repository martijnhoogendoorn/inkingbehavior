using Dev.MartijnHoogendoorn.Inking.Behavior.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

// The Templated Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234235

namespace Dev.MartijnHoogendoorn.Inking.Behavior.Controls
{
    public sealed class InkToolbarControl : Control
    {
        public InkToolbarControl(InkingBehavior parent)
        {
            this.DefaultStyleKey = typeof(InkToolbarControl);
            this.DataContext = parent;
        }
    }
}
