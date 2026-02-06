using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ResearchHub.App.Controls;

public partial class PdfViewerControl : UserControl
{
    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<PdfViewerControl, string?>(nameof(FilePath));

    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    private int _currentPage;
    private int _pageCount;
    private double _zoomLevel = 1.0;
    private Bitmap? _currentBitmap;
    private readonly SemaphoreSlim _renderLock = new(1, 1);

    private const double BaseDpi = 144;
    private const double MaxDpi = 288;
    private const double MinZoom = 0.25;
    private const double MaxZoom = 3.0;

    public PdfViewerControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FilePathProperty)
        {
            _ = LoadPdfAsync();
        }
    }

    private async Task LoadPdfAsync()
    {
        var path = FilePath;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            ClearDisplay();
            return;
        }

        await _renderLock.WaitAsync();
        try
        {
            _pageCount = await Task.Run(() =>
            {
                using var stream = File.OpenRead(path);
                return PDFtoImage.Conversion.GetPageCount(stream);
            });

            _currentPage = 0;
            _zoomLevel = 1.0;
            NoPdfOverlay.IsVisible = false;
            UpdateIndicators();
            await RenderCurrentPageAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PDF load error: {ex.Message}");
            ClearDisplay();
        }
        finally
        {
            _renderLock.Release();
        }
    }

    private async Task RenderCurrentPageAsync()
    {
        var path = FilePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path) || _pageCount == 0) return;

        var dpi = (int)Math.Min(BaseDpi * _zoomLevel, MaxDpi);
        var page = _currentPage;
        var options = new PDFtoImage.RenderOptions(Dpi: dpi);

        try
        {
            var pngBytes = await Task.Run(() =>
            {
                using var stream = File.OpenRead(path);
                using var rendered = PDFtoImage.Conversion.ToImage(stream, page, false, null, options);
                using var ms = new MemoryStream();
                rendered.Encode(ms, SkiaSharp.SKEncodedImageFormat.Png, 90);
                return ms.ToArray();
            });

            var oldBitmap = _currentBitmap;
            using var bitmapStream = new MemoryStream(pngBytes);
            _currentBitmap = new Bitmap(bitmapStream);

            if (_zoomLevel > 1.0 && BaseDpi * _zoomLevel > MaxDpi)
            {
                var scale = _zoomLevel * BaseDpi / MaxDpi;
                PageImage.Width = _currentBitmap.PixelSize.Width * scale;
                PageImage.Height = _currentBitmap.PixelSize.Height * scale;
            }
            else
            {
                PageImage.Width = double.NaN;
                PageImage.Height = double.NaN;
            }

            PageImage.Source = _currentBitmap;
            oldBitmap?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PDF render error: {ex.Message}");
        }
    }

    private void ClearDisplay()
    {
        var old = _currentBitmap;
        _currentBitmap = null;
        PageImage.Source = null;
        _pageCount = 0;
        _currentPage = 0;
        _zoomLevel = 1.0;
        NoPdfOverlay.IsVisible = true;
        UpdateIndicators();
        old?.Dispose();
    }

    private void UpdateIndicators()
    {
        PageIndicator.Text = _pageCount > 0
            ? $"{_currentPage + 1} / {_pageCount}"
            : "0 / 0";
        ZoomIndicator.Text = $"{(int)(_zoomLevel * 100)}%";
    }

    private async void PreviousPage_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentPage <= 0) return;
        await _renderLock.WaitAsync();
        try
        {
            _currentPage--;
            UpdateIndicators();
            await RenderCurrentPageAsync();
        }
        finally { _renderLock.Release(); }
    }

    private async void NextPage_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentPage >= _pageCount - 1) return;
        await _renderLock.WaitAsync();
        try
        {
            _currentPage++;
            UpdateIndicators();
            await RenderCurrentPageAsync();
        }
        finally { _renderLock.Release(); }
    }

    private async void ZoomIn_Click(object? sender, RoutedEventArgs e)
    {
        if (_zoomLevel >= MaxZoom || _pageCount == 0) return;
        await _renderLock.WaitAsync();
        try
        {
            _zoomLevel = Math.Min(_zoomLevel + 0.25, MaxZoom);
            UpdateIndicators();
            await RenderCurrentPageAsync();
        }
        finally { _renderLock.Release(); }
    }

    private async void ZoomOut_Click(object? sender, RoutedEventArgs e)
    {
        if (_zoomLevel <= MinZoom || _pageCount == 0) return;
        await _renderLock.WaitAsync();
        try
        {
            _zoomLevel = Math.Max(_zoomLevel - 0.25, MinZoom);
            UpdateIndicators();
            await RenderCurrentPageAsync();
        }
        finally { _renderLock.Release(); }
    }

    private async void FitWidth_Click(object? sender, RoutedEventArgs e)
    {
        if (_pageCount == 0) return;
        await _renderLock.WaitAsync();
        try
        {
            _zoomLevel = 1.0;
            UpdateIndicators();
            await RenderCurrentPageAsync();
        }
        finally { _renderLock.Release(); }
    }

    private void OpenExternal_Click(object? sender, RoutedEventArgs e)
    {
        var path = FilePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Open external error: {ex.Message}");
        }
    }
}
