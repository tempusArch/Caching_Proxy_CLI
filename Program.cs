using System.Collections.Concurrent;
using System.Net;
using System.Text;

class Program {
    private static readonly HttpClient _httpClient = new();
    private static readonly ConcurrentDictionary<string,string> _cache = new();
    /*private static readonly HashSet<string> HopByHopHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade"
    };*/


    static async Task Main(string[] args) {
        if (args.Length == 1 && args[0] == "clear") {
            _cache.Clear();
            Console.WriteLine("Cache cleared");
            return;
        }

        if (args.Length != 2) {
            Console.WriteLine("Usage: <port> <destinationUrl> or clear");
            return;
        }

        if (!int.TryParse(args[0], out int port)) {
            Console.WriteLine("Invalid port number");
            return;
        }

        string destinationUrl = args[1].TrimEnd('/');

        HttpListener listener = new();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        Console.WriteLine($"Cache Proxy Server started on http://localhgost:{port}");

        while (true) {
            HttpListenerContext context = await listener.GetContextAsync();
            _ = Task.Run(() => HandleRequest(context, destinationUrl));
        }

    } 


    #region helper method
    private static async Task HandleRequest(HttpListenerContext context, string destinationUrl) {
        string relativeUrl = context.Request.Url.PathAndQuery;
        string fullUrl = $"{destinationUrl}{relativeUrl}";

        if (_cache.TryGetValue(fullUrl, out string cachedResponse)) {
            Console.WriteLine($"Cache HIT: {fullUrl}");
            context.Response.Headers["X-Cache"] = "HIT";

            byte[] buffer = Encoding.UTF8.GetBytes(cachedResponse);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);

        } else {
            /*var connectionHeader = context.Request.Headers["Connection"];
            if (!string.IsNullOrEmpty(connectionHeader))            
                foreach (var h in connectionHeader.Split(','))
                    HopByHopHeaders.Add(h.Trim());/**/
                   
            HttpRequestMessage forwardRequest = new(new HttpMethod(context.Request.HttpMethod), fullUrl);

            /*if (context.Request.HasEntityBody)
                forwardRequest.Content = new StreamContent(context.Request.InputStream);

            foreach (string header in context.Request.Headers) {
                if (HopByHopHeaders.Contains(header))
                    continue;

                if (!forwardRequest.Headers.TryAddWithoutValidation(header, context.Request.Headers[header]))
                    forwardRequest.Content?.Headers.TryAddWithoutValidation(header, context.Request.Headers[header]);
            }*/

            HttpResponseMessage forwardResponse = await _httpClient.SendAsync(forwardRequest);

            string responseString = await forwardResponse.Content.ReadAsStringAsync();
            _cache[fullUrl] = responseString;

            Console.WriteLine($"Cache MISS: {fullUrl}");
            context.Response.Headers["X-Cache"] = "MISS";

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length); 
        }

        context.Response.Close();
    }
    #endregion
}
