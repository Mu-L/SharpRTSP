﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Rtsp.Sdp
{
    public class H264Parameters : IDictionary<string, string>
    {
        private const string HeaderName = "sprop-parameter-sets";
        private readonly Dictionary<string, string> parameters = [];

        public IList<byte[]> SpropParameterSets =>
            TryGetValue(HeaderName, out var value)
                ? value.Split(',').Select(Convert.FromBase64String).ToList()
                : [];

        public static H264Parameters Parse(string parameterString)
        {
            var result = new H264Parameters();
            foreach (var pair in parameterString.Split(';').Select(x => x.Trim().Split('=', 2)))
            {
                if (!string.IsNullOrWhiteSpace(pair[0]))
                    result[pair[0]] = pair.Length > 1 ? pair[1] : string.Empty;
            }

            return result;
        }

        public override string ToString()
        {
            return parameters.Select(p => p.Key + (p.Value != null ? "=" + p.Value : string.Empty))
                .Aggregate((x, y) => x + ";" + y);
        }

        public string this[string index]
        {
            get => parameters[index];
            set => parameters[index] = value;
        }

        public int Count => parameters.Count;

        public bool IsReadOnly => ((IDictionary<string, string>)parameters).IsReadOnly;

        public ICollection<string> Keys => ((IDictionary<string, string>)parameters).Keys;

        public ICollection<string> Values => ((IDictionary<string, string>)parameters).Values;

        public void Add(KeyValuePair<string, string> item) => ((IDictionary<string, string>)parameters).Add(item);

        public void Add(string key, string value) => parameters.Add(key, value);

        public void Clear() => parameters.Clear();

        public bool Contains(KeyValuePair<string, string> item) =>
            ((IDictionary<string, string>)parameters).Contains(item);

        public bool ContainsKey(string key) => parameters.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) =>
            ((IDictionary<string, string>)parameters).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
            ((IDictionary<string, string>)parameters).GetEnumerator();

        public bool Remove(KeyValuePair<string, string> item) => ((IDictionary<string, string>)parameters).Remove(item);

        public bool Remove(string key) => parameters.Remove(key);

        public bool TryGetValue(string key, out string value) => parameters.TryGetValue(key, out value!);

        IEnumerator IEnumerable.GetEnumerator() => ((IDictionary<string, string>)parameters).GetEnumerator();
    }
}