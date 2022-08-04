using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Botsome.Util; 

/// <summary>
/// An IDictionary that has automatically removes items, some time after they are added.
/// </summary>
public class ExpiringDictionary<TKey, TValue> where TKey : notnull where TValue : class {
	private readonly IDictionary<TKey, Entry> m_DictionaryImpl = new Dictionary<TKey, Entry>();
	private readonly object m_Lock = new();
	private readonly TimeSpan m_Expiration;

	public ICollection<TKey> Keys {
		get {
			lock (m_Lock) {
				return m_DictionaryImpl.Keys;
			}
		}
	}

	public int Count {
		get {
			lock (m_Lock) {
				return m_DictionaryImpl.Count;
			}
		}
	}

	public event EventHandler<KeyValuePair<TKey, TValue>>? EntryExpired;

	public ExpiringDictionary(TimeSpan expiration) {
		m_Expiration = expiration;
	}

	public bool Remove(TKey key) {
		lock (m_Lock) {
			return m_DictionaryImpl.Remove(key);
		}
	}

	public bool ContainsKey(TKey key) {
		lock (m_Lock) {
			return m_DictionaryImpl.ContainsKey(key);
		}
	}

	public void Add(TKey key, TValue value) {
		lock (m_Lock) {
			m_DictionaryImpl.Add(key, new Entry(this, key, value));
		}
	}
	
	public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) {
		bool ret;
		Entry? entry;

		lock (m_Lock) {
			ret = m_DictionaryImpl.TryGetValue(key, out entry);
		}
		
		value = ret ? entry!.Value : default;
		return ret;
	}

	public bool AddOrUpdate(TKey key, Func<TKey, TValue> valueFactory, Action<TKey, TValue> updater) {
		lock (m_Lock) {
			if (m_DictionaryImpl.TryGetValue(key, out Entry? entry)) {
				updater(key, entry.Value);
				return true;
			} else {
				Add(key, valueFactory(key));
				return false;
			}
		}
	}

	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
		ImmutableList<KeyValuePair<TKey, Entry>> kvps;
		
		lock (m_Lock) {
			kvps = m_DictionaryImpl.ToImmutableList();
		}

		foreach ((TKey key, Entry value) in kvps) {
			yield return new KeyValuePair<TKey, TValue>(key, value.Value);
		}
	}

	private void OnExpired(Entry entry) {
		lock (m_Lock) {
			m_DictionaryImpl.Remove(entry.Key);

			EntryExpired?.Invoke(this, new KeyValuePair<TKey, TValue>(entry.Key, entry.Value));
		}
	}

	private class Entry {
		private readonly Timer m_Timer;
		private readonly ExpiringDictionary<TKey, TValue> m_Dictionary;
		
		public TKey Key { get; }
		public TValue Value { get; }

		internal Entry(ExpiringDictionary<TKey, TValue> dictionary, TKey key, TValue value) {
			m_Dictionary = dictionary;
			Key = key;
			Value = value;
			
			m_Timer = new Timer();
			m_Timer.Interval = m_Dictionary.m_Expiration.TotalMilliseconds;
			m_Timer.AutoReset = false;
			m_Timer.Elapsed += Elapsed;
			m_Timer.Start();
		}

		private void Elapsed(object? sender, ElapsedEventArgs ea) {
			m_Timer.Elapsed -= Elapsed;
			m_Timer.Dispose();
			m_Dictionary.OnExpired(this);
		}
	}
}
//*/
