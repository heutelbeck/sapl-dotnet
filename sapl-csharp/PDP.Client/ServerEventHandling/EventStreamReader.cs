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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace csharp.sapl.constraint.ServerEventHandling
{
    internal class EventStreamReader : IDisposable
    {
        private const string DefaultEventType = "message";

        public delegate void MessageReceivedHandler(object sender, EventStreamMessageEventArgs e);
        public delegate void DisconnectEventHandler(object sender, EventStreamDisconnectEventArgs e);

        private HttpClient httpClient;
        private Uri requestsUri;
        private HttpContent HttpContent { get; }
        private string authentification;



        private Stream stream = null!;
        private volatile bool isDisposed = false;
        private volatile bool isReading = false;
        private readonly object StartLock = new object();

        private int ReconnectDelay = 3000;
        private string LastEventId = string.Empty;

        public event MessageReceivedHandler MessageReceived = null!;
        public event DisconnectEventHandler Disconnected = null!;


        public EventStreamReader(string authentification, Uri requestsUri, HttpContent content, HttpMessageHandler handler = null!)
        {
            this.requestsUri = requestsUri;
            HttpContent = content;
            httpClient = new HttpClient(handler ?? new HttpClientHandler());

            this.authentification = authentification;
            httpClient.DefaultRequestHeaders.Add("Authorization", this.authentification);
        }


        /// <summary>
        /// Subscribes to Server events
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Start()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(EventStreamReader));
            }
            lock (StartLock)
            {
                if (isReading == false)
                {
                    isReading = true;

#pragma warning disable CS4014 // Awaiting is unnecessary
                    ReaderAsync(requestsUri, HttpContent);
#pragma warning restore CS4014 // Execution continues before the call is completed
                }
            }
        }


        /// <summary>
        /// Stop and dispose of the EventStreamReader
        /// </summary>
        public void Dispose()
        {
            isDisposed = true;
            stream?.Dispose();
            httpClient.CancelPendingRequests();
            httpClient.Dispose();
        }

        //Post
        private async Task ReaderAsync(Uri postUri, HttpContent content)
        {
            try
            {
                if (string.Empty != LastEventId)
                {
                    if (httpClient.DefaultRequestHeaders.Contains("Last-Event-Id"))
                    {
                        httpClient.DefaultRequestHeaders.Remove("Last-Event-Id");
                    }

                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Last-Event-Id", LastEventId);
                }

                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, postUri);
                httpRequestMessage.Content = HttpContent;

                using (HttpResponseMessage response = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead))
                {

                    response.EnsureSuccessStatusCode();
                    if (response.Headers.TryGetValues("content-type", out IEnumerable<string> ctypes) || ctypes?.Contains("text/event-stream") == false)
                    {
                        throw new ArgumentException("Specified URI does not return server-sent events");
                    }

                    stream = await response.Content.ReadAsStreamAsync();
                    using (var sr = new StreamReader(stream))
                    {
                        string evt = DefaultEventType;
                        string id = string.Empty;
                        var data = new StringBuilder(string.Empty);

                        while (true)
                        {
                            string line = await sr.ReadLineAsync();
                            if (line == string.Empty)
                            {
                                // raise Event to give data to the observer
                                if (data.Length > 0)
                                {
                                    MessageReceived?.Invoke(this, new EventStreamMessageEventArgs(data.ToString().Trim(), evt, id));
                                }
                                data.Clear();
                                id = string.Empty;
                                evt = DefaultEventType;
                                continue;
                            }
                            else if (line.First() == ':')
                            {
                                // Ignore comments
                                continue;
                            }

                            int dataIndex = line.IndexOf(':');
                            string field;
                            if (dataIndex == -1)
                            {
                                dataIndex = line.Length;
                                field = line;
                            }
                            else
                            {
                                field = line.Substring(0, dataIndex);
                                dataIndex += 1;
                            }

                            string value = line.Substring(dataIndex).Trim();

                            switch (field)
                            {
                                case "event":
                                    // Set event type
                                    evt = value;
                                    break;
                                case "data":
                                    // Append a line to data using a single \n as EOL
                                    data.Append($"{value}\n");
                                    break;
                                case "retry":
                                    // Set reconnect delay for next disconnect from server-info
                                    int.TryParse(value, out ReconnectDelay);
                                    break;
                                case "id":
                                    // Set ID
                                    LastEventId = value;
                                    id = LastEventId;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Disconnect(ex);
            }
        }

        private void Disconnect(Exception ex)
        {
            isReading = false;
            Disconnected?.Invoke(this, new EventStreamDisconnectEventArgs(ReconnectDelay, ex));
        }
    }
}