using Avalonia.Media.Imaging;

using System;
using System.IO;

namespace SocialPublisher.Models;

public class PostImage : IDisposable {
    private Boolean _disposed = false;
    
    public Bitmap DisplayImage { get; }

    public Byte[] ImageBytes { get; }

    public PostImage(Byte[] bytes) {
        this.ImageBytes = bytes;
        using MemoryStream stream = new(bytes);
        this.DisplayImage = new Bitmap(stream);
    }

    public PostImage(Bitmap bitmap) {
        this.DisplayImage = bitmap;
        using MemoryStream stream = new();
        bitmap.Save(stream);
        this.ImageBytes = stream.ToArray();
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
