using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GoogleFormsReverseProxy
{
    public class GoogleFormsReverseProxyMiddleware
    {
        private const string GoogleFormsUrl = "https://docs.google.com/forms";
        private const string GoogleLocalPath = "/google";
        private const string GoogleStaticLocalPath = "/googlestatic";
        private const string GoogleUrl = "https://www.google.com";
        private const string GoogleStatisticsUrl = "https://www.gstatic.com";
        private const string HtmlContentType = "text/html";
        private const string JavascriptContentType = "text/javascript";


        private readonly RequestDelegate next;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly GoogleFormsReverseProxyMiddlewareOptions options;

        public GoogleFormsReverseProxyMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, IOptions<GoogleFormsReverseProxyMiddlewareOptions> options)
        {
            this.next = next;
            this.httpClientFactory = httpClientFactory;
            this.options = options.Value;
        }

        public async Task Invoke(HttpContext context)
        {
            var targetUrl = ResolveTargetUrl(context);

            if (targetUrl != null)
            {
                var targetRequestMessage = ResolveTargetRequestMessage(context, targetUrl);

                var client = httpClientFactory.CreateClient();

                using (var response = await client.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
                {
                    context.Response.StatusCode = (int)response.StatusCode;
                    ApplyTargetResponseHeadersToOriginalResponse(context, response);
                    await ProcessOriginalResponseContent(context, response);
                }

                return;
            }

            await next(context);
        }

        private async Task ProcessOriginalResponseContent(HttpContext context, HttpResponseMessage responseMessage)
        {
            var content = await responseMessage.Content.ReadAsByteArrayAsync();

            if (CheckContentType(responseMessage, HtmlContentType) ||
                CheckContentType(responseMessage, JavascriptContentType))
            {
                var stringContent = Encoding.UTF8.GetString(content);
                var newContent = stringContent.Replace("https://www.google.com", "/google")
                    .Replace("https://www.gstatic.com", "/googlestatic")
                    .Replace("https://docs.google.com/forms", "/googleforms"); ;
                await context.Response.WriteAsync(newContent, Encoding.UTF8);
            }
            else
            {
                await context.Response.Body.WriteAsync(content);
            }
        }

        private bool CheckContentType(HttpResponseMessage responseMessage, string contentType)
        {
            if (responseMessage.Content?.Headers?.ContentType == null)
            {
                return false;
            }

            return responseMessage.Content.Headers.ContentType.MediaType == contentType;
        }

        private void ApplyTargetResponseHeadersToOriginalResponse(HttpContext context, HttpResponseMessage response)
        {
            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            //To avoid ERR_INVALID_CHUNKED_ENCODING error, this header is not required for proxy-browser interaction
            context.Response.Headers.Remove("transfer-encoding");
        }

        private HttpRequestMessage ResolveTargetRequestMessage(HttpContext context, Uri url)
        {
            var targetUrl = url;
            var result = new HttpRequestMessage();
            ApplyOriginalRequestContentAndHeadersToTargetRequest(context, result);

            if (options.PrepopulatedFormFields.Any())
            {
                targetUrl = new Uri(QueryHelpers.AddQueryString(url.OriginalString, options.PrepopulatedFormFields));
            }

            result.RequestUri = targetUrl;
            result.Headers.Host = url.Host;
            result.Method = new HttpMethod(context.Request.Method);

            return result;
        }

        private void ApplyOriginalRequestContentAndHeadersToTargetRequest(HttpContext context, HttpRequestMessage requestMessage)
        {
            var requestMethod = context.Request.Method;

            if (!IsBodylessRequest(requestMethod))
            {
                var streamContent = new StreamContent(context.Request.Body);
                requestMessage.Content = streamContent;
            }

            foreach (var header in context.Request.Headers)
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        private static bool IsBodylessRequest(string requestMethod)
        {
            return HttpMethods.IsGet(requestMethod) ||
                HttpMethods.IsHead(requestMethod) ||
                HttpMethods.IsDelete(requestMethod) ||
                HttpMethods.IsTrace(requestMethod);
        }

        private Uri ResolveTargetUrl(HttpContext context)
        {
            Uri result = null;

            if (context.Request.Path.StartsWithSegments(options.GoogleFormsLocalPath, out PathString remainingPath))
            {
                result = new Uri(GoogleFormsUrl + remainingPath);
            }

            if (context.Request.Path.StartsWithSegments(GoogleLocalPath, out remainingPath))
            {
                result = new Uri(GoogleUrl + remainingPath);
            }

            if (context.Request.Path.StartsWithSegments(GoogleStaticLocalPath, out remainingPath))
            {
                result = new Uri(GoogleStatisticsUrl + remainingPath);
            }

            return result;
        }
    }
}
