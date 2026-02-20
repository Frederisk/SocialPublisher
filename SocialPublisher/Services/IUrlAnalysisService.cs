using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SocialPublisher.Services;

public interface IUrlAnalysisImagesService {
    public Task<List<Byte[]>> AnalysisImagesAsync(String uri, String token);
}
