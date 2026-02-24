using System;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.DependencyInjection;

using SocialPublisher.Utils;

namespace SocialPublisher.ViewModels;

public partial class PostImageViewModel : ViewModelBase, IDisposable {
    private Boolean _disposed = false;

    public Byte[] ImageBytes { get; private set; }

    [ObservableProperty]
    private Bitmap? _displayImage;

    private readonly Action<PostImageViewModel> _removeAction;
    private readonly Action<PostImageViewModel> _openAction;

    public PostImageViewModel() {
        if (Design.IsDesignMode) {
            this.ImageBytes = [];
            _removeAction = _ => { };
            _openAction = _ => { };
        } else {
            throw new InvalidOperationException("Use the constructor with parameters.");
        }
    }

    [ActivatorUtilitiesConstructor]
    public PostImageViewModel(Byte[] bytes, Action<PostImageViewModel> removeAction, Action<PostImageViewModel> openAction) {
        this.ImageBytes = bytes;
        _removeAction = removeAction;
        this.UpdateDisplayImageAsync();
        _openAction = openAction;
    }

    [RelayCommand]
    public void Open() {
        _openAction?.Invoke(this);
    }

    [RelayCommand]
    public async Task RotateAsync() {
        if (_disposed) {
            return;
        }

        this.ImageBytes = await ImageHelper.RotateImageAsync(this.ImageBytes);
        this.UpdateDisplayImageAsync();
    }

    [RelayCommand]
    public void Remove() {
        _removeAction?.Invoke(this);
    }

    private void UpdateDisplayImageAsync() {
        Task.Run(() => {
            if (_disposed) return;

            Bitmap newBitmap;
            using (MemoryStream stream = new(this.ImageBytes)) {
                newBitmap = Bitmap.DecodeToWidth(stream, 300);
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                if (_disposed) {
                    newBitmap.Dispose();
                    return;
                }

                var oldImage = this.DisplayImage;
                this.DisplayImage = newBitmap;

                oldImage?.Dispose();
            });
        });
    }

    protected virtual void Dispose(Boolean disposing) {
        if (_disposed) return;

        if (disposing) {
            this.DisplayImage?.Dispose();
        }
        _disposed = true;
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
