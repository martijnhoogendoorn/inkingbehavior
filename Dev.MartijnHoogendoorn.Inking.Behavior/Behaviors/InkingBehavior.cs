using Dev.MartijnHoogendoorn.Inking.Behavior.Controls;
using Dev.MartijnHoogendoorn.Inking.Behavior.Converters;
using Dev.MartijnHoogendoorn.Inking.Behavior.Model;
using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Dev.MartijnHoogendoorn.Inking.Behavior.Extensions;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Input;
using Windows.UI.Input;
using Windows.UI.Text;
using Dev.MartijnHoogendoorn.Inking.Behavior.Commands;
using Windows.Storage;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Dev.MartijnHoogendoorn.Inking.Behavior.Behaviors
{
    public class InkingBehavior : DependencyObject, IBehavior, INotifyPropertyChanged, IDisposable
    {
        private InkCanvas _inkCanvas;
        private InkToolbar _inkToolbar;
        private Canvas _annotationCanvas;
        private bool _initialized = false;
        private DataTransferManager _dataTransferManager;

        private ListIndexerConverter listIndexerConverter = new ListIndexerConverter();
        private BooleanToVisibilityConverter booleanToVisibilityConverter = new BooleanToVisibilityConverter();

        private StorageFile _tempExportFile;

        #region Command declarations
        public DelegateCommand<List<FrameworkElement>> SaveCommand { get; private set; }
        public DelegateCommand<List<FrameworkElement>> ShareCommand { get; private set; }
        // Ideally, this delegatecommand would have a InkingToolMode argument, but we cannot bind to those as we removed x:static markup from xaml.
        public DelegateCommand<string> SwitchInkingToolCommand { get; private set; }
        #endregion

        public InkingBehavior()
        {
            SetupEvents();        
            SetupCommands();
        }

        private void SetupEvents()
        {
            _dataTransferManager = DataTransferManager.GetForCurrentView();
            _dataTransferManager.DataRequested += new TypedEventHandler<DataTransferManager, DataRequestedEventArgs>(this.OnDataRequested);
        }

        private void SetupCommands()
        {
            SaveCommand = new DelegateCommand<List<FrameworkElement>>(async (excludedElements) =>
            {
                await ((Panel)AssociatedObject).SaveScreenshotAsync(
                    _inkCanvas,
                    new List<FrameworkElement>(excludedElements.Union(new[] { _inkToolbar })));
            });

            ShareCommand = new DelegateCommand<List<FrameworkElement>>(async (excludedElements) =>
            {
                var allExcludedElements = new List<FrameworkElement>(new[] { _inkToolbar });
                foreach (var item in AnnotationTogglers)
                {
                    allExcludedElements.Add(item);
                    var parentGrid = item.FindAncestor<Grid>();
                    var siblingEditBox = parentGrid.FindChild<RichEditBox>();

                    // The below looks nice, but removes our ability to restore once capturing is complete.
                    //siblingEditBox.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
                    //parentGrid.Height= siblingEditBox.DesiredSize.Height;
                    //parentGrid.Width = siblingEditBox.DesiredSize.Width;

                    var commandBar = siblingEditBox.FindChild<CommandBar>();
                    allExcludedElements.Add(commandBar);

                    var deleteIcon = parentGrid.FindName("deleteIcon") as FrameworkElement;
                    allExcludedElements.Add(deleteIcon);
                }

                if (excludedElements != null && excludedElements.Count > 0)
                {
                    allExcludedElements = excludedElements.Union(allExcludedElements).ToList();
                }

                _tempExportFile = await ((Panel)AssociatedObject).SaveScreenshotTemporaryAsync(
                    _inkCanvas,
                    allExcludedElements);

                DataTransferManager.ShowShareUI();
            });

            SwitchInkingToolCommand = new DelegateCommand<string>(m =>
            {
                InkingToolMode mode = InkingToolMode.None;
                if (Enum.TryParse<InkingToolMode>(m, out mode))
                {
                    ChangeDrawTool(mode);
                }
            });
        }

        private async void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs e)
        {
            DataPackage requestData = e.Request.Data;
            requestData.Properties.Title = SharedInkTitle;
            requestData.Properties.Description = SharedInkDescription;
            if (!string.IsNullOrWhiteSpace(SharedInkDeeplink))
            {
                requestData.Properties.ContentSourceApplicationLink = new Uri(SharedInkDeeplink);
            }
            if (!string.IsNullOrWhiteSpace(SharedInkUri))
            {
                requestData.SetWebLink(new Uri(SharedInkUri));
            }
            if (SharedInk30x30Logo != null)
            {
                var x = await SharedInk30x30Logo.OpenReadAsync();
                requestData.Properties.Square30x30Logo = SharedInk30x30Logo;
            }

            List<IStorageItem> imageItems = new List<IStorageItem>();
            imageItems.Add(_tempExportFile);
            requestData.SetStorageItems(imageItems);

            RandomAccessStreamReference imageStreamRef = RandomAccessStreamReference.CreateFromFile(_tempExportFile);
            requestData.Properties.Thumbnail = imageStreamRef;
            requestData.SetBitmap(imageStreamRef);
        }

        private readonly ObservableCollection<ToggleButton> _annotationTogglers = new ObservableCollection<ToggleButton>();
        public ObservableCollection<ToggleButton> AnnotationTogglers
        {
            get { return _annotationTogglers; }
        }

        public DependencyObject AssociatedObject { get; private set; }

        private void Initialize()
        {
            if (!_initialized)
            {
                _inkCanvas = new InkCanvas();
                _inkToolbar = new InkToolbar(this);

                InkDrawingAttributes drawingAttributes = new InkDrawingAttributes
                {
                    Color = PenColor,
                    Size = PenSize,
                    IgnorePressure = false,
                    FitToCurve = true,
                };

                _inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
                _inkCanvas.InkPresenter.InputDeviceTypes = Windows.UI.Core.CoreInputDeviceTypes.Mouse | Windows.UI.Core.CoreInputDeviceTypes.Pen | Windows.UI.Core.CoreInputDeviceTypes.Touch;
                
                Panel parent = (Panel)AssociatedObject;
                parent.Children.Add(_inkCanvas);
                parent.Children.Add(_inkToolbar);

                // The canvas utilized for adding annotations. the background is set to null to ensure hittesting on the inkcanvas works.
                _annotationCanvas = new Canvas { Background = null };
                parent.Children.Add(_annotationCanvas);
                parent.Tapped += Parent_Tapped;

                _inkCanvas.InkPresenter.IsInputEnabled = false;
                _inkCanvas.Width = 0;
                _inkCanvas.Height = 0;
                _inkCanvas.Visibility = Visibility.Collapsed;
                _inkToolbar.Visibility = Visibility.Collapsed;

                _initialized = true;

                ChangeDrawTool(SelectedTool);
            }
        }

        private void DeleteAnnotation(object sender)
        {
            Panel element = (Panel)sender;
            var toggleButton = element.FindChild<ToggleButton>();
            if (toggleButton != null)
            {
                AnnotationTogglers.Remove(toggleButton);
                toggleButton.PointerPressed -= ToggleButton_PointerPressed;
                toggleButton.PointerReleased -= ToggleButton_PointerReleased;
                //BUG: this would not be necessary if we could work out why the binding is not updating.
                foreach (var item in AnnotationTogglers)
                {
                    item.FindChild<TextBlock>().Text = (AnnotationTogglers.IndexOf(item) + 1).ToString();
                }
            }
            _annotationCanvas.Children.Remove(element);
        }

        private void Parent_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (SelectedTool == InkingToolMode.Annotate)
            {
                Point position = e.GetPosition(null);
                // Find if we're intersecting with the toolbar, because we don't want annotation screens there.
                IEnumerable<UIElement> intersectedElements = VisualTreeHelper.FindElementsInHostCoordinates(position, _inkToolbar);
                // Check what elements of the sender we're on top of. If there's a button, we'll intersect with our toggle button collection
                // to confirm whether we'd like to simply 'hittest' that button.
                IEnumerable<UIElement> allIntersectedElements = VisualTreeHelper.FindElementsInHostCoordinates(position, (UIElement)sender);

                // Not intersected with the toolbar and no togglebuttons (of ours) in the way indicating there's another annotation, go ahead
                // and create the XAML.
                if (intersectedElements.Count() == 0 && (AnnotationTogglers.Intersect(allIntersectedElements.OfType<ButtonBase>())).Count() == 0)
                {
                    var panel = new Grid { Width = 300, Height = 200 };
                    StackPanel collapsingPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    var toggleButtonText = new TextBlock { DataContext = this, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    var toggleButtonId = "tb" + Guid.NewGuid().ToString();
                    var toggleButtonContent = new Grid();
                    toggleButtonContent.Children.Add(toggleButtonText);
                    toggleButtonContent.Children.Add(new FontIcon { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = "\uE70B" });
                    var toggleButton = new ToggleButton { Content = new Viewbox { Child = toggleButtonContent }, Name = toggleButtonId, RenderTransform = new TranslateTransform { X = -10 }, Width = 32, Height = 32, Template = null, VerticalAlignment = VerticalAlignment.Top };
                    toggleButton.AddHandler(ToggleButton.PointerPressedEvent, new PointerEventHandler(ToggleButton_PointerPressed), true);
                    toggleButton.AddHandler(ToggleButton.PointerReleasedEvent, new PointerEventHandler(ToggleButton_PointerReleased), true);
                    toggleButton.AddHandler(ToggleButton.PointerMovedEvent, new PointerEventHandler(ToggleButton_PointerMoved), true);

                    toggleButton.PointerReleased += ToggleButton_PointerReleased;
                    AnnotationTogglers.Add(toggleButton);
                    toggleButtonText.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath("AnnotationTogglers"), Converter = listIndexerConverter, ConverterParameter = toggleButtonId, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                    collapsingPanel.Children.Add(toggleButton);

                    var innerGrid = new Grid();

                    CommandBar richTextEditBar = new CommandBar();
                    var boldButton = new AppBarButton { Label = "Bold", Icon = new SymbolIcon(Symbol.Bold) };
                    // TODO: remove when destroyed
                    boldButton.Click += BoldButton_Click;
                    richTextEditBar.PrimaryCommands.Add(boldButton);
                    var italicButton = new AppBarButton { Label = "Italic", Icon = new SymbolIcon(Symbol.Italic) };
                    // TODO: remove when destroyed
                    italicButton.Click += ItalicButton_Click;
                    richTextEditBar.PrimaryCommands.Add(italicButton);

                    innerGrid.Children.Add(new RichEditBox { Header = richTextEditBar, Width = 200 });
                    var deleteIcon = new FontIcon { Name = "deleteIcon", FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = "\uE74D", Margin = new Thickness(0, 0, 5, 5), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
                    deleteIcon.Tapped += new Windows.UI.Xaml.Input.TappedEventHandler((o, a) => { DeleteAnnotation(panel); });
                    innerGrid.Children.Add(deleteIcon);
                    innerGrid.SetBinding(Grid.VisibilityProperty, new Binding { ElementName = toggleButton.Name, Path = new PropertyPath("IsChecked"), Converter = booleanToVisibilityConverter });
                    collapsingPanel.Children.Add(innerGrid);
                    panel.Children.Add(collapsingPanel);

                    Canvas.SetLeft(panel, position.X);
                    Canvas.SetTop(panel, position.Y);

                    _annotationCanvas.Children.Add(panel);
                }
            }
        }

        #region Annotation text formatting support
        private void ItalicButton_Click(object sender, RoutedEventArgs e)
        {
            var editor = ((FrameworkElement)sender).FindAncestor<RichEditBox>();
            ITextSelection selectedText = editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.Italic = FormatEffect.Toggle;
                selectedText.CharacterFormat = charFormatting;
            }
        }

        private void BoldButton_Click(object sender, RoutedEventArgs e)
        {
            var editor = ((FrameworkElement)sender).FindAncestor<RichEditBox>();
            ITextSelection selectedText = editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.Bold = FormatEffect.Toggle;
                selectedText.CharacterFormat = charFormatting;
            }
        }
        #endregion

        #region Dragging support
        bool _isDragging = false;
        double originalX, originalY;
        private void ToggleButton_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                var point = e.GetCurrentPoint((FrameworkElement)AssociatedObject);
                ToggleButton tb = (ToggleButton)sender;
                var grid = tb.FindAncestor<Grid>();
                if (grid != null)
                {
                    Canvas.SetLeft(grid, point.Position.X);
                    Canvas.SetTop(grid, point.Position.Y);
                }
            }
        }

        private void ToggleButton_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isDragging = false;
        }

        private void ToggleButton_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var toggleButton = ((FrameworkElement)e.OriginalSource).FindAncestor<ToggleButton>();
            if (toggleButton != null && AnnotationTogglers.Contains(toggleButton))
            {
                _isDragging = true;
                PointerPoint point = e.GetCurrentPoint((FrameworkElement)AssociatedObject);
                originalX = point.Position.X;
                originalY = point.Position.Y;
            }
        }
        #endregion

        private static void IsEnabledPropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var behavior = sender as InkingBehavior;
            if (behavior.AssociatedObject == null || e.NewValue == null) return;

            behavior.Initialize();

            EnableInking(behavior, (bool)e.NewValue);
            ShowInkingObjects(behavior, (bool)e.NewValue);
        }

        private static void ShowInkingObjects(InkingBehavior behavior, bool isEnabled = true)
        {
            behavior._inkCanvas.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
            behavior._inkToolbar.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
            behavior._annotationCanvas.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void EnableInking(InkingBehavior behavior, bool isEnabled = true)
        {
            if (isEnabled)
            {
                if (behavior._inkCanvas != null)
                {
                    behavior._inkCanvas.InkPresenter.IsInputEnabled = true;

                    behavior._inkCanvas.Width = (double)behavior.AssociatedObject.GetValue(FrameworkElement.ActualWidthProperty);
                    behavior._inkCanvas.Height = (double)behavior.AssociatedObject.GetValue(FrameworkElement.ActualHeightProperty);

                    behavior._inkCanvas.Visibility = Visibility.Visible;
                }

                if (behavior._inkToolbar != null) behavior._inkToolbar.Visibility = Visibility.Visible;
            }
            else
            {
                if (behavior._inkCanvas != null)
                {
                    behavior._inkCanvas.InkPresenter.IsInputEnabled = false;
                    //behavior._inkCanvas.Width = 0;
                    //behavior._inkCanvas.Height = 0;

                    //behavior._inkCanvas.Visibility = Visibility.Collapsed;
                }
                //if (behavior._inkToolbar != null) behavior._inkToolbar.Visibility = Visibility.Collapsed;
            }
        }

        public void Attach(DependencyObject associatedObject)
        {
            var parentPanel = associatedObject as Panel;
            if (parentPanel == null)
            {
                throw new ArgumentException("InkingBehavior can be attached only to elements inheriting from Panel");
            }

            AssociatedObject = associatedObject;
        }

        public void Detach()
        {
            AssociatedObject = null;
        }

        public void ChangeDrawTool(InkingToolMode mode)
        {
            InkDrawingAttributes attributes = null;

            if (_inkCanvas != null)
            {
                switch (mode)
                {
                    case InkingToolMode.Annotate:
                        EnableInking(this, false);

                        break;
                    case InkingToolMode.Eraser:
                        EnableInking(this);
                        _inkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Erasing;
                        break;
                    case InkingToolMode.Marker:
                        EnableInking(this);
                        attributes = new InkDrawingAttributes
                        {
                            Color = MarkerColor,
                            PenTip = PenTipShape.Rectangle,
                            Size = MarkerSize,
                            DrawAsHighlighter = true,
                        };

                        _inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(attributes);
                        if (IsMarkerEnabled)
                        {
                            _inkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
                        }
                        break;
                    case InkingToolMode.Pen:
                        EnableInking(this);
                        attributes = new InkDrawingAttributes
                        {
                            Color = PenColor,
                            PenTip = PenTipShape.Rectangle,
                            Size = PenSize,
                            DrawAsHighlighter = false
                        };

                        _inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(attributes);
                        if (IsPenEnabled)
                        {
                            _inkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
                        }
                        break;
                    case InkingToolMode.None:
                        EnableInking(this, false);
                        break;
                }
            }
        }

        #region Properties
        public InkingToolMode SelectedTool
        {
            get { return (InkingToolMode)GetValue(SelectedToolProperty); }
            set { SetValue(SelectedToolProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectedTool.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedToolProperty =
            DependencyProperty.Register("SelectedTool", typeof(InkingToolMode), typeof(InkingBehavior), new PropertyMetadata(InkingToolMode.None, new PropertyChangedCallback(SelectedToolPropertyChanged)));

        private static void SelectedToolPropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var behavior = sender as InkingBehavior;
            behavior.ChangeDrawTool((InkingToolMode)e.NewValue);
        }

        /// <summary>
        /// When sharing the result of inking, this will be the title of the shared object
        /// </summary>
        public string SharedInkTitle
        {
            get { return (string)GetValue(SharedInkTitleProperty); }
            set { SetValue(SharedInkTitleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SharedInkTitle.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SharedInkTitleProperty =
            DependencyProperty.Register("SharedInkTitle", typeof(string), typeof(InkingBehavior), new PropertyMetadata(string.Empty));

        /// <summary>
        /// When sharing the result of inking, this will be the link to get back to this representation.
        /// </summary>
        public string SharedInkDeeplink
        {
            get { return (string)GetValue(SharedInkDeeplinkProperty); }
            set { SetValue(SharedInkDeeplinkProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SharedInkDeeplink.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SharedInkDeeplinkProperty =
            DependencyProperty.Register("SharedInkDeeplink", typeof(string), typeof(InkingBehavior), new PropertyMetadata(string.Empty));

        /// <summary>
        /// When sharing the result of inking, this will be the uri displayed as the source of the capture.
        /// </summary>
        public string SharedInkUri
        {
            get { return (string)GetValue(SharedInkUriProperty); }
            set { SetValue(SharedInkUriProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SharedInkUri.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SharedInkUriProperty =
            DependencyProperty.Register("SharedInkUri", typeof(string), typeof(InkingBehavior), new PropertyMetadata(string.Empty));

        /// <summary>
        /// When sharing the result of inking, this will be the description.
        /// </summary>
        public string SharedInkDescription
        {
            get { return (string)GetValue(SharedInkDescriptionProperty); }
            set { SetValue(SharedInkDescriptionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SharedInkDescription.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SharedInkDescriptionProperty =
            DependencyProperty.Register("SharedInkDescription", typeof(string), typeof(InkingBehavior), new PropertyMetadata(string.Empty));

        /// <summary>
        /// Defined a reference to an image to use as a logo when sharing the painted ink.
        /// </summary>
        public RandomAccessStreamReference SharedInk30x30Logo
        {
            get { return (RandomAccessStreamReference)GetValue(SharedInk30x30LogoProperty); }
            set { SetValue(SharedInk30x30LogoProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SharedInkLogo.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SharedInk30x30LogoProperty =
            DependencyProperty.Register("SharedInk30x30Logo", typeof(RandomAccessStreamReference), typeof(InkingBehavior), new PropertyMetadata(null));

        /// <summary>
        /// Determines whether the behavior is enabled.
        /// </summary>
        public bool IsEnabled
        {
            get { return (bool)GetValue(ValueProperty); }
            set { SetValueDp(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("IsEnabled", typeof(bool), typeof(InkingBehavior), new PropertyMetadata(false, IsEnabledPropertyChanged));

        /// <summary>
        /// The <see cref="InkCanvas"/> to be used for drawing ink.
        /// </summary>
        public InkCanvas InkCanvas
        {
            get { return _inkCanvas; }
            set
            {
                if (_inkCanvas != value)
                {
                    _inkCanvas = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsPenEnabled
        {
            get { return (bool)GetValue(IsPenEnabledProperty); }
            set { SetValue(IsPenEnabledProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsPenEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsPenEnabledProperty =
            DependencyProperty.Register("IsPenEnabled", typeof(bool), typeof(InkingBehavior), new PropertyMetadata(true));

        public bool IsMarkerEnabled
        {
            get { return (bool)GetValue(IsMarkerEnabledProperty); }
            set { SetValue(IsMarkerEnabledProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsMarkerEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsMarkerEnabledProperty =
            DependencyProperty.Register("IsMarkerEnabled", typeof(bool), typeof(InkingBehavior), new PropertyMetadata(true));

        public bool IsEraserEnabled
        {
            get { return (bool)GetValue(IsEraserEnabledProperty); }
            set { SetValue(IsEraserEnabledProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsEraserEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsEraserEnabledProperty =
            DependencyProperty.Register("IsEraserEnabled", typeof(bool), typeof(InkingBehavior), new PropertyMetadata(true));

        public bool IsAnnotationsEnabled
        {
            get { return (bool)GetValue(IsAnnotationsEnabledProperty); }
            set { SetValue(IsAnnotationsEnabledProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsAnnotationsEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsAnnotationsEnabledProperty =
            DependencyProperty.Register("IsAnnotationsEnabled", typeof(bool), typeof(InkingBehavior), new PropertyMetadata(true));
   
        public VerticalAlignment ToolbarVerticalAlignment
        {
            get { return (VerticalAlignment)GetValue(ToolbarVerticalAlignmentProperty); }
            set { SetValue(ToolbarVerticalAlignmentProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ToolbarVerticalAlignment.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ToolbarVerticalAlignmentProperty =
            DependencyProperty.Register("ToolbarVerticalAlignment", typeof(VerticalAlignment), typeof(InkingBehavior), new PropertyMetadata(VerticalAlignment.Top));

        public HorizontalAlignment ToolbarHorizontalAlignment
        {
            get { return (HorizontalAlignment)GetValue(ToolbarHorizontalAlignmentProperty); }
            set { SetValue(ToolbarHorizontalAlignmentProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ToolbarHorizontalAlignment.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ToolbarHorizontalAlignmentProperty =
            DependencyProperty.Register("ToolbarHorizontalAlignment", typeof(HorizontalAlignment), typeof(InkingBehavior), new PropertyMetadata(HorizontalAlignment.Right));

        public Color PenColor
        {
            get { return (Color)GetValue(PenColorProperty); }
            set { SetValueDp(PenColorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PenColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PenColorProperty =
            DependencyProperty.Register("PenColor", typeof(Color), typeof(InkToolbar), new PropertyMetadata(null, new PropertyChangedCallback(PenColor_Changed)));

        static void PenColor_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = d as InkingBehavior;
            if (behavior != null)
            {
                behavior.ChangeDrawTool(InkingToolMode.Pen);
            }
        }

        public Size PenSize
        {
            get { return (Size)GetValue(PenSizeProperty); }
            set { SetValueDp(PenSizeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PenSize.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PenSizeProperty =
            DependencyProperty.Register("PenSize", typeof(Size), typeof(InkToolbar), new PropertyMetadata(null, new PropertyChangedCallback(PenSize_Changed)));

        static void PenSize_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = d as InkingBehavior;
            if (behavior != null)
            {
                behavior.ChangeDrawTool(InkingToolMode.Pen);
            }
        }

        public Color MarkerColor
        {
            get { return (Color)GetValue(MarkerColorProperty); }
            set { SetValueDp(MarkerColorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MarkerColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MarkerColorProperty =
            DependencyProperty.Register("MarkerColor", typeof(Color), typeof(InkToolbar), new PropertyMetadata(null, new PropertyChangedCallback(MarkerColor_Changed)));

        static void MarkerColor_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = d as InkingBehavior;
            if (behavior != null)
            {
                behavior.ChangeDrawTool(InkingToolMode.Marker);
            }
        }

        public Size MarkerSize
        {
            get { return (Size)GetValue(MarkerSizeProperty); }
            set { SetValueDp(MarkerSizeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PenSize.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MarkerSizeProperty =
            DependencyProperty.Register("MarkerSize", typeof(Size), typeof(InkToolbar), new PropertyMetadata(null, new PropertyChangedCallback(MarkerSize_Changed)));

        static void MarkerSize_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = d as InkingBehavior;
            if (behavior != null)
            {
                behavior.ChangeDrawTool(InkingToolMode.Marker);
            }
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        void SetValueDp(DependencyProperty property, object value, [CallerMemberName] string propertyName = null)
        {
            SetValue(property, value);
            NotifyPropertyChanged(propertyName);
        }

        void NotifyPropertyChanged([CallerMemberName]string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                //TODO: also clean up events from annotations
                if (_dataTransferManager != null)
                {
                    _dataTransferManager.DataRequested -= new TypedEventHandler<DataTransferManager, DataRequestedEventArgs>(OnDataRequested);
                    _dataTransferManager = null;
                }
            }
        }
        #endregion
    }
}
