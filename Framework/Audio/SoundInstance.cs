using System.Numerics;

namespace Foster.Framework.Audio;

/// <summary>
/// A lightweight handle for interacting with sound instances.
/// Will automatically be disposed if <see cref="Finished"/> is true and <see cref="Protected"/> is false.
/// <see cref="Active"/> will return false if this sound instance has been disposed of.
/// </summary>
public readonly struct SoundInstance
{
	public float Volume
	{
		get => Get(s => Platform.FosterSoundGetVolume(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetVolume(s.Ptr, v));
	}

	public float Pitch
	{
		get => Get(s => Platform.FosterSoundGetPitch(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetPitch(s.Ptr, v));
	}

	public float Pan
	{
		get => Get(s => Platform.FosterSoundGetPan(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetPan(s.Ptr, v));
	}

	public bool Playing
	{
		get => Get(s => Platform.FosterSoundGetPlaying(s.Ptr));
	}

	public bool Finished
	{
		get => Get(s => Platform.FosterSoundGetFinished(s.Ptr));
	}

	public ulong LengthPcmFrames
	{
		get => Get(s => Platform.FosterSoundGetLengthPcmFrames(s.Ptr));
	}

	public TimeSpan Length => TimeSpan.FromSeconds(1.0 * LengthPcmFrames / Audio.SampleRate);

	public ulong CursorPcmFrames
	{
		get => Get(s => Platform.FosterSoundGetCursorPcmFrames(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetCursorPcmFrames(s.Ptr, v));
	}

	public TimeSpan Cursor
	{
		get => TimeSpan.FromSeconds(1.0 * CursorPcmFrames / Audio.SampleRate);
		set => CursorPcmFrames = (ulong)Math.Floor(value.TotalSeconds * Audio.SampleRate);
	}

	public bool Looping
	{
		get => Get(s => Platform.FosterSoundGetLooping(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetLooping(s.Ptr, v));
	}

	public ulong LoopBeginPcmFrames
	{
		get => Get(s => Platform.FosterSoundGetLoopBeginPcmFrames(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetLoopBeginPcmFrames(s.Ptr, v));
	}

	public TimeSpan LoopBegin
	{
		get => TimeSpan.FromSeconds(1.0 * LoopBeginPcmFrames / Audio.SampleRate);
		set => LoopBeginPcmFrames = (ulong)Math.Floor(value.TotalSeconds * Audio.SampleRate);
	}

	public ulong LoopEndPcmFrames
	{
		get => Get(s => Platform.FosterSoundGetLoopEndPcmFrames(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetLoopEndPcmFrames(s.Ptr, v));
	}

	public TimeSpan? LoopEnd
	{
		get
		{
			var end = LoopEndPcmFrames;
			if (end == ulong.MaxValue)
			{
				return null; // Special case
			}
			return TimeSpan.FromSeconds(1.0 * end / Audio.SampleRate);
		} 
		set
		{
			if(value.HasValue)
			{
				LoopEndPcmFrames = (ulong)Math.Floor(value.Value.TotalSeconds * Audio.SampleRate);
			}
			else
			{
				LoopEndPcmFrames = ulong.MaxValue;
			}
		}
	}

	public bool Spatialized
	{
		get => Get(s => Platform.FosterSoundGetSpatialized(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetSpatialized(s.Ptr, v));
	}

	public Vector3 Position
	{
		get => Get(s => Platform.FosterSoundGetPosition(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetPosition(s.Ptr, v));
	}

	public Vector3 Velocity
	{
		get => Get(s => Platform.FosterSoundGetVelocity(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetVelocity(s.Ptr, v));
	}

	public Vector3 Direction
	{
		get => Get(s => Platform.FosterSoundGetDirection(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetDirection(s.Ptr, v));
	}

	public SoundPositioning Positioning
	{
		get => Get(s => Platform.FosterSoundGetPositioning(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetPositioning(s.Ptr, v));
	}

	public int PinnedListenerIndex
	{
		get => Get(s => Platform.FosterSoundGetPinnedListenerIndex(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetPinnedListenerIndex(s.Ptr, v));
	}

	public SoundAttenuationModel AttenuationModel
	{
		get => Get(s => Platform.FosterSoundGetAttenuationModel(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetAttenuationModel(s.Ptr, v));
	}

	public float Rolloff
	{
		get => Get(s => Platform.FosterSoundGetRolloff(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetRolloff(s.Ptr, v));
	}

	public float MinGain
	{
		get => Get(s => Platform.FosterSoundGetMinGain(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetMinGain(s.Ptr, v));
	}

	public float MaxGain
	{
		get => Get(s => Platform.FosterSoundGetMaxGain(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetMaxGain(s.Ptr, v));
	}

	public float MinDistance
	{
		get => Get(s => Platform.FosterSoundGetMinDistance(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetMinDistance(s.Ptr, v));
	}

	public float MaxDistance
	{
		get => Get(s => Platform.FosterSoundGetMaxDistance(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetMaxDistance(s.Ptr, v));
	}

	public SoundCone Cone
	{
		get => Get(s => Platform.FosterSoundGetCone(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetCone(s.Ptr, v));
	}

	public float DirectionalAttenuationFactor
	{
		get => Get(s => Platform.FosterSoundGetDirectionalAttenuationFactor(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetDirectionalAttenuationFactor(s.Ptr, v));
	}

	public float DopplerFactor
	{
		get => Get(s => Platform.FosterSoundGetDopplerFactor(s.Ptr));
		set => Set(value, (s, v) => Platform.FosterSoundSetDopplerFactor(s.Ptr, v));
	}

	public bool Protected
	{
		get => Get(s => s.Protected);
		set => Audio.TrySetSoundInstanceProtected(Id, value);
	}

	public Sound? Sound => Get(s => s.Sound);
	public bool Active => Audio.IsSoundInstanceActive(Id);


	internal readonly long Id;

	internal SoundInstance(long id)
	{
		Id = id;
	}

	public void Play()
	{
		if (Audio.TryGetSoundInstanceState(Id, out var state))
		{
			Platform.FosterSoundPlay(state.Ptr);
		}
	}

	public void Pause()
	{
		if (Audio.TryGetSoundInstanceState(Id, out var state))
		{
			Platform.FosterSoundStop(state.Ptr);
		}
	}

	public void Stop()
	{
		if (Audio.TryGetSoundInstanceState(Id, out var state))
		{
			if (state.Protected)
			{
				Platform.FosterSoundStop(state.Ptr);
				CursorPcmFrames = 0;
			}
			else
			{
				Dispose();
			}
		}
	}

	//public void Fade(...) { }

	//public void SchedulePlay(...) { }

	//public void ScheduleStop(...) { }

	//public void ScheduleFade(...) { }

	private T Get<T>(Func<SoundInstanceState, T> func)
	{
		if (Audio.TryGetSoundInstanceState(Id, out var state))
		{
			return func(state);
		}
		return default!;
	}

	private void Set<T>(T value, Action<SoundInstanceState, T> func)
	{
		if (Audio.TryGetSoundInstanceState(Id, out var state))
		{
			func(state, value);
		}
	}

	public void Dispose()
	{
		Audio.DestroySoundInstance(Id);
	}
}
