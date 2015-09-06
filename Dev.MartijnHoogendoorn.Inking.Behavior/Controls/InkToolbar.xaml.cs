using Dev.MartijnHoogendoorn.Inking.Behavior.Behaviors;
using Dev.MartijnHoogendoorn.Inking.Behavior.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Dev.MartijnHoogendoorn.Inking.Behavior.Controls
{
    public sealed partial class InkToolbar : UserControl
    {
        public InkToolbar(InkingBehavior parent)
        {
            this.InitializeComponent();

            (this.Content as FrameworkElement).DataContext = parent;
        }  
    }
}