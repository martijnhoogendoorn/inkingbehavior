using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Dev.MartijnHoogendoorn.Inking.Behavior.Extensions
{
    public static class PanelExtensions
    {
        private static string _strokeCanvasName = Guid.NewGuid().ToString();
        public static async Task SaveScreenshotAsync(this Panel parent, InkCanvas inkCanvas = null, List<FrameworkElement> excludedElements = null)
        {
            var file = await PickSaveImageAsync();

            await SaveScreenshotInternal(file, parent, inkCanvas, excludedElements);
        }

        public static async Task<StorageFile> SaveScreenshotTemporaryAsync(this Panel parent, InkCanvas inkCanvas = null, List<FrameworkElement> excludedElements = null)
        {
            StorageFile tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("dataFile.png", CreationCollisionOption.ReplaceExisting);
            await SaveScreenshotInternal(tempFile, parent, inkCanvas, excludedElements);

            return tempFile;
        }

        private static async Task SaveScreenshotInternal(StorageFile file, Panel parent, InkCanvas inkCanvas = null, List<FrameworkElement> excludedElements = null)
        {
            var strokeCanvas = new Canvas() { Name = _strokeCanvasName, Background = new SolidColorBrush(Colors.Transparent) };
            if (inkCanvas != null)
            {
                // add all inkCanvas strokes as children of the parent panel,
                // allowing us to save those with the RenderTargetBitmap
                RenderAllStrokes(inkCanvas, strokeCanvas);
                parent.Children.Add(strokeCanvas);
            }

            if (excludedElements != null)
            {
                foreach (FrameworkElement element in excludedElements)
                {
                    element.Visibility = Visibility.Collapsed;
                }
            }

            await SaveToFileAsync(parent, file);

            if (inkCanvas != null)
            {
                // HACK: there's an appearant bug in InkCanvas causing strokes to disappear after the rendering
                // we need to ensure the inkcanvas is updated.
                inkCanvas.Width += 1;
                inkCanvas.Width -= 1;

                parent.Children.Remove(strokeCanvas);
            }

            if (excludedElements != null)
            {
                foreach (FrameworkElement element in excludedElements)
                {
                    element.Visibility = Visibility.Visible;
                }
            }
        }

        private static async Task<StorageFile> PickSaveImageAsync()
        {
            var filePicker = new FileSavePicker();
            filePicker.FileTypeChoices.Add("Bitmap", new List<string>() { ".bmp" });
            filePicker.FileTypeChoices.Add("JPEG format", new List<string>() { ".jpg" });
            filePicker.FileTypeChoices.Add("Compuserve format", new List<string>() { ".gif" });
            filePicker.FileTypeChoices.Add("Portable Network Graphics", new List<string>() { ".png" });
            filePicker.FileTypeChoices.Add("Tagged Image File Format", new List<string>() { ".tif" });
            filePicker.DefaultFileExtension = ".png";
            filePicker.SuggestedFileName = "screenshot";
            filePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            filePicker.SettingsIdentifier = "picture picker";
            filePicker.CommitButtonText = "Save picture";

            return await filePicker.PickSaveFileAsync();
        }


        private static async Task<RenderTargetBitmap> SaveToFileAsync(FrameworkElement uielement, StorageFile file)
        {
            if (file != null)
            {
                CachedFileManager.DeferUpdates(file);

                Guid encoderId = GetBitmapEncoder(file.FileType);

                try
                {
                    using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        return await CaptureToStreamAsync(uielement, stream, encoderId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }

                var status = await CachedFileManager.CompleteUpdatesAsync(file);
            }

            return null;
        }

        private static Guid GetBitmapEncoder(string fileType)
        {
            Guid encoderId = BitmapEncoder.JpegEncoderId;
            switch (fileType)
            {
                case ".bmp": encoderId = BitmapEncoder.BmpEncoderId; break;
                case ".gif": encoderId = BitmapEncoder.GifEncoderId; break;
                case ".png": encoderId = BitmapEncoder.PngEncoderId; break;
                case ".tif": encoderId = BitmapEncoder.TiffEncoderId; break;
            }

            return encoderId;
        }

        private static async Task<RenderTargetBitmap> CaptureToStreamAsync(FrameworkElement uielement, IRandomAccessStream stream, Guid encoderId)
        {
            try
            {
                var logicalDpi = DisplayInformation.GetForCurrentView().LogicalDpi;
                var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
                var renderTargetBitmap = new RenderTargetBitmap();
                await renderTargetBitmap.RenderAsync(uielement);

                var pixels = await renderTargetBitmap.GetPixelsAsync();

                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    (uint)renderTargetBitmap.PixelWidth,
                    (uint)renderTargetBitmap.PixelHeight,
                    logicalDpi,
                    logicalDpi,
                    pixels.ToArray());

                await encoder.FlushAsync();

                return renderTargetBitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            return null;
        }

        private static void RenderAllStrokes(InkCanvas canvas, Panel parent)
        {
            // Get the InkStroke objects.
            IReadOnlyList<InkStroke> inkStrokes = canvas.InkPresenter.StrokeContainer.GetStrokes();

            List<Windows.UI.Xaml.Shapes.Path> retVal = new List<Windows.UI.Xaml.Shapes.Path>();

            // Process each stroke.
            foreach (InkStroke inkStroke in inkStrokes)
            {
                PathGeometry pathGeometry = new PathGeometry();
                PathFigureCollection pathFigures = new PathFigureCollection();
                PathFigure pathFigure = new PathFigure();
                PathSegmentCollection pathSegments = new PathSegmentCollection();

                // Create a path and define its attributes.
                Windows.UI.Xaml.Shapes.Path path = new Windows.UI.Xaml.Shapes.Path();
                path.Stroke = new SolidColorBrush(inkStroke.DrawingAttributes.Color);
                path.StrokeThickness = inkStroke.DrawingAttributes.Size.Height;
                if (inkStroke.DrawingAttributes.DrawAsHighlighter)
                {
                    path.Opacity = .4d;
                }

                // Get the stroke segments.
                IReadOnlyList<InkStrokeRenderingSegment> segments;
                segments = inkStroke.GetRenderingSegments();

                // Process each stroke segment.
                bool first = true;
                foreach (InkStrokeRenderingSegment segment in segments)
                {
                    // The first segment is the starting point for the path.
                    if (first)
                    {
                        pathFigure.StartPoint = segment.BezierControlPoint1;
                        first = false;
                    }

                    // Copy each ink segment into a bezier segment.
                    BezierSegment bezSegment = new BezierSegment();
                    bezSegment.Point1 = segment.BezierControlPoint1;
                    bezSegment.Point2 = segment.BezierControlPoint2;
                    bezSegment.Point3 = segment.Position;

                    // Add the bezier segment to the path.
                    pathSegments.Add(bezSegment);
                }

                // Build the path geometerty object.
                pathFigure.Segments = pathSegments;
                pathFigures.Add(pathFigure);
                pathGeometry.Figures = pathFigures;

                // Assign the path geometry object as the path data.
                path.Data = pathGeometry;

                // Render the path by adding it as a child of the Canvas object.
                parent.Children.Add(path);
            }
        }
    }
}