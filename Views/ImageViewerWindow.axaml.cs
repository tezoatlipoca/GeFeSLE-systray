using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;

namespace GeFeSLE.Views
{
    public partial class ImageViewerWindow : Window
    {
        private Bitmap? _originalBitmap;
        private double _originalImageWidth;
        private double _originalImageHeight;
        private bool _isFirstShow = true;
        private double _currentZoom = 1.0;
        private const double MIN_ZOOM = 0.1;
        private const double MAX_ZOOM = 5.0;
        private const double ZOOM_FACTOR = 1.2;

        // Drag state
        private bool _isDragging = false;
        private bool _hasDragged = false;
        private Point _lastPointerPosition;
        private Point _dragStartPosition;
        private const double DRAG_THRESHOLD = 5.0; // Minimum distance to consider it a drag

        public ImageViewerWindow()
        {
            InitializeComponent();
            KeyDown += OnKeyDown;
            Loaded += OnLoaded;
        }

        public void SetImage(Bitmap bitmap, string? title = null)
        {
            _originalBitmap = bitmap;
            _originalImageWidth = bitmap.Size.Width;
            _originalImageHeight = bitmap.Size.Height;
            
            ViewerImage.Source = bitmap;
            
            if (!string.IsNullOrEmpty(title))
            {
                Title = $"Image Viewer - {title}";
            }
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (_isFirstShow && _originalBitmap != null)
            {
                _isFirstShow = false;
                SetInitialZoom();
            }
        }

        private void SetInitialZoom()
        {
            if (_originalBitmap == null) return;

            // Get the available screen space (window client area)
            var windowBounds = ClientSize;
            var availableWidth = windowBounds.Width - 80; // Account for margins and scrollbars
            var availableHeight = windowBounds.Height - 120; // Account for margins and UI elements

            // Calculate the scale to fit the image within the window
            var scaleToFitWidth = availableWidth / _originalImageWidth;
            var scaleToFitHeight = availableHeight / _originalImageHeight;
            var scaleToFit = Math.Min(scaleToFitWidth, scaleToFitHeight);

            // Don't scale up beyond 1:1 (100%)
            _currentZoom = Math.Min(1.0, scaleToFit);

            ApplyZoom();
            
            DBg.d(LogLevel.Debug, $"Set initial zoom to {_currentZoom:F2} (fit: {scaleToFit:F2}, image: {_originalImageWidth}x{_originalImageHeight}, window: {availableWidth:F0}x{availableHeight:F0})");
        }

        private void ApplyZoom()
        {
            if (_originalBitmap == null) return;

            var transform = new ScaleTransform(_currentZoom, _currentZoom);
            ImageBorder.RenderTransform = transform;
            
            // Update the border size to match the scaled image
            ImageBorder.Width = _originalImageWidth * _currentZoom;
            ImageBorder.Height = _originalImageHeight * _currentZoom;
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var oldZoom = _currentZoom;
            
            if (e.Delta.Y > 0)
            {
                // Zoom in
                _currentZoom = Math.Min(_currentZoom * ZOOM_FACTOR, MAX_ZOOM);
            }
            else
            {
                // Zoom out
                _currentZoom = Math.Max(_currentZoom / ZOOM_FACTOR, MIN_ZOOM);
            }

            if (Math.Abs(_currentZoom - oldZoom) > 0.001)
            {
                ApplyZoom();
                DBg.d(LogLevel.Debug, $"Zoom changed to {_currentZoom:F2}");
            }

            e.Handled = true;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void OnBackgroundPressed(object? sender, PointerPressedEventArgs e)
        {
            // Only close on left click on the background, not on drag actions
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !_hasDragged)
            {
                // Check if we're clicking on the dark background, not the image
                var hitTest = this.InputHitTest(e.GetPosition(this));
                if (hitTest == BackgroundOverlay)
                {
                    Close();
                }
            }
        }

        private void OnImagePressed(object? sender, PointerPressedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(ImageScrollViewer);
            
            // Start tracking for potential drag on any button press
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || 
                e.GetCurrentPoint(this).Properties.IsRightButtonPressed ||
                e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                _isDragging = true;
                _hasDragged = false;
                _dragStartPosition = pointer.Position;
                _lastPointerPosition = pointer.Position;
                e.Handled = true;
            }
        }

        private void OnScrollViewerPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isDragging)
            {
                var currentPosition = e.GetCurrentPoint(ImageScrollViewer).Position;
                var deltaX = currentPosition.X - _lastPointerPosition.X;
                var deltaY = currentPosition.Y - _lastPointerPosition.Y;

                // Check if we've moved enough to consider this a drag
                var totalDistance = Math.Sqrt(
                    Math.Pow(currentPosition.X - _dragStartPosition.X, 2) + 
                    Math.Pow(currentPosition.Y - _dragStartPosition.Y, 2)
                );

                if (totalDistance > DRAG_THRESHOLD)
                {
                    _hasDragged = true;
                    
                    // Change cursor to indicate dragging
                    if (ImageScrollViewer.Cursor != new Cursor(StandardCursorType.Hand))
                    {
                        ImageScrollViewer.Cursor = new Cursor(StandardCursorType.Hand);
                    }

                    // Pan the scroll viewer
                    var newOffsetX = ImageScrollViewer.Offset.X - deltaX;
                    var newOffsetY = ImageScrollViewer.Offset.Y - deltaY;
                    
                    ImageScrollViewer.Offset = new Vector(newOffsetX, newOffsetY);
                }
                
                _lastPointerPosition = currentPosition;
                e.Handled = true;
            }
        }

        private void OnScrollViewerPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ImageScrollViewer.Cursor = Cursor.Default;
                
                // If it was just a click (no drag), close the viewer on left click
                if (!_hasDragged && e.InitialPressMouseButton == MouseButton.Left)
                {
                    Close();
                }
                
                _hasDragged = false;
                e.Handled = true;
            }
        }

        private void OnClosePressed(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
