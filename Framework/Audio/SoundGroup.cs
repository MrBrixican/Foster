namespace Foster.Framework.Audio;

public class SoundGroup: IDisposable
{
	public string Name { get; set; }

	public float Volume
	{
		get => Platform.FosterSoundGroupGetVolume(Ptr);
		set => Platform.FosterSoundGroupSetVolume(Ptr, value);
	}

	internal IntPtr Ptr { get; set; }

	public SoundGroup(string? name = null)
	{
		Name = name ?? string.Empty;
		Ptr = Platform.FosterSoundGroupCreate();

		if (Ptr == IntPtr.Zero)
		{
			throw new Exception("Failed to create SoundGroup");
		}
	}

	~SoundGroup() => Dispose();

	public void Dispose()
	{
		if (Ptr != IntPtr.Zero)
		{
			Platform.FosterSoundGroupDestroy(Ptr);
			Ptr = IntPtr.Zero;
		}
	}
}
