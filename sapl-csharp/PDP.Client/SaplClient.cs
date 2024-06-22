/*
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Reactive.Linq;
using System.Reactive;
using Newtonsoft.Json.Linq;
using System.IO;
using csharp.sapl.constraint.ServerEventHandling;
using SAPL.PDP.Api;

namespace csharp.sapl.pdp.remote
{
    internal class SaplClient : IManagePDPConnection
    {
        #region private connection fields

        private static readonly object padlock = new object();
        private static SaplClient current = null!;
        private static string apiKey = "sapl_7A7ByyQd6U_5nTv3KXXLPiZ8JzHQywF9gww2v0iuA3j";
        static string _creds = string.Format("{0}:{1}", "Bearer ", apiKey);
        private static Uri _baseUri = new Uri("https://localhost:8443");
        private string creds = null!;
        //private string authentification = string.Format("Basic {0}", Convert.ToBase64String(Encoding.UTF8.GetBytes(creds)));
        private Uri baseUri = null!;
        #endregion


        private SaplClient() { }

        public static SaplClient Current
        {
            get
            {
                lock (padlock)
                {
                    if (current == null)
                    {
                        current = new SaplClient();
                        SetCredentials(current);
                    }
                    return current;
                }
            }
        }

        public void SetDeviatingCredentials(string uri, string apiKey)
        {
            creds = $"Bearer {apiKey}";
            this.baseUri = new Uri(uri);
        }

        private static void SetCredentials(SaplClient instance)
        {
            instance.creds = _creds;
            instance.baseUri = _baseUri;
        }

        /// <summary>
        /// Get one decision for one time
        /// No Streaming
        /// </summary>
        /// <param name="authzSubscription"></param>
        /// <param name="relativeUri"></param>
        /// <param name="observer"></param>
        /// <returns></returns>
        public async Task<AuthorizationDecision> DecideOnceAsync(AuthorizationSubscription authzSubscription, string relativeUri, IObserver<AuthorizationDecision> observer = null!)
        {
            AuthorizationDecision decision;
            try
            {
                if (authzSubscription == null) throw new ArgumentNullException(nameof(authzSubscription));
                Uri decisionUri = new Uri(baseUri, relativeUri);
                ByteArrayContent httpContent = HttpContentForJson(authzSubscription);
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, decisionUri);
                httpRequestMessage.Content = httpContent;
                HttpClient client = new HttpClient(CreateHttpClientHandler());
                //client.DefaultRequestHeaders.Add("Authorization", authentification);
                client.DefaultRequestHeaders.Add("Authorization", creds);
                using (HttpResponseMessage response =
                       await client.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseContentRead))
                {

                    Stream stream = await response.Content.ReadAsStreamAsync();
                    string data;
                    using (var sr = new StreamReader(stream))
                    {
                        data = await sr.ReadToEndAsync();
                        decision = JsonConvert.DeserializeObject<AuthorizationDecision>(data) ??
                                   throw new InvalidOperationException();
                        if (observer != null)
                        {
                            observer.OnNext(decision);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                decision = AuthorizationDecision.INDETERMINATE;
                if (observer != null)
                {
                    observer.OnError(e);
                    observer.OnNext(decision);
                }
            }
            return decision;
        }

        /// <summary>
        /// Subscribe to observing a single Decision without waiting for a Task
        /// Event based information handling
        /// </summary>
        /// <param name="authzSubscription"></param>
        /// <param name="observer"></param>
        /// <param name="relativeUri"></param>
        /// <returns></returns>
        public async Task SubscribeDecisionOnce(AuthorizationSubscription authzSubscription,
            IObserver<AuthorizationDecision> observer, string relativeUri)
        {
            await DecideOnceAsync(authzSubscription, relativeUri, observer);
        }

        /// <summary>
        /// Subscribe to observe Decision
        /// </summary>
        /// <param name="authzSubscription"></param>
        /// <param name="observer"></param>
        /// <param name="relativeUri"></param>
        /// <returns></returns>
        public Task SubscribeDecision(AuthorizationSubscription authzSubscription,
             IObserver<AuthorizationDecision> observer, string relativeUri)
        {
            Uri decisionUri = new Uri(baseUri, relativeUri);
            ByteArrayContent httpContent = HttpContentForJson(authzSubscription);
            EventStreamReader evt = new EventStreamReader(creds, decisionUri, httpContent, CreateHttpClientHandler());
            evt.Start();
            IObservable<EventPattern<EventStreamMessageEventArgs>> receivedMassege = Observable.FromEventPattern<EventStreamMessageEventArgs>(evt, nameof(evt.MessageReceived));
            receivedMassege.Subscribe(pattern =>
                observer.OnNext(JsonConvert.DeserializeObject<AuthorizationDecision>(pattern.EventArgs.Message)!));
            IObservable<EventPattern<EventStreamDisconnectEventArgs>> disconnected = Observable.FromEventPattern<EventStreamDisconnectEventArgs>(evt, nameof(evt.Disconnected));
            async void OnNext(EventPattern<EventStreamDisconnectEventArgs> pattern) => await Restart(evt);
            disconnected.Subscribe(OnNext);
            return Task.CompletedTask;
        }

        #region private methods

        private static HttpClientHandler CreateHttpClientHandler()
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = IsServerCertificateValid;
            return handler;
        }

        private ByteArrayContent HttpContentForJson(object subscription)
        {
            ByteArrayContent httpContent = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(subscription)));
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return httpContent;
        }

        private async Task Restart(EventStreamReader evt)
        {
            Debug.WriteLine($"Retry: {100}");
            await Task.Delay(100);
            evt.Start();
        }



        private async void DisposeStream(EventStreamReader evt)
        {
            Debug.WriteLine($"Dispose: {10000}");
            await Task.Delay(10000);
            evt.Dispose();
        }

        private async void DisposeStream(EventStreamReader evt, IObserver<AuthorizationDecision> observer)
        {
            Debug.WriteLine($"Dispose: {10000}");
            await Task.Delay(10000);
            evt.Dispose();
            observer.OnCompleted();
        }

        private static bool IsServerCertificateValid(HttpRequestMessage requestMessage, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslErrors)
        {
            return true;
        }

        #endregion
    }

}
