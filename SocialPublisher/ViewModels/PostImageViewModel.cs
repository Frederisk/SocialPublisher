using System;
using System.IO;

using Avalonia.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using SocialPublisher.Utils;

namespace SocialPublisher.ViewModels;

public partial class PostImageViewModel : ViewModelBase, IDisposable {
    private Boolean _disposed = false;

    public Byte[] ImageBytes { get; private set; }

    [ObservableProperty]
    private Bitmap? _displayImage;

    private readonly Action<PostImageViewModel> _removeAction;

    public PostImageViewModel(Byte[] bytes, Action<PostImageViewModel> removeAction) {
        this.ImageBytes = bytes;
        _removeAction = removeAction;
        this.UpdateDisplayImage();
    }

    //public PostImageViewModel(Bitmap bitmap, Action<PostImageViewModel> removeAction) {
    //    this.DisplayImage = bitmap;
    //    using MemoryStream stream = new();
    //    bitmap.Save(stream);
    //    this.ImageBytes = stream.ToArray();
    //    _removeAction = removeAction;
    //    //this.UpdateDisplayImage();
    //}

    [RelayCommand]
    public void Rotate() {
        this.ImageBytes = ImageHelper.RotateImage(this.ImageBytes);
        this.UpdateDisplayImage();
    }

    [RelayCommand]
    public void Remove() {
        _removeAction?.Invoke(this);
    }

    private void UpdateDisplayImage() {
        this.DisplayImage?.Dispose();
        using MemoryStream stream = new(this.ImageBytes);
        this.DisplayImage = new Bitmap(stream);
    }

    protected virtual void Dispose(Boolean disposing) {
        if (_disposed) return;

        if (disposing) {
            // Dispose managed resources
            this.DisplayImage?.Dispose();
        }
        // No unmanaged resources to release
        _disposed = true;
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
