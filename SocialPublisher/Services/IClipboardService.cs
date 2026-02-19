using SocialPublisher.Models;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialPublisher.Services;

public interface IClipboardService {
    public Task<List<PostImage>> GetImagesFromClipboardAsync();
}
