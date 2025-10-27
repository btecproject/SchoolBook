using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace SchoolBookPlatform.Services
{
    public class FaceService
    {
        private readonly string _endpoint;
        private readonly string _key;
        private readonly IHttpClientFactory _httpFactory;

        public FaceService(IConfiguration config, IHttpClientFactory httpFactory)
        {
            _endpoint = config["AzureFace:Endpoint"].TrimEnd('/');
            _key = config["AzureFace:Key"];
            _httpFactory = httpFactory;
        }

        public async Task<string?> DetectFaceAsync(Stream imageStream)
        {
            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _key);

            var url = $"{_endpoint}/face/v1.0/detect?returnFaceId=true&recognitionModel=recognition_04&detectionModel=detection_03";
            imageStream.Position = 0;
            using var content = new StreamContent(imageStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var resp = await client.PostAsync(url, content);
            var json = await resp.Content.ReadAsStringAsync();
            resp.EnsureSuccessStatusCode();

            var arr = JArray.Parse(json);
            if (arr.Count == 0) return null;

            return arr[0]["faceId"]?.ToString();
        }

        public async Task<(bool isIdentical, double confidence)> VerifyFaceAsync(Stream imageStream, string referenceFaceId)
        {
            // 1. detect face in new image
            var newFaceId = await DetectFaceAsync(imageStream);
            if (newFaceId == null)
                return (false, 0);

            // 2. call verify
            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _key);

            var url = $"{_endpoint}/face/v1.0/verify";
            var body = new
            {
                faceId1 = newFaceId,
                faceId2 = referenceFaceId
            };
            var stringBody = Newtonsoft.Json.JsonConvert.SerializeObject(body);
            var content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            var resp = await client.PostAsync(url, content);
            var json = await resp.Content.ReadAsStringAsync();
            resp.EnsureSuccessStatusCode();

            var obj = JObject.Parse(json);
            bool isIdentical = obj["isIdentical"].Value<bool>();
            double confidence = obj["confidence"].Value<double>();

            return (isIdentical, confidence);
        }
    }
}
