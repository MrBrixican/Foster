using System.Collections.ObjectModel;

namespace Foster.Framework.Audio;

public static class Audio
{
	public static float Volume
	{
		get => Platform.FosterAudioGetVolume();
		set => Platform.FosterAudioSetVolume(value);
	}

	public static int Channels { get; private set; }

	public static int SampleRate { get; private set; }

	public static ulong TimePcmFrames
	{
		get => Platform.FosterAudioGetTimePcmFrames();
		set => Platform.FosterAudioSetTimePcmFrames(value);
	}

	public static TimeSpan Time { get; set; } // TODO

	public static int MaxInstances { get; set; } = 100;

	public static int ActiveInstances => instances.Count;

	public static AudioListener Listener { get; private set; } = null!;

	public static ReadOnlyCollection<AudioListener> Listeners { get; private set; } = null!;

	private static long nextInstanceId = 1;
	private static readonly Dictionary<long, SoundInstanceState> instances = new();
	private static readonly List<long> instancesToDestroy = new();

	public static void PlayAll() { }
	public static void PauseAll() { }
	public static void StopAll() { }

	public static void ApplyAll(Action<SoundInstance> action)
	{
		
	}

	internal static void Initialize()
	{
		Channels = Platform.FosterAudioGetChannels();
		SampleRate = Platform.FosterAudioGetSampleRate();
		Listeners = Enumerable.Range(0, Platform.FosterAudioGetListenerCount())
			.Select(i => new AudioListener(i))
			.ToList()
			.AsReadOnly();
		Listener = Listeners[0];
	}

	internal static void Step()
	{
		// Go through all instances and destroy all that are non-protected, finished
		foreach (var pair in instances)
		{
			if (pair.Value.Protected)
			{
				continue;
			}

			var instance = new SoundInstance(pair.Key);
			if (instance.Finished)
			{
				instancesToDestroy.Add(pair.Key);
			}
		}

		foreach (var id in instancesToDestroy)
		{
			DestroySoundInstance(id);
		}

		instancesToDestroy.Clear();
	}

	internal static bool TryCreateSoundInstance(Sound sound, SoundInstanceCreateFlags flags, out SoundInstance instance)
	{
		instance = default;

		// Ensure max sound threshold hasn't been reached
		if (sound.ActiveInstances >= sound.MaxInstances || ActiveInstances >= MaxInstances)
		{
			return false;
		}

		// Set some initial flags
		Platform.FosterSoundFlags fosterFlags = 0;

		if (flags.HasFlag(SoundInstanceCreateFlags.Streamed))
		{
			fosterFlags |= Platform.FosterSoundFlags.STREAM;
		}

		if (!flags.HasFlag(SoundInstanceCreateFlags.Spatialized))
		{
			fosterFlags |= Platform.FosterSoundFlags.NO_SPATIALIZATION;
		}

		// Attempt to create the sound
		var ptr = Platform.FosterSoundCreate(sound.Path, fosterFlags);

		// Ensure sound was actually created
		if (ptr == IntPtr.Zero)
		{
			return false;
		}

		// Create and store the instance
		var id = nextInstanceId++;

		var state = new SoundInstanceState(
			ptr,
			sound,
			null,
			flags.HasFlag(SoundInstanceCreateFlags.Protected)
			);

		instances[id] = state;

		// Increment active count
		sound.ActiveInstances++;

		instance = new(id);
		return true;
	}

	internal static void DestroySoundInstance(long id)
	{
		if (instances.TryGetValue(id, out var state))
		{
			// Attempt to destroy the sound
			Platform.FosterSoundDestroy(state.Ptr);

			// Remove the instance
			instances.Remove(id);

			// Decrement active count
			state.Sound.ActiveInstances--;
		}
	}

	internal static bool TryGetSoundInstanceState(long id, out SoundInstanceState state)
	{
		return instances.TryGetValue(id, out state);
	}

	internal static bool TrySetSoundInstanceProtected(long id, bool isProtected)
	{
		if(TryGetSoundInstanceState(id, out var state))
		{
			instances[id] = state with { Protected = isProtected };
			return true;
		}
		return false;
	}

	internal static bool IsSoundInstanceActive(long id)
	{
		return instances.ContainsKey(id);
	}
}
