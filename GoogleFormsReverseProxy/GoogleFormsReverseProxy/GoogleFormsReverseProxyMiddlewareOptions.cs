using System.Collections.Generic;

namespace GoogleFormsReverseProxy
{
    public class GoogleFormsReverseProxyMiddlewareOptions
    {
        public string GoogleFormsLocalPath = "/googleforms";

        public Dictionary<string, string> PrepopulatedFormFields = new Dictionary<string, string>();
    }
}
