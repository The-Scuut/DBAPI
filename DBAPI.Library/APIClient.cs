namespace DBAPI.Library
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DBAPI.Library.Models;

    public class APIClient : IDisposable
    {
        public string Host { get; }
        private readonly Guid _token;
        private HttpClient _client;
        private readonly Dictionary<object, object[]> _trackedObjects = new Dictionary<object, object[]>();

        public APIClient(APIClientParameters parameters)
        {
            _ = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Host = parameters.UseSSL
                ? $"https://{parameters.Host}:{parameters.Port}"
                : $"http://{parameters.Host}:{parameters.Port}";
            if (!Uri.IsWellFormedUriString(Host, UriKind.Absolute))
                throw new ArgumentException("Invalid host format");
            if (parameters.Token == "null" || string.IsNullOrWhiteSpace(parameters.Token))
                _token = Guid.Empty;
            else
            {
                if (!Guid.TryParse(parameters.Token, out _token))
                    throw new ArgumentException("Invalid token format");
            }
            _client = new HttpClient();
            AddToken(_client);
        }

        public void Connect()
        {
            var getInfoClient = new HttpClient(new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });
            AddToken(getInfoClient);
            try
            {
                var serverInfo = getInfoClient.GetAsync(Host+"/Application/V1/instance/getinfo").GetAwaiter().GetResult();
                var content = serverInfo.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!serverInfo.IsSuccessStatusCode)
                    throw new HttpRequestException("Connection unsuccessful, server returned " + serverInfo.StatusCode + " " +  content);
                var stringInfo = content.TrimStart('{').TrimEnd('}');
                var split = stringInfo.Split(',');
                bool selfSigned = false;
                foreach (var value in split)
                {
                    string[] values = value.Replace("\"", "").Split(':');
                    string key = values.FirstOrDefault() ??
#if DEBUG
                                 throw new HttpRequestException();
#else
                                 string.Empty;
#endif
                    switch (key)
                    {
                        case "HttpsEnabled":
                            break;
                        case "SelfSigned":
                            if (bool.TryParse(values.LastOrDefault(), out var result) && result)
                                selfSigned = true;
                            break;
                        case "Certificate":
                            var cert = values.LastOrDefault() ?? 
#if DEBUG
                                    throw new HttpRequestException();
#else
                                    string.Empty;
#endif
                            if (selfSigned)
                            {
                                _client.Dispose();
                                byte[] certHash = Convert.FromBase64String(cert);
                                _client = new HttpClient(new HttpClientHandler()
                                {
                                    ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert.GetCertHash().SequenceEqual(certHash),
                                });
                                AddToken(_client);
                            }
                            break;
                        default:
#if DEBUG
                            throw new HttpRequestException("Unknown key: " + key);
#endif
                            break;
                    }
                }
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException("Could not connect to server", e);
            }
            finally
            {
                getInfoClient.Dispose();
            }
        }

        public Action<string, string> OnMessageReceived = (s, s2) => { };

        public void Dispose()
        {
            _client?.Dispose();
            _client = null;
        }

        ~APIClient()
        {
            Dispose();
        }

        public Task ListenToMessages(string channel, float interval = 3)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentException("Channel cannot be null or empty", nameof(channel));
            if (channel.Contains(" "))
                throw new ArgumentException("Channel cannot contain spaces", nameof(channel));
            Action<string> callback = (s) => { OnMessageReceived(channel, s);};
            return Task.Run(() => ListenToMessagesAsync(channel, CancellationToken.None, callback, interval));
        }

        private async Task ListenToMessagesAsync(string channel, CancellationToken token, Action<string> callback, float interval)
        {
            int failedAttempts = 0;
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), token);
                var response = await _client.GetAsync(Host + $"/Application/V1/messaging/read/{channel}", token);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    failedAttempts++;
                    if (failedAttempts > 5)
                        throw new HttpRequestException("Connection exceeded 5 failed attempts, last status code: " + response.StatusCode + " " +  content);
                    continue;
                }
                failedAttempts = 0;
                if (string.IsNullOrWhiteSpace(content) || content == "null" || content == "[]")
                    continue;
                content = content.TrimStart('[').TrimEnd(']');
                foreach (var message in content.Split(';'))
                {
                    callback(message);
                }
            }
        }

        public void SendMessage<T>(string channel, T message) where T : class
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentException("Channel cannot be null or empty", nameof(channel));
            if (channel.Contains(" "))
                throw new ArgumentException("Channel cannot contain spaces", nameof(channel));
            if (message == null)
                throw new ArgumentException("Message cannot be null", nameof(message));
            var converter = ObjectConverter.GetTypeConverter<T>();
            var response = _client.PostAsync(Host + $"/Application/V1/messaging/send/{channel}", new StringContent(converter.SerializeForMessage(new []{message}), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not send message, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        public void SendMessages<T>(string channel, IEnumerable<T> messages) where T : class
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentException("Channel cannot be null or empty", nameof(channel));
            if (channel.Contains(" "))
                throw new ArgumentException("Channel cannot contain spaces", nameof(channel));
            if (messages == null)
                throw new ArgumentException("Messages cannot be null", nameof(messages));
            var converter = ObjectConverter.GetTypeConverter<T>();
            var response = _client.PostAsync(Host + $"/Application/V1/messaging/send/{channel}", new StringContent(converter.SerializeForMessage(messages), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not send messages, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        public T[] ReadMessages<T>(string channel, bool peek = false) where T : class
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentException("Channel cannot be null or empty", nameof(channel));
            if (channel.Contains(" "))
                throw new ArgumentException("Channel cannot contain spaces", nameof(channel));
            string path = peek ? "peek" : "read";
            var response = _client.GetAsync(Host + $"/Application/V1/messaging/{path}/{channel}").GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not peek messages, server returned " + response.StatusCode + " " +  content);

            if (string.IsNullOrWhiteSpace(content) || content == "null" || content == "[]")
                return Array.Empty<T>();
            var converter = ObjectConverter.GetTypeConverter<T>();
            return converter.DeserializeEnumerableMessage(content).ToArray();
        }

        public void ClearMessages(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentException("Channel cannot be null or empty", nameof(channel));
            if (channel.Contains(" "))
                throw new ArgumentException("Channel cannot contain spaces", nameof(channel));
            var response = _client.DeleteAsync(Host + $"/Application/V1/messaging/clear/{channel}").GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not clear messages, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        public string[] GetChannels()
        {
            var response = _client.GetAsync(Host + $"/Application/V1/messaging/list").GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not get channels, server returned " + response.StatusCode + " " +  content);
            return content.TrimStart('[').TrimEnd(']').Replace("\"", "").Split(',');
        }

        public string[] GetTables()
        {
            var response = _client.GetAsync(Host + $"/Application/V1/datastore/table/show").GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not get tables, server returned " + response.StatusCode + " " +  content);
            return content.TrimStart('[').TrimEnd(']').Replace("\"", "").Split(',');
        }

        public T[] GetCollection<T>(string tableName) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            var response = _client.GetAsync(Host + $"/Application/V1/datastore/selectall/{tableName}").GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not get collection, server returned " + response.StatusCode + " " +  content);
            var converter = ObjectConverter.GetTypeConverter<T>();
            return Track(converter.DeserializeEnumerableMessage(content).ToArray());
        }

        public T? GetById<T>(string tableName, ulong id) where T : class, IIDEntity
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/select/{tableName}", new StringContent($"*|ID='{id}'", Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not get by id, server returned " + response.StatusCode + " " +  content);
            var converter = ObjectConverter.GetTypeConverter<T>();
            var result = converter.DeserializeSql(content);
            return result.Length == 0 ? null : Track(result[0]);
        }
        
        public void CreateCollection<T>(string tableName) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            var converter = ObjectConverter.GetTypeConverter<T>();
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/table/create/{tableName}", new StringContent(converter.MySqlTypesString, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not create collection, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        public void EnsureCollectionExists<T>(string tableName) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            var converter = ObjectConverter.GetTypeConverter<T>();
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/table/ensureexist/{tableName}", new StringContent(converter.MySqlTypesString, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not create collection, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        public void Insert<T>(string tableName, T entity) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            if (entity == null)
                throw new ArgumentException("Entity cannot be null", nameof(entity));
            var converter = ObjectConverter.GetTypeConverter<T>();
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/insert/{tableName}", new StringContent(converter.SerializeSql(entity), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not insert entity, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        public void Insert<T>(string tableName, IEnumerable<T> entities) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            if (entities == null)
                throw new ArgumentException("Entities cannot be null", nameof(entities));
            var converter = ObjectConverter.GetTypeConverter<T>();
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/insert/{tableName}", new StringContent(converter.SerializeSql(entities), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not insert entities, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        public void Delete<T>(string tableName, T entity) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            if (entity == null)
                throw new ArgumentException("Entity cannot be null", nameof(entity));
            var converter = ObjectConverter.GetTypeConverter<T>();
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/delete/{tableName}", new StringContent(converter.SerializeSqlWhere(entity), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not delete entity, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            Untrack(entity);
        }

        public void Update<T>(string tableName, T entity) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            if (entity == null)
                throw new ArgumentException("Entity cannot be null", nameof(entity));
            if (!_trackedObjects.TryGetValue(entity, out var tracked))
                throw new ArgumentException("Entity is not tracked", nameof(entity));
            var converter = ObjectConverter.GetTypeConverter<T>();
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/update/{tableName}", new StringContent(converter.SerializeSqlSet(entity)+"|"+SerializeSqlTracked<T>(tracked), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not update entity, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        private void AddToken(HttpClient client)
        {
            client.DefaultRequestHeaders.Add("token", Convert.ToBase64String(Encoding.UTF8.GetBytes(_token.ToString())));
        }

        private T Track<T>(T obj) where T : class
        {
            var converter = ObjectConverter.GetTypeConverter<T>();
            var properties = converter.TrackedProperties;
            object[] values = new object[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                var currValue = properties[i].GetValue(obj) ?? "NULL";
                if (currValue is string valueString)
                    values[i] = valueString.Replace("\"", "");
                else
                    values[i] = currValue;
            }
            _trackedObjects.Add(obj, values);
            return obj;
        }

        private IEnumerable<T> Track<T>(IEnumerable<T> objs) where T : class
        {
            foreach (var obj in objs)
            {
                Track(obj);
            }
            return objs;
        }

        private void Untrack<T>(T obj) where T : class
        {
            _trackedObjects.Remove(obj);
        }

        private string SerializeSqlTracked<T>(object[] trackedValues) where T : class
        {
            var converter = ObjectConverter.GetTypeConverter<T>();
            var properties = converter.TrackedProperties;
            string[] values = new string[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var value = trackedValues[i];
                if (value == null || value == "NULL")
                    values[i] += $"{property.Name}=NULL";
                else if (property.PropertyType == typeof(string))
                    values[i] += $"{property.Name}='{value}'";
                else
                    values[i] += $"{property.Name}={value}";
            }
            return $"{string.Join(" AND ", values)}";
        }

        public void Ping(string server)
        {
            if (string.IsNullOrWhiteSpace(server))
                throw new ArgumentException("Server cannot be null or empty", nameof(server));
            if (server.Contains(" "))
                throw new ArgumentException("Server cannot contain spaces", nameof(server));
            var response = _client.GetAsync(Host + $"/Application/V1/messaging/servers/ping/{server}").GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not ping server, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        public DateTime GetServerStatus(string server)
        {
            if (string.IsNullOrWhiteSpace(server))
                throw new ArgumentException("Server cannot be null or empty", nameof(server));
            if (server.Contains(" "))
                throw new ArgumentException("Server cannot contain spaces", nameof(server));
            var response = _client.GetAsync(Host + $"/Application/V1/messaging/servers/status/{server}").GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not get server time, server returned " + response.StatusCode + " " +  content);
            var unixTimeMilliseconds = long.Parse(content);
            return DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds).UtcDateTime;
        }

        public string[] GetServers()
        {
            var response = _client.GetAsync(Host + $"/Application/V1/messaging/servers/list").GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not get servers, server returned " + response.StatusCode + " " +  content);
            return content.TrimStart('[').TrimEnd(']').Replace("\"", "").Split(',');
        }

        public void ClearServer(string server)
        {
            if (string.IsNullOrWhiteSpace(server))
                throw new ArgumentException("Server cannot be null or empty", nameof(server));
            if (server.Contains(" "))
                throw new ArgumentException("Server cannot contain spaces", nameof(server));
            var response = _client.GetAsync(Host + $"/Application/V1/messaging/servers/clear/{server}").GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not clear server, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }
    }
}