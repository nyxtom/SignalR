﻿using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SignalR.Client.Transports;
using System.Net;

namespace SignalR.Client {
    public class Connection {
        public event Action<string> Received;
        public event Action<Exception> Error;
        public event Action Closed;

        private readonly IClientTransport _transport = new LongPollingTransport();

        public Connection(string url) {
            if (!url.EndsWith("/")) {
                url += "/";
            }

            Url = url;
        }

        public ICredentials Credentials { get; set; }

        public Func<string> Sending { get; set; }

        public string Url { get; private set; }

        public bool IsActive { get; private set; }

        internal long? MessageId { get; set; }

        internal string ClientId { get; set; }

        public virtual Task Start() {
            if (IsActive) {
                return TaskAsyncHelper.Empty;
            }

            IsActive = true;

            string data = null;

            if (Sending != null) {
                data = Sending();
            }

            string negotiateUrl = Url + "negotiate";

            return HttpHelper.PostAsync(negotiateUrl, this.PrepareWebRequest).Success(task => {
                string raw = task.Result.ReadAsString();

                var negotiationResponse = JsonConvert.DeserializeObject<NegotiationResponse>(raw);

                ClientId = negotiationResponse.ClientId;

                _transport.Start(this, data);
            });
        }

        public virtual void Stop() {
            try {
                _transport.Stop(this);

                if (Closed != null) {
                    Closed();
                }
            }
            finally {
                IsActive = false;
            }
        }

        private void PrepareWebRequest(WebRequest request)
        {
            if (this.Credentials != null)
                request.Credentials = this.Credentials;
        }

        public Task Send(string data) {
            return Send<object>(data);
        }

        public Task<T> Send<T>(string data) {
            return _transport.Send<T>(this, data);
        }

        internal void RaiseOnReceived(string message) {
            if (Received != null) {
                Received(message);
            }
        }

        internal void RaiseOnError(Exception error) {
            if (Error != null) {
                Error(error);
            }
        }
    }
}
