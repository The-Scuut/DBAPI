namespace DBAPI.Library
{
    using System;
    using System.Collections;
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
        private readonly Dictionary<string, Dictionary<object, object[]>> _trackedObjects = new ();

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
            if (string.IsNullOrWhiteSpace(content) || content == "null" || content == "[]")
                return Array.Empty<T>();
            return TrackArray(tableName, converter.DeserializeSql(content).ToArray());
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
            return result.Length == 0 ? null : Track(tableName, result[0]);
        }

        public T[]? GetByField<T>(string tableName, string property, object? value) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/select/{tableName}", new StringContent($"*|{property}={ObjectConverter.FromType(value)}", Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not get by field, server returned " + response.StatusCode + " " +  content);
            var converter = ObjectConverter.GetTypeConverter<T>();
            var result = converter.DeserializeSql(content);
            return result.Length == 0 ? null : TrackArray(tableName, result);
        }

        public T[]? GetByFields<T>(string tableName, params (string, object?)[] properties) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            if (properties.Length == 0)
                throw new ArgumentException("Properties cannot be empty", nameof(properties));
            var converter = ObjectConverter.GetTypeConverter<T>();
            if (properties.Any(x => converter.TrackedProperties.All(y => y.Name != x.Item1)))
                throw new ArgumentException("One or more properties do not exist in the table", nameof(properties));
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/select/{tableName}", new StringContent($"*|{string.Join(" AND ", properties.Select(x => $"{x.Item1}={ObjectConverter.FromType(x.Item2)}"))}", Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not get by fields, server returned " + response.StatusCode + " " +  content);
            var result = converter.DeserializeSql(content);
            return result.Length == 0 ? null : TrackArray(tableName, result);
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

        public void DeleteCollection(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            var response = _client.DeleteAsync(Host + $"/Application/V1/datastore/table/delete/{tableName}").GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not delete collection, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            UntrackTable(tableName);
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
            Track(tableName, entity);
        }

        public void Insert<T>(string tableName, IEnumerable<T> entities) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            if (entities == null)
                throw new ArgumentException("Entities cannot be null", nameof(entities));
            entities = entities.ToArray();
            var converter = ObjectConverter.GetTypeConverter<T>();
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/insert/{tableName}", new StringContent(converter.SerializeSql(entities), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not insert entities, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            TrackArray(tableName, entities.ToArray());
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

        public void DeleteById(string tableName, long id)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            var response = _client.PostAsync(Host + $"/Application/V1/datastore/delete/{tableName}", new StringContent($"ID = {id}", Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Could not delete entity by id, server returned " + response.StatusCode + " " +  response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            UntrackId(id);
        }

        public void Update<T>(string tableName, T entity) where T : class
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            if (tableName.Contains(" "))
                throw new ArgumentException("Table name cannot contain spaces", nameof(tableName));
            if (entity == null)
                throw new ArgumentException("Entity cannot be null", nameof(entity));
            if (!_trackedObjects.TryGetValue(tableName, out var tableDict))
                throw new ArgumentException("Table is not tracked", nameof(entity));
            if (!tableDict.TryGetValue(entity, out var tracked))
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

        private object Track(string table, object obj)
        {
            if (obj is IEnumerable enumerable)
            {
                return TrackArray(table, enumerable.Cast<object>().ToArray());
            }
            return Track(table, obj, false);
        }

        private T Track<T>(string table, T obj, bool useGeneric = true) where T : class
        {
            var converter = useGeneric
                ? ObjectConverter.GetTypeConverter<T>()
                : ObjectConverter.GetTypeConverter(obj.GetType());
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

            if (_trackedObjects.TryGetValue(table, out var dict))
                dict.Add(obj, values);
            else
                _trackedObjects.Add(table, new Dictionary<object, object[]>());
            return obj;
        }

        private T[] TrackArray<T>(string table, T[] objs, bool useGeneric = true) where T : class
        {
            foreach (var obj in objs)
            {
                Track(table, obj, useGeneric);
            }
            return objs;
        }

        private object[] TrackArray(string table, object[] objs)
        {
            foreach (var obj in objs)
            {
                Track(table, obj, false);
            }
            return objs;
        }

        private bool Untrack<T>(T obj) where T : class
        {
            foreach (var dict in _trackedObjects)
            {
                if (dict.Value.Remove(obj))
                    return true;
            }

            return false;
        }

        private bool UntrackId(long id)
        {
            foreach (var dict in _trackedObjects)
            {
                if (dict.Value.Count == 0 || dict.Value.First().Key is not IIDEntity entity)
                    continue;
                if (dict.Value.Any(x => x.Value.Any(x => x is long longValue && longValue == id)))
                    return dict.Value.Remove(entity);
            }

            return false;
        }

        private void UntrackTable(string table)
        {
            _trackedObjects.Remove(table);
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
                values[i] = $"{property.Name} = {ObjectConverter.FromType(value)}";
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