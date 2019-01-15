using Polly;
using Polly.CircuitBreaker;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoolyTests
{
    internal class Program
    {
        private static HttpStatusCode[] httpStatusCodesWorthRetrying = {
            HttpStatusCode.RequestTimeout, // 408
            HttpStatusCode.InternalServerError, // 500
            HttpStatusCode.BadGateway, // 502
            HttpStatusCode.ServiceUnavailable, // 503
            HttpStatusCode.GatewayTimeout // 504
        };

        private static Policy<HttpResponseMessage> breaker = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
            .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30));

        private static async Task Main(string[] args)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    (await RunTest()).EnsureSuccessStatusCode();
                }
                catch (BrokenCircuitException<HttpResponseMessage> ex)
                {
                    Console.WriteLine(ex.Result);
                }
                catch (BrokenCircuitException<HttpRequestException> ex)
                {
                    Console.WriteLine(ex.Result);
                }
                catch (BrokenCircuitException ex)
                {
                    Console.WriteLine(ex);
                    //throw ex.InnerException;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        private static Task<HttpResponseMessage> RunTest()
        {
            return breaker.ExecuteAsync(async () =>
            {
            var response = await RunHttpRequest();
                //response.EnsureSuccessStatusCode();
                return response;
        });
        }

        private static async Task<HttpResponseMessage> RunHttpRequest()
        {
            var client = new HttpClient()
            {
                BaseAddress = new Uri(@"https://www.google.com"),
                Timeout = TimeSpan.FromSeconds(30)
            };

            var random = new Random();
            var x = random.Next(100);
            if (x % 3 == 0)
            {
                throw new HttpRequestException("Divided by 3");
            }
            else if (x % 2 != 0)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return await client.GetAsync("/");
        }
    }
}
